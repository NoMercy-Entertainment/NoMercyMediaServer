namespace NoMercy.Encoder.V3.LiveTranscode;

public enum LiveSessionState
{
    Starting,
    Transcoding,
    Buffering,
    Buffered,
    Seeking,
    ChangingQuality,
    Error,
    Ended,
}
