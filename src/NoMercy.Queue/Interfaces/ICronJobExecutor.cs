namespace NoMercy.Queue.Interfaces;

public interface ICronJobExecutor
{
    string CronExpression { get; }
    string JobName { get; }
    Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default);
}