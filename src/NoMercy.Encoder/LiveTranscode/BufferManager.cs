namespace NoMercy.Encoder.LiveTranscode;

public enum BufferAction
{
    None,
    Suspend,
    Resume,
    DropQuality,
    EmergencyDropQuality,
}

public class BufferManager(LiveSessionLimits limits)
{
    public BufferAction Evaluate(TimeSpan bufferAhead, bool isSuspended)
    {
        double seconds = bufferAhead.TotalSeconds;
        BufferThresholds t = limits.Buffer;

        if (seconds > t.SuspendAboveSeconds && !isSuspended)
            return BufferAction.Suspend;

        if (seconds < t.ResumeBelowSeconds && isSuspended)
            return BufferAction.Resume;

        if (seconds < t.EmergencyDropBelowSeconds)
            return BufferAction.EmergencyDropQuality;

        if (seconds < t.DropQualityBelowSeconds)
            return BufferAction.DropQuality;

        return BufferAction.None;
    }
}
