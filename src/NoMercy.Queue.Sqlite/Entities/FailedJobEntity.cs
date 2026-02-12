using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NoMercy.Queue.Sqlite.Entities;

[PrimaryKey(nameof(Id))]
internal class FailedJobEntity
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
