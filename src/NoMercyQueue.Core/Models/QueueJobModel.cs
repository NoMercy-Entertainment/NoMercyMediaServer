namespace NoMercyQueue.Core.Models;

public class QueueJobModel
{
    public int Id { get; set; }
    public int Priority { get; set; }
    public string Queue { get; set; } = "default";
    public required string Payload { get; set; }
    public byte Attempts { get; set; }
    public DateTime? ReservedAt { get; set; }
    public DateTime AvailableAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
