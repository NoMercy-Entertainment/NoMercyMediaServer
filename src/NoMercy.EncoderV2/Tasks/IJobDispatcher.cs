using NoMercy.Database.Models;

namespace NoMercy.EncoderV2.Tasks;

/// <summary>
/// Priority levels for encoding jobs
/// </summary>
public enum JobPriority
{
    /// <summary>
    /// Lowest priority, processed when no higher priority jobs exist
    /// </summary>
    Low = -10,

    /// <summary>
    /// Normal priority (default)
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Higher priority, processed before normal jobs
    /// </summary>
    High = 10,

    /// <summary>
    /// Highest priority, processed immediately
    /// </summary>
    Urgent = 100
}

/// <summary>
/// Options for dispatching encoding jobs
/// </summary>
public class JobDispatchOptions
{
    /// <summary>
    /// Job priority level
    /// </summary>
    public JobPriority Priority { get; set; } = JobPriority.Normal;

    /// <summary>
    /// Task splitting strategy to use
    /// </summary>
    public TaskDistributionStrategy SplitStrategy { get; set; } = TaskDistributionStrategy.Optimal;

    /// <summary>
    /// Node selection strategy to use
    /// </summary>
    public NodeSelectionStrategy NodeStrategy { get; set; } = NodeSelectionStrategy.Auto;

    /// <summary>
    /// Whether to assign tasks to nodes immediately or defer until execution
    /// </summary>
    public bool AssignNodesImmediately { get; set; } = false;

    /// <summary>
    /// Whether to include post-processing tasks (fonts, chapters, sprites)
    /// </summary>
    public bool IncludePostProcessing { get; set; } = true;

    /// <summary>
    /// Whether to include validation as a final task
    /// </summary>
    public bool IncludeValidation { get; set; } = true;

    /// <summary>
    /// Custom title for the job (null to use input filename)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Maximum number of retries for failed tasks
    /// </summary>
    public int MaxTaskRetries { get; set; } = 3;
}

/// <summary>
/// Result of dispatching an encoding job
/// </summary>
public class JobDispatchResult
{
    /// <summary>
    /// Whether the job was successfully dispatched
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The created encoding job (null if dispatch failed)
    /// </summary>
    public EncodingJob? Job { get; set; }

    /// <summary>
    /// List of created tasks
    /// </summary>
    public List<EncodingTask> Tasks { get; set; } = [];

    /// <summary>
    /// Error message if dispatch failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Total estimated weight of all tasks
    /// </summary>
    public double TotalWeight { get; set; }

    /// <summary>
    /// Number of tasks that can run in parallel
    /// </summary>
    public int MaxParallelism { get; set; }

    /// <summary>
    /// Tasks that could not be assigned to nodes (when immediate assignment requested)
    /// </summary>
    public List<UnassignedTask> UnassignedTasks { get; set; } = [];
}

/// <summary>
/// Result of cancelling an encoding job
/// </summary>
public class JobCancelResult
{
    /// <summary>
    /// Whether the cancellation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of tasks that were cancelled
    /// </summary>
    public int CancelledTaskCount { get; set; }

    /// <summary>
    /// Number of tasks that were already completed
    /// </summary>
    public int AlreadyCompletedCount { get; set; }

    /// <summary>
    /// Error message if cancellation failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of retrying failed tasks
/// </summary>
public class TaskRetryResult
{
    /// <summary>
    /// Whether retry was initiated successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of tasks queued for retry
    /// </summary>
    public int RetriedCount { get; set; }

    /// <summary>
    /// Tasks that exceeded max retries
    /// </summary>
    public List<EncodingTask> ExceededMaxRetries { get; set; } = [];

    /// <summary>
    /// Error message if retry failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Status of an encoding job with aggregated progress
/// </summary>
public class JobStatus
{
    /// <summary>
    /// The encoding job
    /// </summary>
    public EncodingJob Job { get; set; } = null!;

    /// <summary>
    /// Overall progress percentage (0-100)
    /// </summary>
    public double OverallProgress { get; set; }

    /// <summary>
    /// Number of pending tasks
    /// </summary>
    public int PendingTasks { get; set; }

    /// <summary>
    /// Number of running tasks
    /// </summary>
    public int RunningTasks { get; set; }

    /// <summary>
    /// Number of completed tasks
    /// </summary>
    public int CompletedTasks { get; set; }

    /// <summary>
    /// Number of failed tasks
    /// </summary>
    public int FailedTasks { get; set; }

    /// <summary>
    /// Total number of tasks
    /// </summary>
    public int TotalTasks { get; set; }

    /// <summary>
    /// Estimated time remaining based on current progress
    /// </summary>
    public TimeSpan? EstimatedRemaining { get; set; }

    /// <summary>
    /// Current task being processed (if any)
    /// </summary>
    public EncodingTask? CurrentTask { get; set; }

    /// <summary>
    /// Latest progress update (if any)
    /// </summary>
    public EncodingProgress? LatestProgress { get; set; }
}

/// <summary>
/// Service for dispatching encoding jobs to the queue system.
/// Integrates with TaskSplitter for job decomposition and NodeSelector for node assignment.
/// </summary>
public interface IJobDispatcher
{
    /// <summary>
    /// Dispatch a new encoding job
    /// </summary>
    /// <param name="inputFilePath">Path to the source media file</param>
    /// <param name="outputFolder">Path to the output directory</param>
    /// <param name="profileId">Profile ID to use (null for default profile)</param>
    /// <param name="options">Dispatch options (null for defaults)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dispatch result with job and task information</returns>
    Task<JobDispatchResult> DispatchAsync(
        string inputFilePath,
        string outputFolder,
        Ulid? profileId = null,
        JobDispatchOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatch a new encoding job with an existing profile object
    /// </summary>
    /// <param name="inputFilePath">Path to the source media file</param>
    /// <param name="outputFolder">Path to the output directory</param>
    /// <param name="profile">Encoding profile to use</param>
    /// <param name="options">Dispatch options (null for defaults)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dispatch result with job and task information</returns>
    Task<JobDispatchResult> DispatchAsync(
        string inputFilePath,
        string outputFolder,
        EncoderProfile profile,
        JobDispatchOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel an encoding job and all its pending tasks
    /// </summary>
    /// <param name="jobId">Job ID to cancel</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cancellation result</returns>
    Task<JobCancelResult> CancelJobAsync(
        Ulid jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retry failed tasks for a job
    /// </summary>
    /// <param name="jobId">Job ID to retry tasks for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Retry result</returns>
    Task<TaskRetryResult> RetryFailedTasksAsync(
        Ulid jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current status of an encoding job
    /// </summary>
    /// <param name="jobId">Job ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job status with progress information</returns>
    Task<JobStatus?> GetJobStatusAsync(
        Ulid jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all jobs with a specific state
    /// </summary>
    /// <param name="state">Job state to filter by (null for all jobs)</param>
    /// <param name="limit">Maximum number of jobs to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of jobs matching the filter</returns>
    Task<List<EncodingJob>> GetJobsAsync(
        string? state = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the next pending task that can be executed
    /// </summary>
    /// <param name="nodeId">Node ID requesting the task (null for any node)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Next available task, or null if none available</returns>
    Task<EncodingTask?> GetNextPendingTaskAsync(
        Ulid? nodeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assign a task to a specific node
    /// </summary>
    /// <param name="taskId">Task ID to assign</param>
    /// <param name="nodeId">Node ID to assign to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if assignment was successful</returns>
    Task<bool> AssignTaskToNodeAsync(
        Ulid taskId,
        Ulid nodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a task as started
    /// </summary>
    /// <param name="taskId">Task ID to start</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if task was started successfully</returns>
    Task<bool> StartTaskAsync(
        Ulid taskId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a task as completed
    /// </summary>
    /// <param name="taskId">Task ID to complete</param>
    /// <param name="outputFile">Path to the output file (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if task was completed successfully</returns>
    Task<bool> CompleteTaskAsync(
        Ulid taskId,
        string? outputFile = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a task as failed
    /// </summary>
    /// <param name="taskId">Task ID that failed</param>
    /// <param name="errorMessage">Error message describing the failure</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if task was marked as failed</returns>
    Task<bool> FailTaskAsync(
        Ulid taskId,
        string errorMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Record progress for a running task
    /// </summary>
    /// <param name="taskId">Task ID to update</param>
    /// <param name="progress">Progress snapshot to record</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if progress was recorded</returns>
    Task<bool> RecordProgressAsync(
        Ulid taskId,
        EncodingProgress progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reassign tasks from an unhealthy node to other available nodes
    /// </summary>
    /// <param name="nodeId">Node ID to reassign tasks from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tasks reassigned</returns>
    Task<int> ReassignTasksFromNodeAsync(
        Ulid nodeId,
        CancellationToken cancellationToken = default);
}
