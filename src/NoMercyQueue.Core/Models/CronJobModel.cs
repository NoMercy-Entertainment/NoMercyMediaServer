namespace NoMercyQueue.Core.Models;

public class CronJobModel
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string CronExpression { get; set; } = null!;
    public string JobType { get; set; } = null!;
    public string? Parameters { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastRun { get; set; }
    public DateTime? NextRun { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
