using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Networking;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.Controllers.Socket;

/// <summary>
/// SignalR hub for real-time encoding progress updates.
/// Provides job and task state changes, progress metrics, and node status updates.
/// </summary>
public class EncodingProgressHub : ConnectionHub
{
    private const string JobGroupPrefix = "encoding-job-";
    private const string AllJobsGroup = "encoding-all-jobs";
    private const string NodesGroup = "encoding-nodes";

    public EncodingProgressHub(IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor)
    {
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        Logger.Socket("Encoding progress client connected");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        Logger.Socket("Encoding progress client disconnected");
    }

    #region Client Subscription Methods

    /// <summary>
    /// Subscribe to progress updates for a specific encoding job.
    /// </summary>
    public async Task SubscribeToJob(string jobId)
    {
        string groupName = $"{JobGroupPrefix}{jobId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        Logger.Socket($"Client subscribed to job: {jobId}");
    }

    /// <summary>
    /// Unsubscribe from progress updates for a specific encoding job.
    /// </summary>
    public async Task UnsubscribeFromJob(string jobId)
    {
        string groupName = $"{JobGroupPrefix}{jobId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        Logger.Socket($"Client unsubscribed from job: {jobId}");
    }

    /// <summary>
    /// Subscribe to updates for all encoding jobs.
    /// </summary>
    public async Task SubscribeToAllJobs()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AllJobsGroup);
        Logger.Socket("Client subscribed to all encoding jobs");
    }

    /// <summary>
    /// Unsubscribe from all encoding job updates.
    /// </summary>
    public async Task UnsubscribeFromAllJobs()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AllJobsGroup);
        Logger.Socket("Client unsubscribed from all encoding jobs");
    }

    /// <summary>
    /// Subscribe to encoder node status updates.
    /// </summary>
    public async Task SubscribeToNodes()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, NodesGroup);
        Logger.Socket("Client subscribed to encoder node updates");
    }

    /// <summary>
    /// Unsubscribe from encoder node status updates.
    /// </summary>
    public async Task UnsubscribeFromNodes()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, NodesGroup);
        Logger.Socket("Client unsubscribed from encoder node updates");
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Get current status of a specific encoding job.
    /// </summary>
    public async Task<EncodingJobStatusDto?> GetJobStatus(string jobId)
    {
        if (!Ulid.TryParse(jobId, out Ulid jobUlid))
        {
            return null;
        }

        await using QueueContext context = new();
        EncodingJob? job = await context.EncodingJobs
            .Include(j => j.Tasks)
            .FirstOrDefaultAsync(j => j.Id == jobUlid);

        if (job is null)
        {
            return null;
        }

        return MapJobToDto(job);
    }

    /// <summary>
    /// Get current status of all active encoding jobs.
    /// </summary>
    public async Task<List<EncodingJobStatusDto>> GetActiveJobs()
    {
        await using QueueContext context = new();
        List<EncodingJob> jobs = await context.EncodingJobs
            .Include(j => j.Tasks)
            .Where(j => j.State == EncodingJobState.Queued || j.State == EncodingJobState.Encoding)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .ToListAsync();

        return jobs.Select(MapJobToDto).ToList();
    }

    /// <summary>
    /// Get current status of all encoder nodes.
    /// </summary>
    public async Task<List<EncoderNodeStatusDto>> GetNodeStatuses()
    {
        await using QueueContext context = new();
        List<EncoderNode> nodes = await context.EncoderNodes.ToListAsync();

        List<EncoderNodeStatusDto> result = [];
        foreach (EncoderNode node in nodes)
        {
            int runningTasks = await context.EncodingTasks
                .CountAsync(t => t.AssignedNodeId == node.Id && t.State == EncodingTaskState.Running);

            result.Add(new EncoderNodeStatusDto
            {
                Id = node.Id.ToString(),
                Name = node.Name,
                IpAddress = node.IpAddress,
                Port = node.Port,
                IsEnabled = node.IsEnabled,
                IsHealthy = node.IsHealthy,
                HasGpu = node.HasGpu,
                GpuModel = node.GpuModel,
                CpuCores = node.CpuCores,
                MemoryGb = node.MemoryGb,
                LastHeartbeat = node.LastHeartbeat,
                RunningTasks = runningTasks,
                MaxConcurrentTasks = node.MaxConcurrentTasks
            });
        }

        return result;
    }

    #endregion

    #region Helper Methods

    private static EncodingJobStatusDto MapJobToDto(EncodingJob job)
    {
        int totalTasks = job.Tasks.Count;
        int completedTasks = job.Tasks.Count(t => t.State == EncodingTaskState.Completed);
        int runningTasks = job.Tasks.Count(t => t.State == EncodingTaskState.Running);
        int failedTasks = job.Tasks.Count(t => t.State == EncodingTaskState.Failed);
        int pendingTasks = job.Tasks.Count(t => t.State == EncodingTaskState.Pending);

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
            Tasks = job.Tasks.Select(t => new EncodingTaskStatusDto
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

    #endregion
}

#region DTOs for SignalR Messages

/// <summary>
/// Progress update for an encoding task.
/// </summary>
public class TaskProgressUpdate
{
    public string JobId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public double ProgressPercentage { get; set; }
    public double? Fps { get; set; }
    public double? Speed { get; set; }
    public string? Bitrate { get; set; }
    public TimeSpan? CurrentTime { get; set; }
    public TimeSpan? TotalDuration { get; set; }
    public TimeSpan? EstimatedRemaining { get; set; }
    public long? EncodedFrames { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Job state change notification.
/// </summary>
public class JobStateChange
{
    public string JobId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string PreviousState { get; set; } = string.Empty;
    public string NewState { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Task state change notification.
/// </summary>
public class TaskStateChange
{
    public string JobId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string PreviousState { get; set; } = string.Empty;
    public string NewState { get; set; } = string.Empty;
    public string? AssignedNodeId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Encoder node status change notification.
/// </summary>
public class NodeStatusChange
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Complete status of an encoding job.
/// </summary>
public class EncodingJobStatusDto
{
    public string Id { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string InputFilePath { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int RunningTasks { get; set; }
    public int FailedTasks { get; set; }
    public int PendingTasks { get; set; }
    public double OverallProgress { get; set; }
    public List<EncodingTaskStatusDto> Tasks { get; set; } = [];
}

/// <summary>
/// Status of an individual encoding task.
/// </summary>
public class EncodingTaskStatusDto
{
    public string Id { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int Weight { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public string? AssignedNodeId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Status of an encoder node.
/// </summary>
public class EncoderNodeStatusDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsHealthy { get; set; }
    public bool HasGpu { get; set; }
    public string? GpuModel { get; set; }
    public int CpuCores { get; set; }
    public double MemoryGb { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public int RunningTasks { get; set; }
    public int MaxConcurrentTasks { get; set; }
}

#endregion
