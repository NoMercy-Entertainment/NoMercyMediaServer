using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.SystemCalls;
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