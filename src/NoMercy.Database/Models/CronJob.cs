using Microsoft.EntityFrameworkCore;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(Name), IsUnique = true)]
public class CronJob: Timestamps
{
    public int Id { get; set; }
        
    public string Name { get; set; } = null!;
        
    public string CronExpression { get; set; } = null!;
        
    public string JobType { get; set; } = null!;
        
    public string? Parameters { get; set; }
        
    public bool IsEnabled { get; set; } = true;
        
    public DateTime? LastRun { get; set; }
        
    public DateTime? NextRun { get; set; }
}