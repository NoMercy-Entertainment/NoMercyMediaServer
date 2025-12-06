using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoMercy.Api.Controllers.V1.Encoder.Dto;
using NoMercy.Api.Controllers.V1.Encoder.Services;

namespace NoMercy.Api.Controllers.V1.Encoder;

/// <summary>
/// API endpoints for encoder nodes to receive and manage encoding jobs
/// Delegates implementation to NoMercy.EncoderNode services
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/encoder/jobs", Order = 26)]
[Authorize]
public class EncoderJobsController : ControllerBase
{
    private readonly IJobExecutorService _jobExecutor;
    private readonly IJobReportingService _jobReporting;
    private readonly ILogger<EncoderJobsController> _logger;

    public EncoderJobsController(
        IJobExecutorService jobExecutor,
        IJobReportingService jobReporting,
        ILogger<EncoderJobsController> logger)
    {
        _jobExecutor = jobExecutor;
        _jobReporting = jobReporting;
        _logger = logger;
    }

    /// <summary>
    /// Receive a job from the dispatcher and queue it for processing on the encoder node
    /// Called by dispatcher to assign work to an encoder node
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReceiveJob([FromBody] EncoderJobDispatchRequest jobRequest)
    {
        if (jobRequest == null)
        {
            _logger.LogWarning("Received null job request");
            return BadRequest(new { error = "Job request is required" });
        }

        try
        {
            _logger.LogInformation("Received job: {JobId} from authenticated encoder node", jobRequest.JobId);

            // Log job details for debugging
            _logger.LogInformation("Job details: Input={Input}, Output={Output}, Format={Format}, Video={Video}, Audio={Audio}",
                jobRequest.InputPath, jobRequest.OutputPath, jobRequest.ContainerFormat, 
                jobRequest.VideoCodec, jobRequest.AudioCodec);

            // Delegate to EncoderNode service for actual job processing
            var encodingRequest = new EncodingJobRequest
            {
                JobId = jobRequest.JobId,
                InputPath = jobRequest.InputPath,
                OutputPath = jobRequest.OutputPath,
                FfmpegCommand = BuildFfmpegCommand(jobRequest)
            };

            var result = await _jobExecutor.ExecuteJobAsync(encodingRequest, CancellationToken.None);

            return Ok(new
            {
                status = result.Success ? "accepted" : "failed",
                jobId = jobRequest.JobId,
                message = result.ErrorMessage ?? "Job received and queued for processing"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving job {JobId}", jobRequest?.JobId);
            return StatusCode(500, new { error = "Failed to receive job", details = ex.Message });
        }
    }

    /// <summary>
    /// Get the status of a job
    /// </summary>
    [HttpGet("{jobId}/status")]
    public async Task<IActionResult> GetJobStatus(string jobId)
    {
        try
        {
            // TODO: Implement job status retrieval from EncoderNode service
            return Ok(new
            {
                jobId = jobId,
                status = "processing",
                progress = 0,
                message = "Job is processing"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job status for {JobId}", jobId);
            return StatusCode(500, new { error = "Failed to get job status", details = ex.Message });
        }
    }

    /// <summary>
    /// Helper method to build FFmpeg command from job request
    /// </summary>
    private string BuildFfmpegCommand(EncoderJobDispatchRequest request)
    {
        // This is a placeholder - actual command building would be delegated to EncoderV2
        return $"ffmpeg -i \"{request.InputPath}\" -c:v {request.VideoCodec} -c:a {request.AudioCodec} \"{request.OutputPath}\"";
    }

    /// <summary>
    /// Update job progress/status
    /// </summary>
    [HttpPost("{jobId}/progress")]
    public async Task<IActionResult> UpdateJobProgress(string jobId, [FromBody] JobProgressUpdate progress)
    {
        try
        {
            _logger.LogInformation("Job {JobId} progress: {Progress}%", jobId, progress?.ProgressPercent);
            
            // Delegate to reporting service
            bool reported = await _jobReporting.ReportJobProgressAsync(jobId, "", progress?.ProgressPercent ?? 0, CancellationToken.None);

            return Ok(new
            {
                status = reported ? "acknowledged" : "queued",
                jobId = jobId,
                message = "Progress acknowledged"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job progress for {JobId}", jobId);
            return StatusCode(500, new { error = "Failed to update job progress", details = ex.Message });
        }
    }

    /// <summary>
    /// Mark a job as complete and remove it from the queue
    /// </summary>
    [HttpPost("{jobId}/complete")]
    public async Task<IActionResult> CompleteJob(string jobId, [FromBody] JobCompletionData completionData)
    {
        try
        {
            _logger.LogInformation("Job {JobId} completed with status: {Status}", jobId, completionData?.Status ?? "unknown");

            if (completionData?.Status == "success")
            {
                _logger.LogInformation("Job {JobId} completed successfully at {CompletedAt}", jobId, completionData.CompletedAt);
            }
            else
            {
                _logger.LogWarning("Job {JobId} failed: {ErrorMessage}", jobId, completionData?.ErrorMessage);
            }

            // Delegate to reporting service
            var status = new JobCompletionStatus
            {
                Status = completionData?.Status ?? "unknown",
                CompletedAt = completionData?.CompletedAt ?? DateTime.UtcNow,
                OutputPath = completionData?.OutputPath ?? string.Empty,
                FileSize = completionData?.FileSize ?? 0,
                Duration = completionData?.Duration ?? TimeSpan.Zero,
                ErrorMessage = completionData?.ErrorMessage
            };

            bool reported = await _jobReporting.ReportJobCompletionAsync(jobId, "", status, CancellationToken.None);

            return Ok(new
            {
                status = "acknowledged",
                jobId = jobId,
                message = "Job completion acknowledged and removed from queue"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing job {JobId}", jobId);
            return StatusCode(500, new { error = "Failed to complete job", details = ex.Message });
        }
    }
}
