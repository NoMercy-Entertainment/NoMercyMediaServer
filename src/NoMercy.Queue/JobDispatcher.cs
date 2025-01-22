using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;
using Serilog.Events;

namespace NoMercy.Queue;

public class JobDispatcher
{
    private static readonly JobQueue Queue = new(new());

    public static void Dispatch(IShouldQueue job, string onQueue = "default", int priority = 0)
    {
        QueueJob jobData = new()
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