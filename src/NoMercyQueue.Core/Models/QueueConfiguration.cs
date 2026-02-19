namespace NoMercyQueue.Core.Models;

public record QueueConfiguration
{
    public Dictionary<string, int> WorkerCounts { get; init; } = new();

    public byte MaxAttempts { get; init; } = 3;
    public int PollingIntervalMs { get; init; } = 1000;
}
