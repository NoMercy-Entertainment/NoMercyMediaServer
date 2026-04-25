namespace NoMercy.Encoder.LiveTranscode;

public class LiveSessionLimits
{
    public int MaxConcurrentSessions { get; set; } = 4;
    public int MaxSessionsPerUser { get; set; } = 2;
    public long MaxSegmentDiskUsageBytes { get; set; } = 1L * 1024 * 1024 * 1024;
    public int SessionTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Minutes of inactivity (no playlist or segment hit) after which the reaper
    /// disposes and cleans the session. Default: 5 minutes.
    /// </summary>
    public int IdleTimeoutMinutes { get; set; } = 5;
}
