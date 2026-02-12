namespace NoMercy.Plugins.Abstractions;

public interface IScheduledTaskPlugin : IPlugin
{
    string CronExpression { get; }
    Task ExecuteAsync(CancellationToken ct = default);
}
