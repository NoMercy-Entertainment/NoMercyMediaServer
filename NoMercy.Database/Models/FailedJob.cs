#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
public class FailedJob
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public Guid Uuid { get; set; }
    public string Connection { get; set; } = "default";
    [MaxLength(1024)] public required string Queue { get; set; }
    [MaxLength(4092)] public required string Payload { get; set; }
    [MaxLength(4092)] public required string Exception { get; set; }
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
}