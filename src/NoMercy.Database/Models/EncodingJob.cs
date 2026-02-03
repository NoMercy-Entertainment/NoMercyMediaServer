using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

/// <summary>
/// Represents an encoding job in the EncoderV2 system.
/// A job contains multiple tasks that can be distributed across encoder nodes.
/// </summary>
[PrimaryKey(nameof(Id))]
public class EncodingJob : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    /// <summary>
    /// Reference to the EncoderProfile in MediaContext.
    /// Note: This is a cross-database reference; the relationship is managed at the application level.
    /// </summary>
    [JsonProperty("profile_id")]
    public Ulid? ProfileId { get; set; }

    /// <summary>
    /// Immutable snapshot of the profile at job creation time.
    /// This ensures profile changes don't affect in-progress jobs.
    /// </summary>
    [Column("ProfileSnapshot")]
    [JsonProperty("profile_snapshot")]
    [MaxLength(8192)]
    public string ProfileSnapshotJson { get; set; } = string.Empty;

    [Required]
    [JsonProperty("input_file_path")]
    [MaxLength(1024)]
    public string InputFilePath { get; set; } = null!;

    [Required]
    [JsonProperty("output_folder")]
    [MaxLength(1024)]
    public string OutputFolder { get; set; } = null!;

    [JsonProperty("title")]
    [MaxLength(512)]
    public string? Title { get; set; }

    [Required]
    [JsonProperty("state")]
    [MaxLength(32)]
    public string State { get; set; } = EncodingJobState.Queued;

    [JsonProperty("error_message")]
    [MaxLength(4096)]
    public string? ErrorMessage { get; set; }

    [JsonProperty("priority")]
    public int Priority { get; set; } = 0;

    [JsonProperty("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonProperty("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonProperty("tasks")]
    public ICollection<EncodingTask> Tasks { get; set; } = [];
}

/// <summary>
/// Valid states for an encoding job.
/// </summary>
public static class EncodingJobState
{
    public const string Queued = "queued";
    public const string Encoding = "encoding";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}
