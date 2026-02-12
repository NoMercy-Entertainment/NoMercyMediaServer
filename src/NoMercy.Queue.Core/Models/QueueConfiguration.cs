namespace NoMercy.Queue.Core.Models;

public record QueueConfiguration
{
    public Dictionary<string, int> WorkerCounts { get; init; } = new()
    {
        ["queue"] = 1,
        ["data"] = 3,
        ["encoder"] = 1
    };

    public byte MaxAttempts { get; init; } = 3;
    public int PollingIntervalMs { get; init; } = 1000;
}
