using Microsoft.AspNetCore.SignalR;

namespace NoMercy.Api.Controllers.Socket;

/// <summary>
/// Service for broadcasting encoding progress updates to SignalR clients.
/// Inject this service into EncoderV2 components to send real-time updates.
/// </summary>
public interface IEncodingProgressHubService
{
    /// <summary>
    /// Broadcast task progress update to subscribed clients.
    /// </summary>
    Task SendTaskProgressAsync(TaskProgressUpdate progress);

    /// <summary>
    /// Broadcast job state change to subscribed clients.
    /// </summary>
    Task SendJobStateChangeAsync(JobStateChange stateChange);

    /// <summary>
    /// Broadcast task state change to subscribed clients.
    /// </summary>
    Task SendTaskStateChangeAsync(TaskStateChange stateChange);

    /// <summary>
    /// Broadcast node status change to subscribed clients.
    /// </summary>
    Task SendNodeStatusChangeAsync(NodeStatusChange statusChange);

    /// <summary>
    /// Broadcast a new job was created.
    /// </summary>
    Task SendJobCreatedAsync(EncodingJobStatusDto job);

    /// <summary>
    /// Broadcast a job was deleted or cancelled.
    /// </summary>
    Task SendJobRemovedAsync(string jobId, string reason);
}

/// <summary>
/// Implementation of the encoding progress hub service.
/// Uses IHubContext to send messages to clients without being inside the hub.
/// </summary>
public class EncodingProgressHubService : IEncodingProgressHubService
{
    private const string JobGroupPrefix = "encoding-job-";
    private const string AllJobsGroup = "encoding-all-jobs";
    private const string NodesGroup = "encoding-nodes";

    private readonly IHubContext<EncodingProgressHub> _hubContext;

    public EncodingProgressHubService(IHubContext<EncodingProgressHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendTaskProgressAsync(TaskProgressUpdate progress)
    {
        string jobGroup = $"{JobGroupPrefix}{progress.JobId}";

        // Send to job-specific subscribers
        await _hubContext.Clients.Group(jobGroup).SendAsync("TaskProgress", progress);

        // Send to all-jobs subscribers
        await _hubContext.Clients.Group(AllJobsGroup).SendAsync("TaskProgress", progress);
    }

    public async Task SendJobStateChangeAsync(JobStateChange stateChange)
    {
        string jobGroup = $"{JobGroupPrefix}{stateChange.JobId}";

        // Send to job-specific subscribers
        await _hubContext.Clients.Group(jobGroup).SendAsync("JobStateChanged", stateChange);

        // Send to all-jobs subscribers
        await _hubContext.Clients.Group(AllJobsGroup).SendAsync("JobStateChanged", stateChange);
    }

    public async Task SendTaskStateChangeAsync(TaskStateChange stateChange)
    {
        string jobGroup = $"{JobGroupPrefix}{stateChange.JobId}";

        // Send to job-specific subscribers
        await _hubContext.Clients.Group(jobGroup).SendAsync("TaskStateChanged", stateChange);

        // Send to all-jobs subscribers
        await _hubContext.Clients.Group(AllJobsGroup).SendAsync("TaskStateChanged", stateChange);
    }

    public async Task SendNodeStatusChangeAsync(NodeStatusChange statusChange)
    {
        // Send to node subscribers only
        await _hubContext.Clients.Group(NodesGroup).SendAsync("NodeStatusChanged", statusChange);

        // Also send to all-jobs subscribers since node changes affect job execution
        await _hubContext.Clients.Group(AllJobsGroup).SendAsync("NodeStatusChanged", statusChange);
    }

    public async Task SendJobCreatedAsync(EncodingJobStatusDto job)
    {
        // Send to all-jobs subscribers
        await _hubContext.Clients.Group(AllJobsGroup).SendAsync("JobCreated", job);
    }

    public async Task SendJobRemovedAsync(string jobId, string reason)
    {
        string jobGroup = $"{JobGroupPrefix}{jobId}";

        // Send to job-specific subscribers
        await _hubContext.Clients.Group(jobGroup).SendAsync("JobRemoved", new { jobId, reason });

        // Send to all-jobs subscribers
        await _hubContext.Clients.Group(AllJobsGroup).SendAsync("JobRemoved", new { jobId, reason });
    }
}
