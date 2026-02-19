namespace NoMercy.Events.Audit;

public sealed class EventAuditOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxEntries { get; set; } = 10_000;
    public double CompactionPercentage { get; set; } = 0.25;
    public HashSet<string> ExcludedEventTypes { get; set; } = [];
}
