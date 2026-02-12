using NoMercy.NmSystem.SystemCalls;
using NoMercy.Queue.Core.Interfaces;
using NoMercy.Queue.Core.Models;
using Serilog.Events;
using CoreIShouldQueue = NoMercy.Queue.Core.Interfaces.IShouldQueue;

namespace NoMercy.Queue;

public class JobDispatcher : IJobDispatcher
{
    private readonly JobQueue _queue;

    public JobDispatcher(JobQueue queue)
    {
        _queue = queue;
    }

    public void Dispatch(CoreIShouldQueue job)
    {
        Dispatch(job, job.QueueName, job.Priority);
    }

    public void Dispatch(CoreIShouldQueue job, string onQueue, int priority)
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
            Logger.Queue(e.Message, LogEventLevel.Error);
        }
    }
}
