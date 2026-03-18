using Microsoft.Extensions.Logging;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;

namespace NoMercyQueue;

public class JobDispatcher : IJobDispatcher
{
    private readonly JobQueue _queue;
    private readonly ILogger<JobDispatcher> _logger;

    public JobDispatcher(JobQueue queue, ILogger<JobDispatcher> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public void Dispatch(IShouldQueue job)
    {
        Dispatch(job, job.QueueName, job.Priority);
    }

    public void Dispatch(IShouldQueue job, string onQueue, int priority)
    {
        QueueJobModel jobData = new()
        {
            Queue = onQueue,
            Payload = SerializationHelper.Serialize(job),
            AvailableAt = DateTime.UtcNow,
            Priority = priority
        };

        try
        {
            _queue.Enqueue(jobData);
        }
        catch (Exception e)
        {
            _logger.LogError("{Message}", e.Message);
        }
    }
}
