namespace NoMercy.Queue.Core.Models;

public record QueueConfiguration
{
    public Dictionary<string, int> WorkerCounts { get; init; } = new()
    {
        ["queue"] = 1,
        ["encoder"] = 2,
        ["cron"] = 1,
        ["data"] = 10,
        ["image"] = 5,
        ["file"] = 2
    };

    public byte MaxAttempts { get; init; } = 3;
    public int PollingIntervalMs { get; init; } = 1000;
}
