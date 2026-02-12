using NoMercy.NmSystem.SystemCalls;
using NoMercy.Queue.Core.Models;
using Serilog.Events;

namespace NoMercy.Queue;

public class JobDispatcher
{
    private static readonly JobQueue Queue = new(new EfQueueContextAdapter(new()));

    public static void Dispatch(IShouldQueue job, string onQueue = "default", int priority = 0)
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
            Queue.Enqueue(jobData);
        }
        catch (Exception e)
        {
            Logger.Queue(e.Message, LogEventLevel.Error);
        }
    }
}