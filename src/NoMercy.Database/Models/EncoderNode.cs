using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

/// <summary>
/// Represents a distributed encoder node that can execute encoding tasks.
/// Nodes report their capabilities and health status to the main server.
/// </summary>
[PrimaryKey(nameof(Id))]
public class EncoderNode : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [Required]
    [JsonProperty("name")]
    [MaxLength(256)]
    public string Name { get; set; } = null!;

    [Required]
    [JsonProperty("ip_address")]
    [MaxLength(45)] // IPv6 max length
    public string IpAddress { get; set; } = null!;

    [JsonProperty("port")]
    public int Port { get; set; } = 7627;

    [JsonProperty("has_gpu")]
    public bool HasGpu { get; set; } = false;

    [JsonProperty("gpu_model")]
    [MaxLength(256)]
    public string? GpuModel { get; set; }

    /// <summary>
    /// GPU vendor for hardware acceleration selection (nvidia, amd, intel).
    /// </summary>
    [JsonProperty("gpu_vendor")]
    [MaxLength(64)]
    public string? GpuVendor { get; set; }

    [JsonProperty("cpu_cores")]
    public int CpuCores { get; set; } = 1;

    [JsonProperty("memory_gb")]
    public int MemoryGb { get; set; } = 1;

    [JsonProperty("is_healthy")]
    public bool IsHealthy { get; set; } = true;

    [JsonProperty("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonProperty("last_heartbeat")]
    public DateTime? LastHeartbeat { get; set; }

    /// <summary>
    /// Maximum concurrent tasks this node can handle.
    /// </summary>
    [JsonProperty("max_concurrent_tasks")]
    public int MaxConcurrentTasks { get; set; } = 1;

    /// <summary>
    /// Current number of running tasks on this node.
    /// </summary>
    [JsonProperty("current_task_count")]
    public int CurrentTaskCount { get; set; } = 0;

    /// <summary>
    /// JSON array of supported hardware acceleration types.
    /// </summary>
    [Column("SupportedAccelerations")]
    [JsonProperty("supported_accelerations")]
    [MaxLength(512)]
    public string SupportedAccelerationsJson { get; set; } = "[]";

    [NotMapped]
    public string[] SupportedAccelerations
    {
        get => !string.IsNullOrEmpty(SupportedAccelerationsJson)
            ? JsonConvert.DeserializeObject<string[]>(SupportedAccelerationsJson) ?? []
            : [];
        set => SupportedAccelerationsJson = JsonConvert.SerializeObject(value);
    }

    /// <summary>
    /// Operating system of the node (linux, windows, macos).
    /// </summary>
    [JsonProperty("os")]
    [MaxLength(64)]
    public string? OperatingSystem { get; set; }

    /// <summary>
    /// FFmpeg version installed on this node.
    /// </summary>
    [JsonProperty("ffmpeg_version")]
    [MaxLength(64)]
    public string? FfmpegVersion { get; set; }

    [JsonProperty("assigned_tasks")]
    public ICollection<EncodingTask> AssignedTasks { get; set; } = [];
}

/// <summary>
/// Known GPU vendors for hardware acceleration.
/// </summary>
public static class GpuVendor
{
    public const string Nvidia = "nvidia";
    public const string Amd = "amd";
    public const string Intel = "intel";
    public const string Apple = "apple";
}

/// <summary>
/// Supported hardware acceleration types.
/// </summary>
public static class HardwareAccelerationType
{
    public const string Nvenc = "nvenc";
    public const string Qsv = "qsv";
    public const string Vaapi = "vaapi";
    public const string Amf = "amf";
    public const string VideoToolbox = "videotoolbox";
    public const string None = "none";
}
