using NoMercy.Api.Controllers.Socket;
using NoMercy.Database.Models;
using NoMercy.EncoderV2.Workers;

namespace NoMercy.Server.services;

/// <summary>
/// Adapter that implements IEncodingProgressBroadcaster using IEncodingProgressHubService.
/// Bridges the EncoderV2 worker with the SignalR hub service.
/// </summary>
public class EncodingProgressBroadcasterAdapter : IEncodingProgressBroadcaster
{
    private readonly IEncodingProgressHubService _hubService;

    public EncodingProgressBroadcasterAdapter(IEncodingProgressHubService hubService)
    {
        _hubService = hubService;
    }

    public async Task SendTaskProgressAsync(Ulid jobId, Ulid taskId, EncodingProgress progress)
    {
        TaskProgressUpdate update = new()
        {
            JobId = jobId.ToString(),
            TaskId = taskId.ToString(),
            TaskType = string.Empty, // Not available from EncodingProgress
            ProgressPercentage = progress.ProgressPercentage,
            Fps = progress.Fps,
            Speed = progress.Speed,
            Bitrate = progress.Bitrate,
            CurrentTime = progress.CurrentTime,
            EncodedFrames = progress.EncodedFrames,
            EstimatedRemaining = progress.EstimatedRemaining,
            Timestamp = progress.RecordedAt
        };

        await _hubService.SendTaskProgressAsync(update);
    }

    public async Task SendTaskStateChangeAsync(Ulid jobId, Ulid taskId, string previousState, string newState, string taskType, string? errorMessage)
    {
        TaskStateChange stateChange = new()
        {
            JobId = jobId.ToString(),
            TaskId = taskId.ToString(),
            TaskType = taskType,
            PreviousState = previousState,
            NewState = newState,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        };

        await _hubService.SendTaskStateChangeAsync(stateChange);
    }

    public async Task SendJobStateChangeAsync(Ulid jobId, string previousState, string newState, string? errorMessage)
    {
        JobStateChange stateChange = new()
        {
            JobId = jobId.ToString(),
            PreviousState = previousState,
            NewState = newState,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        };

        await _hubService.SendJobStateChangeAsync(stateChange);
    }

    public async Task SendNodeStatusChangeAsync(Ulid nodeId, bool isHealthy, bool isEnabled, DateTime? lastHeartbeat)
    {
        NodeStatusChange statusChange = new()
        {
            NodeId = nodeId.ToString(),
            NodeName = string.Empty, // Would need to lookup
            IsHealthy = isHealthy,
            IsEnabled = isEnabled,
            LastHeartbeat = lastHeartbeat,
            Timestamp = DateTime.UtcNow
        };

        await _hubService.SendNodeStatusChangeAsync(statusChange);
    }

    public async Task SendJobCreatedAsync(EncodingJob job)
    {
        EncodingJobStatusDto dto = MapJobToDto(job);
        await _hubService.SendJobCreatedAsync(dto);
    }

    public async Task SendJobRemovedAsync(Ulid jobId, string reason)
    {
        await _hubService.SendJobRemovedAsync(jobId.ToString(), reason);
    }

    private static EncodingJobStatusDto MapJobToDto(EncodingJob job)
    {
        List<EncodingTask> tasks = job.Tasks?.ToList() ?? [];
        int totalTasks = tasks.Count;
        int completedTasks = tasks.Count(t => t.State == EncodingTaskState.Completed);
        int runningTasks = tasks.Count(t => t.State == EncodingTaskState.Running);
        int failedTasks = tasks.Count(t => t.State == EncodingTaskState.Failed);
        int pendingTasks = tasks.Count(t => t.State == EncodingTaskState.Pending);

        double overallProgress = totalTasks > 0
            ? (double)completedTasks / totalTasks * 100
            : 0;

        return new EncodingJobStatusDto
        {
            Id = job.Id.ToString(),
            Title = job.Title ?? Path.GetFileNameWithoutExtension(job.InputFilePath),
            InputFilePath = job.InputFilePath,
            OutputFolder = job.OutputFolder,
            State = job.State,
            Priority = job.Priority,
            ErrorMessage = job.ErrorMessage,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            TotalTasks = totalTasks,
            CompletedTasks = completedTasks,
            RunningTasks = runningTasks,
            FailedTasks = failedTasks,
            PendingTasks = pendingTasks,
            OverallProgress = overallProgress,
            Tasks = tasks.Select(t => new EncodingTaskStatusDto
            {
                Id = t.Id.ToString(),
                TaskType = t.TaskType,
                State = t.State,
                Weight = t.Weight,
                RetryCount = t.RetryCount,
                MaxRetries = t.MaxRetries,
                AssignedNodeId = t.AssignedNodeId?.ToString(),
                ErrorMessage = t.ErrorMessage,
                StartedAt = t.StartedAt,
                CompletedAt = t.CompletedAt
            }).ToList()
        };
    }
}
