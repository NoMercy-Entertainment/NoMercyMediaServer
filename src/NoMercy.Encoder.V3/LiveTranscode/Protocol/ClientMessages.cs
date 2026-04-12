namespace NoMercy.Encoder.V3.LiveTranscode.Protocol;

public record RequestSeekMessage(double PositionSeconds);

public record RequestQualityMessage(string? QualityId);

public record ReportPositionMessage(double CurrentTimeSeconds);

// Parameterless client messages (RequestPause, RequestResume, Heartbeat, EndSession)
// are signal-only — no payload needed, no record required.
