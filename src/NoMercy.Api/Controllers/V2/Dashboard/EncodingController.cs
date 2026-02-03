using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Database.Models;
using NoMercy.EncoderV2.Tasks;
using NoMercy.Helpers;

namespace NoMercy.Api.Controllers.V2.Dashboard;

/// <summary>
/// Controller for managing EncoderV2 encoding jobs
/// Provides endpoints for job submission, cancellation, status tracking, and task management
/// </summary>
[ApiController]
[Tags("EncoderV2 Jobs")]
[ApiVersion(2.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/encoder/jobs", Order = 10)]
public class EncodingController(IJobDispatcher jobDispatcher) : BaseController
{
    /// <summary>
    /// Get all encoding jobs with optional state filter
    /// </summary>
    /// <param name="state">Filter by job state (queued, encoding, completed, failed, cancelled)</param>
    /// <param name="limit">Maximum number of jobs to return (default: 100)</param>
    /// <returns>List of encoding jobs</returns>
    [HttpGet]
    [ProducesResponseType(typeof(StatusResponseDto<List<EncodingJob>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Index([FromQuery] string? state = null, [FromQuery] int limit = 100)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoding jobs");

        List<EncodingJob> jobs = await jobDispatcher.GetJobsAsync(state, limit);

        return Ok(new StatusResponseDto<List<EncodingJob>>
        {
            Status = "ok",
            Data = jobs,
            Message = "Successfully retrieved encoding jobs."
        });
    }

    /// <summary>
    /// Get the status of a specific encoding job with progress information
    /// </summary>
    /// <param name="id">Job ID (ULID)</param>
    /// <returns>Job status with progress details</returns>
    [HttpGet]
    [Route("{id:ulid}")]
    [ProducesResponseType(typeof(StatusResponseDto<JobStatus>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Show(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoding jobs");

        JobStatus? status = await jobDispatcher.GetJobStatusAsync(id);

        if (status is null)
            return NotFoundResponse("Encoding job not found");

        return Ok(new StatusResponseDto<JobStatus>
        {
            Status = "ok",
            Data = status,
            Message = "Successfully retrieved encoding job status."
        });
    }

    /// <summary>
    /// Submit a new encoding job
    /// </summary>
    /// <param name="request">Job submission request</param>
    /// <returns>The dispatched job with task information</returns>
    [HttpPost]
    [ProducesResponseType(typeof(StatusResponseDto<JobDispatchResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Store([FromBody] SubmitJobRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to submit encoding jobs");

        if (string.IsNullOrWhiteSpace(request.InputFilePath))
            return BadRequestResponse("Input file path is required");

        if (string.IsNullOrWhiteSpace(request.OutputFolder))
            return BadRequestResponse("Output folder is required");

        try
        {
            JobDispatchOptions options = new()
            {
                Priority = request.Priority ?? JobPriority.Normal,
                SplitStrategy = request.SplitStrategy ?? TaskDistributionStrategy.Optimal,
                NodeStrategy = request.NodeStrategy ?? NodeSelectionStrategy.Auto,
                AssignNodesImmediately = request.AssignNodesImmediately ?? false,
                IncludePostProcessing = request.IncludePostProcessing ?? true,
                IncludeValidation = request.IncludeValidation ?? true,
                Title = request.Title,
                MaxTaskRetries = request.MaxTaskRetries ?? 3
            };

            JobDispatchResult result = await jobDispatcher.DispatchAsync(
                request.InputFilePath,
                request.OutputFolder,
                request.ProfileId,
                options);

            if (!result.Success)
            {
                return UnprocessableEntityResponse($"Failed to submit encoding job: {result.ErrorMessage}");
            }

            return Ok(new StatusResponseDto<JobDispatchResult>
            {
                Status = "ok",
                Data = result,
                Message = "Successfully submitted encoding job.",
                Args = [result.Job?.Title ?? request.InputFilePath]
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntityResponse($"Failed to submit encoding job: {e.Message}");
        }
    }

    /// <summary>
    /// Cancel an encoding job and all its pending tasks
    /// </summary>
    /// <param name="id">Job ID (ULID)</param>
    /// <returns>Cancellation result</returns>
    [HttpPost]
    [Route("{id:ulid}/cancel")]
    [ProducesResponseType(typeof(StatusResponseDto<JobCancelResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to cancel encoding jobs");

        JobStatus? status = await jobDispatcher.GetJobStatusAsync(id);

        if (status is null)
            return NotFoundResponse("Encoding job not found");

        try
        {
            JobCancelResult result = await jobDispatcher.CancelJobAsync(id);

            if (!result.Success)
            {
                return UnprocessableEntityResponse($"Failed to cancel encoding job: {result.ErrorMessage}");
            }

            return Ok(new StatusResponseDto<JobCancelResult>
            {
                Status = "ok",
                Data = result,
                Message = "Successfully cancelled encoding job.",
                Args = [status.Job.Title ?? status.Job.InputFilePath]
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntityResponse($"Failed to cancel encoding job: {e.Message}");
        }
    }

    /// <summary>
    /// Retry failed tasks for an encoding job
    /// </summary>
    /// <param name="id">Job ID (ULID)</param>
    /// <returns>Retry result</returns>
    [HttpPost]
    [Route("{id:ulid}/retry")]
    [ProducesResponseType(typeof(StatusResponseDto<TaskRetryResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Retry(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to retry encoding jobs");

        JobStatus? status = await jobDispatcher.GetJobStatusAsync(id);

        if (status is null)
            return NotFoundResponse("Encoding job not found");

        try
        {
            TaskRetryResult result = await jobDispatcher.RetryFailedTasksAsync(id);

            if (!result.Success)
            {
                return UnprocessableEntityResponse($"Failed to retry encoding job: {result.ErrorMessage}");
            }

            return Ok(new StatusResponseDto<TaskRetryResult>
            {
                Status = "ok",
                Data = result,
                Message = "Successfully queued tasks for retry.",
                Args = [result.RetriedCount.ToString()]
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntityResponse($"Failed to retry encoding job: {e.Message}");
        }
    }

    /// <summary>
    /// Get the next pending task for execution (used by encoder nodes)
    /// </summary>
    /// <param name="nodeId">Optional node ID requesting the task</param>
    /// <returns>The next available task, or null if none available</returns>
    [HttpGet]
    [Route("tasks/next")]
    [ProducesResponseType(typeof(StatusResponseDto<EncodingTask?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetNextTask([FromQuery] Ulid? nodeId = null)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to access encoding tasks");

        EncodingTask? task = await jobDispatcher.GetNextPendingTaskAsync(nodeId);

        return Ok(new StatusResponseDto<EncodingTask?>
        {
            Status = "ok",
            Data = task,
            Message = task is not null
                ? "Task available for execution."
                : "No pending tasks available."
        });
    }

    /// <summary>
    /// Assign a task to a specific encoder node
    /// </summary>
    /// <param name="taskId">Task ID (ULID)</param>
    /// <param name="request">Assignment request with node ID</param>
    /// <returns>Success status</returns>
    [HttpPost]
    [Route("tasks/{taskId:ulid}/assign")]
    [ProducesResponseType(typeof(StatusResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignTask(Ulid taskId, [FromBody] AssignTaskRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to assign encoding tasks");

        if (request.NodeId == Ulid.Empty)
            return BadRequestResponse("Node ID is required");

        try
        {
            bool success = await jobDispatcher.AssignTaskToNodeAsync(taskId, request.NodeId);

            if (!success)
            {
                return UnprocessableEntityResponse("Failed to assign task to node");
            }

            return Ok(new StatusResponseDto<bool>
            {
                Status = "ok",
                Data = true,
                Message = "Successfully assigned task to node."
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntityResponse($"Failed to assign task: {e.Message}");
        }
    }

    /// <summary>
    /// Mark a task as started (used by encoder nodes)
    /// </summary>
    /// <param name="taskId">Task ID (ULID)</param>
    /// <returns>Success status</returns>
    [HttpPost]
    [Route("tasks/{taskId:ulid}/start")]
    [ProducesResponseType(typeof(StatusResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> StartTask(Ulid taskId)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to manage encoding tasks");

        try
        {
            bool success = await jobDispatcher.StartTaskAsync(taskId);

            if (!success)
            {
                return UnprocessableEntityResponse("Failed to start task");
            }

            return Ok(new StatusResponseDto<bool>
            {
                Status = "ok",
                Data = true,
                Message = "Successfully started task."
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntityResponse($"Failed to start task: {e.Message}");
        }
    }

    /// <summary>
    /// Mark a task as completed (used by encoder nodes)
    /// </summary>
    /// <param name="taskId">Task ID (ULID)</param>
    /// <param name="request">Completion request with optional output file path</param>
    /// <returns>Success status</returns>
    [HttpPost]
    [Route("tasks/{taskId:ulid}/complete")]
    [ProducesResponseType(typeof(StatusResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompleteTask(Ulid taskId, [FromBody] CompleteTaskRequest? request = null)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to manage encoding tasks");

        try
        {
            bool success = await jobDispatcher.CompleteTaskAsync(taskId, request?.OutputFile);

            if (!success)
            {
                return UnprocessableEntityResponse("Failed to complete task");
            }

            return Ok(new StatusResponseDto<bool>
            {
                Status = "ok",
                Data = true,
                Message = "Successfully completed task."
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntityResponse($"Failed to complete task: {e.Message}");
        }
    }

    /// <summary>
    /// Mark a task as failed (used by encoder nodes)
    /// </summary>
    /// <param name="taskId">Task ID (ULID)</param>
    /// <param name="request">Failure request with error message</param>
    /// <returns>Success status</returns>
    [HttpPost]
    [Route("tasks/{taskId:ulid}/fail")]
    [ProducesResponseType(typeof(StatusResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> FailTask(Ulid taskId, [FromBody] FailTaskRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to manage encoding tasks");

        if (string.IsNullOrWhiteSpace(request.ErrorMessage))
            return BadRequestResponse("Error message is required");

        try
        {
            bool success = await jobDispatcher.FailTaskAsync(taskId, request.ErrorMessage);

            if (!success)
            {
                return UnprocessableEntityResponse("Failed to mark task as failed");
            }

            return Ok(new StatusResponseDto<bool>
            {
                Status = "ok",
                Data = true,
                Message = "Task marked as failed."
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntityResponse($"Failed to update task: {e.Message}");
        }
    }

    /// <summary>
    /// Record progress for a running task (used by encoder nodes)
    /// </summary>
    /// <param name="taskId">Task ID (ULID)</param>
    /// <param name="request">Progress data to record</param>
    /// <returns>Success status</returns>
    [HttpPost]
    [Route("tasks/{taskId:ulid}/progress")]
    [ProducesResponseType(typeof(StatusResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordProgress(Ulid taskId, [FromBody] RecordProgressRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to manage encoding tasks");

        try
        {
            EncodingProgress progress = new()
            {
                Id = Ulid.NewUlid(),
                TaskId = taskId,
                ProgressPercentage = request.ProgressPercentage,
                Fps = request.Fps,
                Speed = request.Speed,
                Bitrate = request.Bitrate,
                CurrentTime = request.CurrentTime,
                TotalDuration = request.TotalDuration,
                EstimatedRemaining = request.EstimatedRemaining,
                EncodedFrames = request.EncodedFrames,
                TotalFrames = request.TotalFrames,
                OutputSize = request.OutputSize,
                RecordedAt = DateTime.UtcNow
            };

            bool success = await jobDispatcher.RecordProgressAsync(taskId, progress);

            if (!success)
            {
                return UnprocessableEntityResponse("Failed to record progress");
            }

            return Ok(new StatusResponseDto<bool>
            {
                Status = "ok",
                Data = true,
                Message = "Progress recorded."
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntityResponse($"Failed to record progress: {e.Message}");
        }
    }
}

/// <summary>
/// Request DTO for submitting a new encoding job
/// </summary>
public class SubmitJobRequest
{
    /// <summary>
    /// Path to the source media file (required)
    /// </summary>
    public required string InputFilePath { get; set; }

    /// <summary>
    /// Path to the output directory (required)
    /// </summary>
    public required string OutputFolder { get; set; }

    /// <summary>
    /// Profile ID to use (null for default profile)
    /// </summary>
    public Ulid? ProfileId { get; set; }

    /// <summary>
    /// Job priority level
    /// </summary>
    public JobPriority? Priority { get; set; }

    /// <summary>
    /// Task splitting strategy
    /// </summary>
    public TaskDistributionStrategy? SplitStrategy { get; set; }

    /// <summary>
    /// Node selection strategy
    /// </summary>
    public NodeSelectionStrategy? NodeStrategy { get; set; }

    /// <summary>
    /// Whether to assign tasks to nodes immediately
    /// </summary>
    public bool? AssignNodesImmediately { get; set; }

    /// <summary>
    /// Whether to include post-processing tasks
    /// </summary>
    public bool? IncludePostProcessing { get; set; }

    /// <summary>
    /// Whether to include validation as a final task
    /// </summary>
    public bool? IncludeValidation { get; set; }

    /// <summary>
    /// Custom title for the job (null to use input filename)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Maximum number of retries for failed tasks
    /// </summary>
    public int? MaxTaskRetries { get; set; }
}

/// <summary>
/// Request DTO for assigning a task to a node
/// </summary>
public class AssignTaskRequest
{
    /// <summary>
    /// Node ID to assign the task to
    /// </summary>
    public Ulid NodeId { get; set; }
}

/// <summary>
/// Request DTO for completing a task
/// </summary>
public class CompleteTaskRequest
{
    /// <summary>
    /// Path to the output file (optional)
    /// </summary>
    public string? OutputFile { get; set; }
}

/// <summary>
/// Request DTO for failing a task
/// </summary>
public class FailTaskRequest
{
    /// <summary>
    /// Error message describing the failure (required)
    /// </summary>
    public required string ErrorMessage { get; set; }
}

/// <summary>
/// Request DTO for recording task progress
/// </summary>
public class RecordProgressRequest
{
    /// <summary>
    /// Progress percentage (0.0 to 100.0)
    /// </summary>
    public double ProgressPercentage { get; set; }

    /// <summary>
    /// Current encoding speed in frames per second
    /// </summary>
    public double? Fps { get; set; }

    /// <summary>
    /// Encoding speed multiplier (e.g., 2.5x)
    /// </summary>
    public double? Speed { get; set; }

    /// <summary>
    /// Current output bitrate (e.g., "5000kbps")
    /// </summary>
    public string? Bitrate { get; set; }

    /// <summary>
    /// Current position in the source media
    /// </summary>
    public TimeSpan? CurrentTime { get; set; }

    /// <summary>
    /// Total duration of the source media
    /// </summary>
    public TimeSpan? TotalDuration { get; set; }

    /// <summary>
    /// Estimated time remaining
    /// </summary>
    public TimeSpan? EstimatedRemaining { get; set; }

    /// <summary>
    /// Number of frames encoded so far
    /// </summary>
    public long? EncodedFrames { get; set; }

    /// <summary>
    /// Total number of frames to encode
    /// </summary>
    public long? TotalFrames { get; set; }

    /// <summary>
    /// Current output file size in bytes
    /// </summary>
    public long? OutputSize { get; set; }
}
