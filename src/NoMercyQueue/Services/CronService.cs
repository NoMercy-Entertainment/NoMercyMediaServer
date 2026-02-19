using NCrontab;
namespace NoMercy.Queue.Services;

public class CronService
{
    public static DateTime GetNextOccurrence(string cronExpression, DateTime baseTime)
    {
        CrontabSchedule? schedule = CrontabSchedule.Parse(cronExpression);
        return schedule.GetNextOccurrence(baseTime);
    }
        
    public static bool ShouldRun(string cronExpression, DateTime lastRun, DateTime currentTime)
    {
        DateTime nextRun = GetNextOccurrence(cronExpression, lastRun);
        return currentTime >= nextRun;
    }
}