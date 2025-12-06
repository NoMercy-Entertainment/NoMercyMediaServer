using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models;
using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Encoder.Services;

public class JobExecutorService : IJobExecutorService
{
    private readonly ILogger<JobExecutorService> _logger;
    private readonly QueueContext _queueContext;

    public JobExecutorService(ILogger<JobExecutorService> logger, QueueContext queueContext)
    {
        _logger = logger;
        _queueContext = queueContext;
    }

    public async Task<JobExecutionResult> ExecuteJobAsync(EncodingJobRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.JobId) || string.IsNullOrWhiteSpace(request.InputPath))
        {
            _logger.LogWarning("Invalid job request: JobId={JobId}, InputPath={InputPath}", request.JobId, request.InputPath);
            return new JobExecutionResult
            {
                Success = false,
                ErrorMessage = "JobId and InputPath are required"
            };
        }

        try
        {
            _logger.LogInformation("Dispatching encoder job {JobId} to queue. Input: {Input}, Output: {Output}", 
                request.JobId, request.InputPath, request.OutputPath);
            
            var queueJob = new QueueJob
            {
                Queue = "encoder:video",
                Payload = JsonConvert.SerializeObject(new
                {
                    jobId = request.JobId,
                    inputPath = request.InputPath,
                    outputPath = request.OutputPath,
                    ffmpegCommand = request.FfmpegCommand
                }),
                AvailableAt = DateTime.UtcNow,
                Priority = 0,
                CreatedAt = DateTime.UtcNow
            };

            _queueContext.QueueJobs.Add(queueJob);
            await _queueContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully queued encoder job {JobId} with DB ID: {DbId}", request.JobId, queueJob.Id);

            return new JobExecutionResult
            {
                Success = true,
                ErrorMessage = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch encoding job {JobId}", request.JobId);
            return new JobExecutionResult
            {
                Success = false,
                ErrorMessage = $"Failed to dispatch job: {ex.Message}"
            };
        }
    }
}


