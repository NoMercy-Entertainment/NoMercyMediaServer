using Newtonsoft.Json;
using NoMercy.NmSystem.Dto;

namespace NoMercy.EncoderNode.Api;

/// <summary>
/// Encoder node registration request DTO
/// Matches the EncoderNodeCapabilities sent by encoder nodes
/// </summary>
public class EncoderNodeRegistrationDto
{
    [JsonProperty("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonProperty("node_name")]
    public string NodeName { get; set; } = string.Empty;

    [JsonProperty("node_version")]
    public string NodeVersion { get; set; } = string.Empty;

    [JsonProperty("network_address")]
    public string NetworkAddress { get; set; } = string.Empty;

    [JsonProperty("network_port")]
    public int NetworkPort { get; set; }

    [JsonProperty("use_https")]
    public bool UseHttps { get; set; } = false;

    [JsonProperty("max_concurrent_jobs")]
    public int MaxConcurrentJobs { get; set; } = 1;

    [JsonProperty("current_job_count")]
    public int CurrentJobCount { get; set; } = 0;

    [JsonProperty("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("registered_at")]
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("last_heartbeat")]
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Encoder node heartbeat model matching encoder node's heartbeat payload
/// </summary>
public class EncoderNodeHeartbeatModel
{
    [JsonProperty("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonProperty("cpu_usage")]
    public float CpuUsage { get; set; }

    [JsonProperty("memory_usage_mb")]
    public int MemoryUsageMb { get; set; }

    [JsonProperty("available_memory_mb")]
    public int AvailableMemoryMb { get; set; }

    [JsonProperty("active_jobs")]
    public int ActiveJobs { get; set; }

    [JsonProperty("completed_jobs")]
    public int CompletedJobs { get; set; }

    [JsonProperty("gpu_usage")]
    public float? GpuUsage { get; set; }

    [JsonProperty("temperature")]
    public float? Temperature { get; set; }
}

/// <summary>
/// Encoder availability information
/// </summary>
public class EncoderAvailabilityDto
{
    public string EncoderName { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public bool IsHardwareAccelerated { get; set; }
    public List<string> AvailablePresets { get; set; } = [];
    public string? Reason { get; set; }
}

/// <summary>
/// Server hardware capabilities for comparison
/// </summary>
public class ServerHardwareCapabilities
{
    [JsonProperty("cpu_cores")]
    public int CpuCores { get; set; }

    [JsonProperty("cpu_name")]
    public string CpuName { get; set; } = string.Empty;

    [JsonProperty("total_memory_mb")]
    public long TotalMemoryMb { get; set; }

    [JsonProperty("gpu_name")]
    public string? GpuName { get; set; }

    [JsonProperty("gpu_memory_mb")]
    public long? GpuMemoryMb { get; set; }

    [JsonProperty("has_hardware_encoding")]
    public bool HasHardwareEncoding { get; set; }

    [JsonProperty("supported_accelerators")]
    public List<string> SupportedAccelerators { get; set; } = [];
}

/// <summary>
/// Server vs Node comparison data
/// </summary>
public class ServerVsNodeComparisonDto
{
    public ServerHardwareCapabilities? ServerCapabilities { get; set; }
    public List<string> RecommendedWorkloads { get; set; } = [];
    public List<string> ThinClientAdvantages { get; set; } = [];
    public List<string> PrimaryServerAdvantages { get; set; } = [];
}
