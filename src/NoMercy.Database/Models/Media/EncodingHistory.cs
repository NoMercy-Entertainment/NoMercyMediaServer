using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Media;

/// <summary>
/// Append-only record of every successful encode. The dashboard reads from
/// this table to show users what was encoded, when, how long it took, and
/// how much space it reclaimed (or spent). Writes happen from the encoding
/// orchestrator on EncodingResult.Success == true.
/// </summary>
[PrimaryKey(nameof(Id))]
[Index(nameof(CreatedAt))]
[Index(nameof(ProfileId))]
public class EncodingHistory
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty("input_path")]
    [MaxLength(4096)]
    public required string InputPath { get; set; }

    [JsonProperty("output_path")]
    [MaxLength(4096)]
    public required string OutputPath { get; set; }

    /// <summary>Profile that produced this encode — denormalized so history
    /// survives profile deletion.</summary>
    [JsonProperty("profile_id")]
    public Ulid? ProfileId { get; set; }

    [JsonProperty("profile_name")]
    [MaxLength(256)]
    public required string ProfileName { get; set; }

    /// <summary>FFmpeg encoder used (libx264, h264_nvenc, …).</summary>
    [JsonProperty("encoder_used")]
    [MaxLength(64)]
    public required string EncoderUsed { get; set; }

    [JsonProperty("gpu_used")]
    [MaxLength(64)]
    public string? GpuUsed { get; set; }

    /// <summary>Total wall-clock duration of the encode.</summary>
    [JsonProperty("duration_seconds")]
    public double DurationSeconds { get; set; }

    [JsonProperty("input_size_bytes")]
    public long InputSizeBytes { get; set; }

    [JsonProperty("output_size_bytes")]
    public long OutputSizeBytes { get; set; }

    /// <summary>output/input ratio; &lt; 1 means the encode reclaimed space.
    /// 0 when input size is unknown.</summary>
    [JsonProperty("compression_ratio")]
    public double CompressionRatio { get; set; }

    [JsonProperty("average_speed")]
    public double AverageSpeed { get; set; }

    [JsonProperty("average_fps")]
    public double AverageFps { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
