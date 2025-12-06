namespace NoMercy.Api.Controllers.V1.Encoder.Dto;

/// <summary>
/// DTO for encoder node heartbeat data
/// Sent periodically by encoder nodes to indicate they're alive
/// </summary>
public class EncoderNodeHeartbeatModel
{
    public string NodeId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int ActiveJobs { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double DiskUsage { get; set; }
}
