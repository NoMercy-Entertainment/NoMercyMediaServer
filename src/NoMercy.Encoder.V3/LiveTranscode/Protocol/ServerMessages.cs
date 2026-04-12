namespace NoMercy.Encoder.V3.LiveTranscode.Protocol;

using NoMercy.Encoder.V3.Errors;

public record SessionCreatedMessage(
    string SessionId,
    double DurationSeconds,
    LiveQuality[] AvailableQualities,
    LiveQuality SelectedQuality,
    string FirstSegmentUrl
);

public record SegmentReadyMessage(
    int Index,
    double StartTimeSeconds,
    double DurationSeconds,
    string RelativeUrl,
    long SizeBytes
);

public record SeekCompletedMessage(double NewPositionSeconds, int FirstSegmentIndex);

public record QualityChangedMessage(LiveQuality NewQuality, QualityChangeReason Reason);

public enum QualityChangeReason
{
    UserRequested,
    AutoAdaptive,
    HardwareLimited,
    GpuFallbackToCpu,
}

public record TranscodeStateMessage(
    double Speed,
    double BufferAheadSeconds,
    LiveSessionState State
);

public record TranscodeErrorMessage(EncodingErrorKind Kind, string Message, bool Recoverable);

public record SessionEndedMessage(SessionEndReason Reason);

public enum SessionEndReason
{
    ClientDisconnected,
    Completed,
    Error,
    ServerShutdown,
}
