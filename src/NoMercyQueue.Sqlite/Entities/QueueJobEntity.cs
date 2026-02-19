using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NoMercy.Queue.Sqlite.Entities;

[PrimaryKey(nameof(Id))]
internal class QueueJobEntity
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int Priority { get; set; }
    public string Queue { get; set; } = "default";
    [MaxLength(4096)] public required string Payload { get; set; }
    public byte Attempts { get; set; }
    public DateTime? ReservedAt { get; set; }
    public DateTime AvailableAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
