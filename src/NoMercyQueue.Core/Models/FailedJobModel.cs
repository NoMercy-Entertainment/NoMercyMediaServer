namespace NoMercy.Queue.Core.Models;

public class FailedJobModel
{
    public long Id { get; set; }
    public Guid Uuid { get; set; }
    public string Connection { get; set; } = "default";
    public required string Queue { get; set; }
    public required string Payload { get; set; }
    public required string Exception { get; set; }
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
}
