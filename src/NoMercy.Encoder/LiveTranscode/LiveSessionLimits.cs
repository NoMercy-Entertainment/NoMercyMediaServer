namespace NoMercy.Encoder.LiveTranscode;

public class LiveSessionLimits
{
    public int MaxConcurrentSessions { get; set; } = 4;
    public int MaxSessionsPerUser { get; set; } = 2;
    public long MaxSegmentDiskUsageBytes { get; set; } = 1L * 1024 * 1024 * 1024;
    public int SessionTimeoutMinutes { get; set; } = 30;
}
