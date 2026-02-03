using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

/// <summary>
/// Stores progress snapshots for encoding tasks.
/// Used for real-time progress updates and historical analysis.
/// </summary>
[PrimaryKey(nameof(Id))]
public class EncodingProgress
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [Required]
    [JsonProperty("task_id")]
    public Ulid TaskId { get; set; }

    [ForeignKey(nameof(TaskId))]
    [JsonProperty("task")]
    public EncodingTask Task { get; set; } = null!;

    /// <summary>
    /// Progress percentage (0.0 to 100.0).
    /// </summary>
    [JsonProperty("progress_percentage")]
    public double ProgressPercentage { get; set; }

    /// <summary>
    /// Current encoding speed in frames per second.
    /// </summary>
    [JsonProperty("fps")]
    public double? Fps { get; set; }

    /// <summary>
    /// Encoding speed multiplier (e.g., 2.5x means encoding 2.5x faster than realtime).
    /// </summary>
    [JsonProperty("speed")]
    public double? Speed { get; set; }

    /// <summary>
    /// Current output bitrate (e.g., "5000kbps").
    /// </summary>
    [JsonProperty("bitrate")]
    [MaxLength(32)]
    public string? Bitrate { get; set; }

    /// <summary>
    /// Current position in the source media.
    /// </summary>
    [JsonProperty("current_time")]
    public TimeSpan? CurrentTime { get; set; }

    /// <summary>
    /// Total duration of the source media.
    /// </summary>
    [JsonProperty("total_duration")]
    public TimeSpan? TotalDuration { get; set; }

    /// <summary>
    /// Estimated time remaining for task completion.
    /// </summary>
    [JsonProperty("estimated_remaining")]
    public TimeSpan? EstimatedRemaining { get; set; }

    /// <summary>
    /// Number of frames encoded so far.
    /// </summary>
    [JsonProperty("encoded_frames")]
    public long? EncodedFrames { get; set; }

    /// <summary>
    /// Total number of frames to encode.
    /// </summary>
    [JsonProperty("total_frames")]
    public long? TotalFrames { get; set; }

    /// <summary>
    /// Current output file size in bytes.
    /// </summary>
    [JsonProperty("output_size")]
    public long? OutputSize { get; set; }

    [JsonProperty("recorded_at")]
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
