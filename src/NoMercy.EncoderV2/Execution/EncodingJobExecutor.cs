using System.Text.Json;
using NoMercy.Database.Models;
using NoMercy.EncoderV2.FFmpeg;
using NoMercy.EncoderV2.Hardware;
using NoMercy.EncoderV2.Profiles;
using NoMercy.EncoderV2.Progress;
using NoMercy.EncoderV2.Repositories;
using NoMercy.EncoderV2.Streams;
using NoMercy.EncoderV2.Tasks;
using NoMercy.NmSystem.Capabilities;

namespace NoMercy.EncoderV2.Execution;

/// <summary>
/// Executes encoding jobs end-to-end
/// Orchestrates the full pipeline: analysis → task splitting → FFmpeg execution → progress tracking
/// </summary>
public class EncodingJobExecutor(
    IJobRepository jobRepository,
    IProfileRepository profileRepository,
    IStreamAnalyzer streamAnalyzer,
    ITaskSplitter taskSplitter,
    IHardwareAccelerationService hardwareService,
    IFFmpegService ffmpegService,
    IProgressMonitor progressMonitor,
    ICodecSelector codecSelector) : IEncodingJobExecutor
{
    public async Task<EncodingJob> CreateJobAsync(string inputFile, string outputFolder, Ulid profileId)
    {
        if (!File.Exists(inputFile))
        {
            throw new FileNotFoundException($"Input file not found: {inputFile}");
        }

        EncoderProfile? profile = await profileRepository.GetProfileAsync(profileId);
        if (profile == null)
        {
            throw new InvalidOperationException($"Profile not found: {profileId}");
        }

        StreamAnalysis analysis = await streamAnalyzer.AnalyzeAsync(inputFile);

        EncodingJob job = new()
        {
            Id = Guid.NewGuid().ToString(),
            InputFilePath = inputFile,
            OutputFolder = outputFolder,
            ProfileId = profileId,
            ProfileSnapshotJson = JsonSerializer.Serialize(profile),
            State = "queued",
            CreatedAt = DateTime.UtcNow
        };

        List<EncodingTaskDefinition> taskDefinitions = taskSplitter.SplitJob(
            analysis,
            profile,
            TaskDistributionStrategy.SingleTask
        );

        foreach (EncodingTaskDefinition taskDef in taskDefinitions)
        {
            EncodingTask task = new()
            {
                Id = Guid.NewGuid().ToString(),
                JobId = job.Id,
                TaskType = taskDef.TaskType,
                Weight = taskDef.Weight,
                State = "pending",
                DependenciesJson = JsonSerializer.Serialize(taskDef.Dependencies),
                CreatedAt = DateTime.UtcNow
            };

            job.Tasks.Add(task);
        }

        return await jobRepository.CreateJobAsync(job);
    }

    public async Task<bool> ExecuteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        EncodingJob? job = await jobRepository.GetJobAsync(jobId);
        if (job == null)
        {
            return false;
        }

        if (job.State != "queued")
        {
            return false;
        }

        try
        {
            job.State = "processing";
            job.StartedAt = DateTime.UtcNow;
            await jobRepository.UpdateJobAsync(job);

            EncoderProfile? profile = job.Profile ?? JsonSerializer.Deserialize<EncoderProfile>(job.ProfileSnapshotJson);
            if (profile == null)
            {
                throw new InvalidOperationException("Profile snapshot is invalid");
            }

            StreamAnalysis analysis = await streamAnalyzer.AnalyzeAsync(job.InputFilePath, cancellationToken);

            List<GpuAccelerator> accelerators = hardwareService.GetAvailableAccelerators();

            FFmpegCommandBuilder commandBuilder = new(
                analysis,
                profile,
                accelerators,
                job.InputFilePath,
                job.OutputFolder,
                codecSelector
            );

            string command = commandBuilder.BuildCommand();

            foreach (EncodingTask task in job.Tasks)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    job.State = "cancelled";
                    await jobRepository.UpdateJobAsync(job);
                    return false;
                }

                task.State = "processing";
                task.StartedAt = DateTime.UtcNow;
                await jobRepository.UpdateTaskAsync(task);

                FFmpegExecutionResult result = await ffmpegService.ExecuteAsync(
                    command,
                    job.OutputFolder,
                    progressOutput =>
                    {
                        EncodingProgressInfo? progressInfo = progressMonitor.ParseProgressOutput(progressOutput, analysis.Duration);
                        if (progressInfo != null)
                        {
                            progressMonitor.ReportProgressAsync(task.Id, progressInfo).Wait();
                        }
                    },
                    cancellationToken
                );

                if (result.ExitCode == 0)
                {
                    task.State = "completed";
                    task.CompletedAt = DateTime.UtcNow;
                }
                else
                {
                    task.State = "failed";
                    task.ErrorMessage = result.StandardError;
                    task.CompletedAt = DateTime.UtcNow;
                }

                await jobRepository.UpdateTaskAsync(task);

                if (result.ExitCode != 0)
                {
                    job.State = "failed";
                    job.ErrorMessage = $"Task {task.Id} failed: {task.ErrorMessage}";
                    job.CompletedAt = DateTime.UtcNow;
                    await jobRepository.UpdateJobAsync(job);
                    return false;
                }
            }

            job.State = "completed";
            job.CompletedAt = DateTime.UtcNow;
            job.ExecutionTimeMs = (long)(job.CompletedAt.Value - job.StartedAt!.Value).TotalMilliseconds;
            await jobRepository.UpdateJobAsync(job);

            return true;
        }
        catch (Exception ex)
        {
            job.State = "failed";
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            await jobRepository.UpdateJobAsync(job);
            return false;
        }
    }

    public async Task CancelJobAsync(string jobId)
    {
        EncodingJob? job = await jobRepository.GetJobAsync(jobId);
        if (job != null && (job.State == "queued" || job.State == "processing"))
        {
            job.State = "cancelled";
            job.CompletedAt = DateTime.UtcNow;
            await jobRepository.UpdateJobAsync(job);
        }
    }

    public async Task<EncodingJobStatus> GetJobStatusAsync(string jobId)
    {
        EncodingJob? job = await jobRepository.GetJobAsync(jobId);
        if (job == null)
        {
            throw new InvalidOperationException($"Job not found: {jobId}");
        }

        EncodingJobStatus status = new()
        {
            JobId = job.Id,
            State = job.State
        };

        foreach (EncodingTask task in job.Tasks)
        {
            List<EncodingProgress> progressList = await jobRepository.GetTaskProgressAsync(task.Id, 1);
            EncodingProgress? latestProgress = progressList.FirstOrDefault();

            status.Tasks.Add(new EncodingTaskStatus
            {
                TaskId = task.Id,
                TaskType = task.TaskType,
                State = task.State,
                Progress = latestProgress?.ProgressPercentage ?? 0,
                AssignedNodeName = task.AssignedNode?.NodeName
            });
        }

        if (status.Tasks.Count > 0)
        {
            status.OverallProgress = status.Tasks.Average(t => t.Progress);
            TimeSpan estimatedRemaining = TimeSpan.Zero;

            foreach (EncodingTask task in job.Tasks)
            {
                List<EncodingProgress> progressList = await jobRepository.GetTaskProgressAsync(task.Id, 1);
                EncodingProgress? latestProgress = progressList.FirstOrDefault();

                if (latestProgress != null && latestProgress.EstimatedRemaining > estimatedRemaining)
                {
                    estimatedRemaining = latestProgress.EstimatedRemaining;
                }
            }

            status.EstimatedRemaining = estimatedRemaining;
        }

        return status;
    }
}
