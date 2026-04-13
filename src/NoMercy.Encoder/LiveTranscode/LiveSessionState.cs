namespace NoMercy.Encoder.LiveTranscode;

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
