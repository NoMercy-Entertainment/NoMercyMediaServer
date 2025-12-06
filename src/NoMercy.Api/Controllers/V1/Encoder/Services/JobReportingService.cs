using Microsoft.Extensions.Logging;
using NoMercy.Database;
using Microsoft.EntityFrameworkCore;

namespace NoMercy.Api.Controllers.V1.Encoder.Services;

public class JobReportingService : IJobReportingService
{
    private readonly ILogger<JobReportingService> _logger;
    private readonly QueueContext _queueContext;

    public JobReportingService(ILogger<JobReportingService> logger, QueueContext queueContext)
    {
        _logger = logger;
        _queueContext = queueContext;
    }

    public async Task<bool> ReportJobProgressAsync(string jobId, string nodeId, int progressPercent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            _logger.LogWarning("Cannot report progress: JobId is empty");
            return false;
        }

        try
        {
            _logger.LogDebug("Encoder job {JobId} progress: {Progress}% from node {NodeId}", jobId, progressPercent, nodeId);
            
            Networking.Networking.SendToAll("encoder-progress", "dashboardHub", new
            {
                jobId = jobId,
                progress = progressPercent,
                status = "running"
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to report progress for job {JobId}", jobId);
            return false;
        }
    }

    public async Task<bool> ReportJobCompletionAsync(string jobId, string nodeId, JobCompletionStatus status, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobId) || status == null)
        {
            _logger.LogWarning("Cannot report completion: JobId or status is invalid");
            return false;
        }

        try
        {
            if (status.Status == "success")
            {
                _logger.LogInformation("Encoder job {JobId} completed successfully. Output: {OutputPath}, Size: {FileSize} bytes, Duration: {Duration}s", 
                    jobId, status.OutputPath, status.FileSize, status.Duration.TotalSeconds);

                Networking.Networking.SendToAll("encoder-complete", "dashboardHub", new
                {
                    jobId = jobId,
                    status = "success",
                    outputPath = status.OutputPath,
                    fileSize = status.FileSize,
                    duration = status.Duration.TotalSeconds
                });
            }
            else
            {
                _logger.LogWarning("Encoder job {JobId} failed: {ErrorMessage}", jobId, status.ErrorMessage);

                Networking.Networking.SendToAll("encoder-failed", "dashboardHub", new
                {
                    jobId = jobId,
                    status = "failed",
                    error = status.ErrorMessage
                });
            }

            var queueJob = await _queueContext.QueueJobs
                .FirstOrDefaultAsync(q => q.Payload.Contains(jobId), cancellationToken);

            if (queueJob != null)
            {
                _queueContext.QueueJobs.Remove(queueJob);
                await _queueContext.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("Removed completed job {JobId} from queue", jobId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to report completion for job {JobId}", jobId);
            return false;
        }
    }
}
