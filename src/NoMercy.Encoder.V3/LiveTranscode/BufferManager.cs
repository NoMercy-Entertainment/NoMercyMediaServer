namespace NoMercy.Encoder.V3.LiveTranscode;

public enum BufferAction
{
    None,
    Suspend,
    Resume,
    DropQuality,
    EmergencyDropQuality,
}

public class BufferManager
{
    public BufferAction Evaluate(TimeSpan bufferAhead, bool isSuspended)
    {
        double seconds = bufferAhead.TotalSeconds;

        if (seconds > 30 && !isSuspended)
            return BufferAction.Suspend;

        if (seconds < 15 && isSuspended)
            return BufferAction.Resume;

        if (seconds < 3)
            return BufferAction.EmergencyDropQuality;

        if (seconds < 5)
            return BufferAction.DropQuality;

        return BufferAction.None;
    }
}
