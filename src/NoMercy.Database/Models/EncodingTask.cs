using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

/// <summary>
/// Represents an individual task within an encoding job.
/// Tasks are distributed to encoder nodes for execution.
/// </summary>
[PrimaryKey(nameof(Id))]
public class EncodingTask : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [Required]
    [JsonProperty("job_id")]
    public Ulid JobId { get; set; }

    [ForeignKey(nameof(JobId))]
    [JsonProperty("job")]
    public EncodingJob Job { get; set; } = null!;

    [Required]
    [JsonProperty("task_type")]
    [MaxLength(64)]
    public string TaskType { get; set; } = null!;

    /// <summary>
    /// Estimated CPU/time weight for load balancing.
    /// Higher values indicate more resource-intensive tasks.
    /// </summary>
    [JsonProperty("weight")]
    public int Weight { get; set; } = 1;

    [Required]
    [JsonProperty("state")]
    [MaxLength(32)]
    public string State { get; set; } = EncodingTaskState.Pending;

    [JsonProperty("assigned_node_id")]
    public Ulid? AssignedNodeId { get; set; }

    [ForeignKey(nameof(AssignedNodeId))]
    [JsonProperty("assigned_node")]
    public EncoderNode? AssignedNode { get; set; }

    /// <summary>
    /// JSON array of task IDs that must complete before this task can run.
    /// </summary>
    [Column("Dependencies")]
    [JsonProperty("dependencies")]
    [MaxLength(2048)]
    public string DependenciesJson { get; set; } = "[]";

    [NotMapped]
    public string[] Dependencies
    {
        get => !string.IsNullOrEmpty(DependenciesJson)
            ? JsonConvert.DeserializeObject<string[]>(DependenciesJson) ?? []
            : [];
        set => DependenciesJson = JsonConvert.SerializeObject(value);
    }

    [JsonProperty("retry_count")]
    public int RetryCount { get; set; } = 0;

    [JsonProperty("max_retries")]
    public int MaxRetries { get; set; } = 3;

    [JsonProperty("error_message")]
    [MaxLength(4096)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// FFmpeg command or task-specific arguments as JSON.
    /// </summary>
    [Column("CommandArgs")]
    [JsonProperty("command_args")]
    [MaxLength(8192)]
    public string CommandArgsJson { get; set; } = "{}";

    [JsonProperty("output_file")]
    [MaxLength(1024)]
    public string? OutputFile { get; set; }

    [JsonProperty("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonProperty("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonProperty("progress")]
    public ICollection<EncodingProgress> ProgressHistory { get; set; } = [];
}

/// <summary>
/// Valid states for an encoding task.
/// </summary>
public static class EncodingTaskState
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

/// <summary>
/// Types of encoding tasks.
/// </summary>
public static class EncodingTaskType
{
    public const string HdrConversion = "hdr_conversion";
    public const string VideoEncoding = "video_encoding";
    public const string AudioEncoding = "audio_encoding";
    public const string SubtitleExtraction = "subtitle_extraction";
    public const string FontExtraction = "font_extraction";
    public const string ThumbnailGeneration = "thumbnail_generation";
    public const string SpriteGeneration = "sprite_generation";
    public const string ChapterExtraction = "chapter_extraction";
    public const string PlaylistGeneration = "playlist_generation";
    public const string Validation = "validation";
}
