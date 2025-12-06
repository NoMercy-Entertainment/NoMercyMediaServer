using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace NoMercy.Database.Models;

/// <summary>
/// Cached server hardware capabilities to avoid regenerating on each startup
/// Only recalculates when FFmpeg version changes or cache is manually invalidated
/// Includes scoring system for encoding tier detection
/// </summary>
[Table("ServerCapabilityCache")]
public class ServerCapabilityCache : Timestamps
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// The FFmpeg version this cache was generated with
    /// If the current FFmpeg version differs, cache is invalid and must be regenerated
    /// </summary>
    [Required]
    public string FfmpegVersion { get; set; } = string.Empty;

    /// <summary>
    /// Serialized hardware system information (JSON)
    /// </summary>
    [Required]
    [JsonPropertyName("hardware")]
    public string HardwareJson { get; set; } = string.Empty;

    /// <summary>
    /// Serialized list of GPU devices (JSON)
    /// </summary>
    [Required]
    [JsonPropertyName("gpu_devices")]
    public string GpuDevicesJson { get; set; } = string.Empty;

    /// <summary>
    /// Serialized list of supported video codecs (JSON)
    /// </summary>
    [Required]
    [JsonPropertyName("video_codecs")]
    public string VideoCodecsJson { get; set; } = string.Empty;

    /// <summary>
    /// Serialized list of supported audio codecs (JSON)
    /// </summary>
    [Required]
    [JsonPropertyName("audio_codecs")]
    public string AudioCodecsJson { get; set; } = string.Empty;

    /// <summary>
    /// Serialized list of detected optical drives and their capabilities (JSON)
    /// </summary>
    [JsonPropertyName("optical_drives")]
    public string? OpticalDrivesJson { get; set; }

    /// <summary>
    /// Serialized encoding capability score (JSON)
    /// Contains overall score, video score, audio score, GPU tier, etc.
    /// </summary>
    [JsonPropertyName("capability_score")]
    public string? CapabilityScoreJson { get; set; }

    /// <summary>
    /// Overall encoding capability score (0-10 scale)
    /// Combines GPU tier, encoder availability, and hardware features
    /// </summary>
    [JsonPropertyName("overall_score")]
    public decimal OverallScore { get; set; }

    /// <summary>
    /// Video encoding capability score (0-10 scale)
    /// Based on GPU tier and available video encoders
    /// </summary>
    [JsonPropertyName("video_score")]
    public decimal VideoScore { get; set; }

    /// <summary>
    /// Audio encoding capability score (0-10 scale)
    /// Based on available audio encoders
    /// </summary>
    [JsonPropertyName("audio_score")]
    public decimal AudioScore { get; set; }

    /// <summary>
    /// GPU tier score (0-10 scale)
    /// 0=no GPU, 5=entry level, 7=mid-range, 10=high-end
    /// </summary>
    [JsonPropertyName("gpu_tier_score")]
    public decimal GpuTierScore { get; set; }

    /// <summary>
    /// Hardware acceleration score (0-10 scale)
    /// Based on number of available hardware encoders
    /// </summary>
    [JsonPropertyName("hardware_acceleration_score")]
    public decimal HardwareAccelerationScore { get; set; }

    /// <summary>
    /// When this cache was last generated
    /// </summary>
    [JsonPropertyName("cached_at")]
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional notes about the cache (e.g., "Manual invalidation requested")
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
