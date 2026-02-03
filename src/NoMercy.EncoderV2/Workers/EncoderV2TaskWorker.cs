using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.EncoderV2.PostProcessing;
using NoMercy.EncoderV2.Processing;
using NoMercy.EncoderV2.Tasks;
using NoMercy.EncoderV2.Validation;

namespace NoMercy.EncoderV2.Workers;

/// <summary>
/// Background service that processes EncoderV2 encoding tasks from the QueueContext database.
/// Integrates with the existing NoMercy.Queue system by polling EncodingTask records.
/// </summary>
public class EncoderV2TaskWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EncoderV2TaskWorker> _logger;
    private readonly EncoderV2WorkerOptions _options;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly List<Task> _runningTasks = [];

    public EncoderV2TaskWorker(
        IServiceProvider serviceProvider,
        ILogger<EncoderV2TaskWorker> logger,
        EncoderV2WorkerOptions options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
        _concurrencySemaphore = new SemaphoreSlim(options.MaxConcurrentTasks, options.MaxConcurrentTasks);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EncoderV2TaskWorker starting with max {MaxTasks} concurrent tasks", _options.MaxConcurrentTasks);

        // Wait a bit before starting to allow other services to initialize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Clean up completed tasks from the list
                _runningTasks.RemoveAll(t => t.IsCompleted);

                // Try to get and process a task if we have capacity
                if (_concurrencySemaphore.CurrentCount > 0)
                {
                    await TryProcessNextTaskAsync(stoppingToken);
                }

                // Wait before polling again
                await Task.Delay(TimeSpan.FromMilliseconds(_options.PollingIntervalMs), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EncoderV2TaskWorker main loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        // Wait for running tasks to complete
        if (_runningTasks.Count > 0)
        {
            _logger.LogInformation("Waiting for {Count} running tasks to complete", _runningTasks.Count);
            await Task.WhenAll(_runningTasks);
        }

        _logger.LogInformation("EncoderV2TaskWorker stopped");
    }

    private async Task TryProcessNextTaskAsync(CancellationToken stoppingToken)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IJobDispatcher jobDispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();

        // Get the next pending task (local node = null means this server)
        EncodingTask? task = await jobDispatcher.GetNextPendingTaskAsync(nodeId: null, stoppingToken);

        if (task == null)
        {
            return;
        }

        // Try to acquire the semaphore
        if (!await _concurrencySemaphore.WaitAsync(0, stoppingToken))
        {
            return;
        }

        _logger.LogInformation("Starting task {TaskId} ({TaskType}) for job {JobId}",
            task.Id, task.TaskType, task.JobId);

        // Start the task execution in background
        Task executionTask = Task.Run(async () =>
        {
            try
            {
                await ExecuteTaskAsync(task.Id, stoppingToken);
            }
            finally
            {
                _concurrencySemaphore.Release();
            }
        }, stoppingToken);

        _runningTasks.Add(executionTask);
    }

    private async Task ExecuteTaskAsync(Ulid taskId, CancellationToken stoppingToken)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        QueueContext queueContext = scope.ServiceProvider.GetRequiredService<QueueContext>();
        IJobDispatcher jobDispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();

        // Re-fetch the task within this scope
        EncodingTask? task = await queueContext.EncodingTasks
            .Include(t => t.Job)
            .FirstOrDefaultAsync(t => t.Id == taskId, stoppingToken);

        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found when starting execution", taskId);
            return;
        }

        // Mark task as started
        bool started = await jobDispatcher.StartTaskAsync(taskId, stoppingToken);
        if (!started)
        {
            _logger.LogWarning("Failed to start task {TaskId}", taskId);
            return;
        }

        // Broadcast task state change
        await BroadcastTaskStateChangeAsync(scope, task, EncodingTaskState.Running, null);

        try
        {
            // Execute the task based on its type
            string? outputFile = await ExecuteTaskByTypeAsync(scope, task, stoppingToken);

            // Mark task as completed
            await jobDispatcher.CompleteTaskAsync(taskId, outputFile, stoppingToken);

            _logger.LogInformation("Task {TaskId} ({TaskType}) completed successfully", taskId, task.TaskType);

            // Broadcast completion
            await BroadcastTaskStateChangeAsync(scope, task, EncodingTaskState.Completed, null);

            // Check if job is now complete
            await CheckAndBroadcastJobCompletionAsync(scope, task.JobId, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Task {TaskId} was cancelled", taskId);
            await jobDispatcher.FailTaskAsync(taskId, "Task was cancelled", stoppingToken);
            await BroadcastTaskStateChangeAsync(scope, task, EncodingTaskState.Failed, "Task was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task {TaskId} ({TaskType}) failed", taskId, task.TaskType);
            await jobDispatcher.FailTaskAsync(taskId, ex.Message, stoppingToken);
            await BroadcastTaskStateChangeAsync(scope, task, EncodingTaskState.Failed, ex.Message);
        }
    }

    private async Task<string?> ExecuteTaskByTypeAsync(
        IServiceScope scope,
        EncodingTask task,
        CancellationToken stoppingToken)
    {
        IJobDispatcher jobDispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();

        // Get the executor if available
        IEncodingTaskExecutor? executor = scope.ServiceProvider.GetService<IEncodingTaskExecutor>();

        if (executor != null)
        {
            // Use the full task executor
            return await executor.ExecuteAsync(task, async progress =>
            {
                // Record progress
                await jobDispatcher.RecordProgressAsync(task.Id, progress, stoppingToken);

                // Broadcast progress update
                await BroadcastTaskProgressAsync(scope, task, progress);
            }, stoppingToken);
        }

        // Fallback: execute based on task type using simple implementations
        return task.TaskType switch
        {
            EncodingTaskType.VideoEncoding => await ExecuteVideoEncodingAsync(scope, task, stoppingToken),
            EncodingTaskType.AudioEncoding => await ExecuteAudioEncodingAsync(scope, task, stoppingToken),
            EncodingTaskType.SubtitleExtraction => await ExecuteSubtitleExtractionAsync(scope, task, stoppingToken),
            EncodingTaskType.FontExtraction => await ExecuteFontExtractionAsync(scope, task, stoppingToken),
            EncodingTaskType.ChapterExtraction => await ExecuteChapterExtractionAsync(scope, task, stoppingToken),
            EncodingTaskType.SpriteGeneration => await ExecuteSpriteGenerationAsync(scope, task, stoppingToken),
            EncodingTaskType.HdrConversion => await ExecuteHdrConversionAsync(scope, task, stoppingToken),
            EncodingTaskType.PlaylistGeneration => await ExecutePlaylistGenerationAsync(scope, task, stoppingToken),
            EncodingTaskType.Validation => await ExecuteValidationAsync(scope, task, stoppingToken),
            _ => throw new NotSupportedException($"Task type {task.TaskType} is not supported")
        };
    }

    private async Task<string?> ExecuteVideoEncodingAsync(IServiceScope scope, EncodingTask task, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Executing video encoding for task {TaskId}", task.Id);
        // TODO: Implement actual video encoding using FFmpegExecutor
        await Task.Delay(100, stoppingToken); // Placeholder
        return task.OutputFile;
    }

    private async Task<string?> ExecuteAudioEncodingAsync(IServiceScope scope, EncodingTask task, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Executing audio encoding for task {TaskId}", task.Id);
        // TODO: Implement actual audio encoding
        await Task.Delay(100, stoppingToken);
        return task.OutputFile;
    }

    private async Task<string?> ExecuteSubtitleExtractionAsync(IServiceScope scope, EncodingTask task, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Executing subtitle extraction for task {TaskId}", task.Id);
        // TODO: Implement using SubtitleStreamProcessor
        await Task.Delay(100, stoppingToken);
        return task.OutputFile;
    }

    private async Task<string?> ExecuteFontExtractionAsync(IServiceScope scope, EncodingTask task, CancellationToken stoppingToken)
    {
        IFontExtractor? fontExtractor = scope.ServiceProvider.GetService<IFontExtractor>();
        if (fontExtractor == null)
        {
            _logger.LogWarning("FontExtractor not available for task {TaskId}", task.Id);
            return null;
        }

        _logger.LogInformation("Executing font extraction for task {TaskId}", task.Id);
        FontExtractionResult result = await fontExtractor.ExtractFontsAsync(
            task.Job.InputFilePath,
            task.Job.OutputFolder,
            stoppingToken);

        return result.Success ? result.OutputDirectory : null;
    }

    private async Task<string?> ExecuteChapterExtractionAsync(IServiceScope scope, EncodingTask task, CancellationToken stoppingToken)
    {
        IChapterProcessor? chapterProcessor = scope.ServiceProvider.GetService<IChapterProcessor>();
        if (chapterProcessor == null)
        {
            _logger.LogWarning("ChapterProcessor not available for task {TaskId}", task.Id);
            return null;
        }

        _logger.LogInformation("Executing chapter extraction for task {TaskId}", task.Id);
        ChapterExtractionResult result = await chapterProcessor.ExtractChaptersAsync(
            task.Job.InputFilePath,
            task.Job.OutputFolder,
            stoppingToken);

        return result.Success ? result.OutputPath : null;
    }

    private async Task<string?> ExecuteSpriteGenerationAsync(IServiceScope scope, EncodingTask task, CancellationToken stoppingToken)
    {
        ISpriteGenerator? spriteGenerator = scope.ServiceProvider.GetService<ISpriteGenerator>();
        if (spriteGenerator == null)
        {
            _logger.LogWarning("SpriteGenerator not available for task {TaskId}", task.Id);
            return null;
        }

        _logger.LogInformation("Executing sprite generation for task {TaskId}", task.Id);
        SpriteGenerationResult result = await spriteGenerator.GenerateSpriteAsync(
            task.Job.InputFilePath,
            task.Job.OutputFolder,
            cancellationToken: stoppingToken);

        return result.Success ? result.SpriteFilePath : null;
    }

    private async Task<string?> ExecuteHdrConversionAsync(IServiceScope scope, EncodingTask task, CancellationToken stoppingToken)
    {
        IHdrProcessor? hdrProcessor = scope.ServiceProvider.GetService<IHdrProcessor>();
        if (hdrProcessor == null)
        {
            _logger.LogWarning("HdrProcessor not available for task {TaskId}", task.Id);
            return null;
        }

        _logger.LogInformation("Executing HDR conversion for task {TaskId}", task.Id);
        HdrConversionResult result = await hdrProcessor.ConvertToSdrAsync(
            task.Job.InputFilePath,
            task.Job.OutputFolder,
            cancellationToken: stoppingToken);

        return result.Success ? result.OutputPath : null;
    }

    private async Task<string?> ExecutePlaylistGenerationAsync(IServiceScope scope, EncodingTask task, CancellationToken stoppingToken)
    {
        IPostProcessor? postProcessor = scope.ServiceProvider.GetService<IPostProcessor>();
        if (postProcessor == null)
        {
            _logger.LogWarning("PostProcessor not available for task {TaskId}", task.Id);
            return null;
        }

        _logger.LogInformation("Executing playlist generation for task {TaskId}", task.Id);
        // Generate master playlist
        await Task.Delay(100, stoppingToken); // Placeholder
        return Path.Combine(task.Job.OutputFolder, "master.m3u8");
    }

    private async Task<string?> ExecuteValidationAsync(IServiceScope scope, EncodingTask task, CancellationToken stoppingToken)
    {
        IOutputValidator? validator = scope.ServiceProvider.GetService<IOutputValidator>();
        if (validator == null)
        {
            _logger.LogWarning("OutputValidator not available for task {TaskId}", task.Id);
            return null;
        }

        _logger.LogInformation("Executing output validation for task {TaskId}", task.Id);

        // Find output files to validate
        string[] outputFiles = Directory.GetFiles(task.Job.OutputFolder, "*.m3u8", SearchOption.AllDirectories);
        List<string> errors = [];

        foreach (string playlist in outputFiles)
        {
            OutputValidationResult result = await validator.ValidatePlaylistAsync(playlist);
            if (!result.IsValid)
            {
                errors.AddRange(result.Errors);
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Validation failed: {string.Join(", ", errors)}");
        }

        return task.Job.OutputFolder;
    }

    private async Task BroadcastTaskProgressAsync(IServiceScope scope, EncodingTask task, EncodingProgress progress)
    {
        try
        {
            IEncodingProgressBroadcaster? broadcaster = scope.ServiceProvider.GetService<IEncodingProgressBroadcaster>();
            if (broadcaster != null)
            {
                await broadcaster.SendTaskProgressAsync(task.JobId, task.Id, progress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast task progress for {TaskId}", task.Id);
        }
    }

    private async Task BroadcastTaskStateChangeAsync(IServiceScope scope, EncodingTask task, string newState, string? error)
    {
        try
        {
            IEncodingProgressBroadcaster? broadcaster = scope.ServiceProvider.GetService<IEncodingProgressBroadcaster>();
            if (broadcaster != null)
            {
                await broadcaster.SendTaskStateChangeAsync(task.JobId, task.Id, task.State, newState, task.TaskType, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast task state change for {TaskId}", task.Id);
        }
    }

    private async Task CheckAndBroadcastJobCompletionAsync(IServiceScope scope, Ulid jobId, CancellationToken stoppingToken)
    {
        try
        {
            QueueContext queueContext = scope.ServiceProvider.GetRequiredService<QueueContext>();
            EncodingJob? job = await queueContext.EncodingJobs
                .Include(j => j.Tasks)
                .FirstOrDefaultAsync(j => j.Id == jobId, stoppingToken);

            if (job == null) return;

            // Check if job state changed to completed
            if (job.State == EncodingJobState.Completed || job.State == EncodingJobState.Failed)
            {
                IEncodingProgressBroadcaster? broadcaster = scope.ServiceProvider.GetService<IEncodingProgressBroadcaster>();
                if (broadcaster != null)
                {
                    await broadcaster.SendJobStateChangeAsync(jobId, EncodingJobState.Encoding, job.State, job.ErrorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check/broadcast job completion for {JobId}", jobId);
        }
    }
}

/// <summary>
/// Configuration options for EncoderV2TaskWorker
/// </summary>
public class EncoderV2WorkerOptions
{
    /// <summary>
    /// Maximum number of concurrent tasks to process (default: 1)
    /// </summary>
    public int MaxConcurrentTasks { get; set; } = 1;

    /// <summary>
    /// Polling interval in milliseconds (default: 1000)
    /// </summary>
    public int PollingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Whether this worker should process local tasks only (default: true)
    /// </summary>
    public bool LocalTasksOnly { get; set; } = true;
}
