namespace NoMercy.Providers.Helpers;

public class QueueOptions
{
    public int Concurrent { get; init; } = 5;
    public int Interval { get; init; } = 500;
    public bool Start { get; init; } = true;
}