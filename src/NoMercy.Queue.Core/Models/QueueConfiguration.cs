namespace NoMercy.Queue.Core.Models;

public record QueueConfiguration
{
    public Dictionary<string, int> WorkerCounts { get; init; } = new()
    {
        ["library"] = 1,
        ["import"] = 1,
        ["extras"] = 10,
        ["encoder"] = 2,
        ["cron"] = 1,
        ["image"] = 5,
        ["file"] = 2,
        ["music"] = 1
    };

    public byte MaxAttempts { get; init; } = 3;
    public int PollingIntervalMs { get; init; } = 1000;
}
