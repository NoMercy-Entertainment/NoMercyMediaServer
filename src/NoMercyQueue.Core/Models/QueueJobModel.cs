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

    /// <summary>
    /// ID of the coordinator job that spawned this child task.
    /// Null for top-level (non-decomposed) jobs.
    /// </summary>
    public int? ParentJobId { get; set; }

    /// <summary>
    /// Shared ULID tag grouping all tasks from one coordinator encode run.
    /// Null for non-decomposed jobs.
    /// </summary>
    public string? GroupTag { get; set; }
}
