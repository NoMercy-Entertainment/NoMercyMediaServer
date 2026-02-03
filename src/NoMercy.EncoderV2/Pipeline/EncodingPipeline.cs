using System.Collections.Concurrent;
using NoMercy.EncoderV2.Abstractions;
using NoMercy.EncoderV2.Commands;

namespace NoMercy.EncoderV2.Pipeline;

/// <summary>
/// Default implementation of IEncodingPipeline
/// </summary>
public sealed class EncodingPipeline : IEncodingPipeline, IDisposable
{
    private readonly IFFmpegExecutor _executor;
    private readonly IMediaAnalyzer _analyzer;
    private readonly IProfileRegistry _profileRegistry;
    private readonly ConcurrentDictionary<string, JobContext> _jobs = new();
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly CancellationTokenSource _shutdownCts = new();
    private bool _disposed;

    public event EventHandler<EncodingJobStatus>? JobProgressChanged;
    public event EventHandler<EncodingJobResult>? JobCompleted;

    public EncodingPipeline(
        IFFmpegExecutor executor,
        IMediaAnalyzer analyzer,
        IProfileRegistry profileRegistry,
        int maxConcurrentJobs = 1)
    {
        _executor = executor;
        _analyzer = analyzer;
        _profileRegistry = profileRegistry;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrentJobs, maxConcurrentJobs);
    }

    public Task<string> SubmitJobAsync(EncodingJob job, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        JobContext context = new()
        {
            Job = job,
            Status = new EncodingJobStatus
            {
                JobId = job.Id,
                State = JobState.Pending,
                Progress = 0
            },
            CancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token, cancellationToken)
        };

        if (!_jobs.TryAdd(job.Id, context))
        {
            throw new InvalidOperationException($"Job with ID {job.Id} already exists");
        }

        // Start the job asynchronously
        _ = ProcessJobAsync(context);

        return Task.FromResult(job.Id);
    }

    public Task<EncodingJobStatus?> GetJobStatusAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobId, out JobContext? context))
        {
            return Task.FromResult<EncodingJobStatus?>(context.Status);
        }

        return Task.FromResult<EncodingJobStatus?>(null);
    }

    public async Task<EncodingJobResult> WaitForCompletionAsync(
        string jobId,
        IProgress<EncodingJobStatus>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out JobContext? context))
        {
            throw new InvalidOperationException($"Job with ID {jobId} not found");
        }

        // Wait for completion
        while (!context.CompletionSource.Task.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(context.Status);
            await Task.Delay(500, cancellationToken);
        }

        return await context.CompletionSource.Task;
    }

    public Task<bool> CancelJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobId, out JobContext? context))
        {
            context.CancellationSource.Cancel();
            UpdateJobStatus(context, JobState.Cancelled);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> PauseJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobId, out JobContext? context) && context.ProcessId.HasValue)
        {
            _ = _executor.PauseAsync(context.ProcessId.Value);
            UpdateJobStatus(context, JobState.Paused);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> ResumeJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobId, out JobContext? context) && context.ProcessId.HasValue)
        {
            _ = _executor.ResumeAsync(context.ProcessId.Value);
            UpdateJobStatus(context, JobState.Encoding);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<EncodingJobStatus>> GetActiveJobsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EncodingJobStatus> activeJobs = _jobs.Values
            .Where(j => j.Status.State is JobState.Pending or JobState.Analyzing or JobState.Encoding or JobState.Processing or JobState.Paused)
            .Select(j => j.Status)
            .ToList();

        return Task.FromResult(activeJobs);
    }

    private async Task ProcessJobAsync(JobContext context)
    {
        DateTimeOffset startTime = DateTimeOffset.UtcNow;
        List<OutputFile> outputFiles = [];
        List<string> errors = [];
        List<string> warnings = [];
        MediaInfo? sourceInfo = null;

        try
        {
            await _concurrencyLimiter.WaitAsync(context.CancellationSource.Token);

            try
            {
                // Analyze source
                UpdateJobStatus(context, JobState.Analyzing, "Analyzing source file...");
                sourceInfo = await _analyzer.AnalyzeAsync(context.Job.InputPath, context.CancellationSource.Token);

                // Validate profile
                ValidationResult validationResult = context.Job.Profile.Validate();
                if (!validationResult.IsValid)
                {
                    errors.AddRange(validationResult.Errors);
                    throw new InvalidOperationException($"Profile validation failed: {string.Join(", ", validationResult.Errors)}");
                }
                warnings.AddRange(validationResult.Warnings);

                // Build and execute FFmpeg command
                UpdateJobStatus(context, JobState.Encoding, "Encoding...");

                FFmpegCommandBuilder commandBuilder = FFmpegCommandBuilder.FromProfile(
                    context.Job.InputPath,
                    context.Job.OutputPath,
                    context.Job.Profile,
                    sourceInfo);

                string command = commandBuilder.Build();

                Progress<EncodingProgress> progress = new(p =>
                {
                    context.Status = context.Status with
                    {
                        Progress = p.Percentage,
                        Elapsed = p.Elapsed,
                        EstimatedRemaining = p.Estimated
                    };
                    OnJobProgressChanged(context.Status);
                });

                string? workingDir = Path.GetDirectoryName(context.Job.OutputPath);
                FFmpegResult result = await _executor.ExecuteAsync(
                    command,
                    workingDir,
                    progress,
                    context.CancellationSource.Token);

                if (!result.Success)
                {
                    errors.Add($"FFmpeg failed with exit code {result.ExitCode}: {result.StandardError}");
                    throw new InvalidOperationException($"Encoding failed: {result.StandardError}");
                }

                // Collect output files
                UpdateJobStatus(context, JobState.Processing, "Finalizing...");
                outputFiles = await CollectOutputFilesAsync(context.Job.OutputPath, context.Job.Profile);

                // Complete successfully
                context.Status = context.Status with
                {
                    State = JobState.Completed,
                    Progress = 100,
                    CompletedAt = DateTimeOffset.UtcNow,
                    CompletedOutputs = outputFiles.Select(f => f.Path).ToList()
                };

                EncodingJobResult successResult = new()
                {
                    JobId = context.Job.Id,
                    Success = true,
                    OutputFiles = outputFiles,
                    Warnings = warnings,
                    TotalDuration = DateTimeOffset.UtcNow - startTime,
                    SourceInfo = sourceInfo
                };

                context.CompletionSource.SetResult(successResult);
                OnJobCompleted(successResult);
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        }
        catch (OperationCanceledException)
        {
            UpdateJobStatus(context, JobState.Cancelled);

            EncodingJobResult cancelledResult = new()
            {
                JobId = context.Job.Id,
                Success = false,
                Errors = ["Job was cancelled"],
                TotalDuration = DateTimeOffset.UtcNow - startTime,
                SourceInfo = sourceInfo
            };

            context.CompletionSource.SetResult(cancelledResult);
            OnJobCompleted(cancelledResult);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
            UpdateJobStatus(context, JobState.Failed, ex.Message);

            EncodingJobResult failedResult = new()
            {
                JobId = context.Job.Id,
                Success = false,
                Errors = errors,
                Warnings = warnings,
                TotalDuration = DateTimeOffset.UtcNow - startTime,
                SourceInfo = sourceInfo
            };

            context.CompletionSource.SetResult(failedResult);
            OnJobCompleted(failedResult);
        }
    }

    private static async Task<List<OutputFile>> CollectOutputFilesAsync(string outputPath, IEncodingProfile profile)
    {
        List<OutputFile> files = [];

        await Task.Run(() =>
        {
            if (File.Exists(outputPath))
            {
                FileInfo info = new(outputPath);
                files.Add(new OutputFile
                {
                    Path = outputPath,
                    Type = DetermineFileType(outputPath),
                    Size = info.Length
                });
            }
            else if (Directory.Exists(outputPath))
            {
                foreach (string file in Directory.EnumerateFiles(outputPath, "*", SearchOption.AllDirectories))
                {
                    FileInfo info = new(file);
                    files.Add(new OutputFile
                    {
                        Path = file,
                        Type = DetermineFileType(file),
                        Size = info.Length
                    });
                }
            }
        });

        return files;
    }

    private static string DetermineFileType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".m3u8" => "playlist",
            ".ts" => "video",
            ".mp4" => "video",
            ".mkv" => "video",
            ".webm" => "video",
            ".m4a" => "audio",
            ".mp3" => "audio",
            ".aac" => "audio",
            ".flac" => "audio",
            ".vtt" => "subtitle",
            ".srt" => "subtitle",
            ".jpg" or ".jpeg" or ".png" or ".webp" => "thumbnail",
            _ => "unknown"
        };
    }

    private void UpdateJobStatus(JobContext context, JobState state, string? step = null)
    {
        context.Status = context.Status with
        {
            State = state,
            CurrentStep = step,
            StartedAt = state == JobState.Analyzing ? DateTimeOffset.UtcNow : context.Status.StartedAt
        };
        OnJobProgressChanged(context.Status);
    }

    private void OnJobProgressChanged(EncodingJobStatus status)
    {
        JobProgressChanged?.Invoke(this, status);
    }

    private void OnJobCompleted(EncodingJobResult result)
    {
        JobCompleted?.Invoke(this, result);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
        _concurrencyLimiter.Dispose();

        foreach (JobContext context in _jobs.Values)
        {
            context.CancellationSource.Dispose();
        }
    }

    private sealed class JobContext
    {
        public required EncodingJob Job { get; init; }
        public EncodingJobStatus Status { get; set; } = null!;
        public required CancellationTokenSource CancellationSource { get; init; }
        public TaskCompletionSource<EncodingJobResult> CompletionSource { get; } = new();
        public int? ProcessId { get; set; }
    }
}
