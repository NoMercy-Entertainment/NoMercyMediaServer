namespace NoMercy.Encoder.LiveTranscode.Protocol;

public record RequestSeekMessage(double PositionSeconds);

public record RequestQualityMessage(string? QualityId);

public record ReportPositionMessage(double CurrentTimeSeconds);

public record RequestPauseMessage;

public record RequestResumeMessage;

public record HeartbeatMessage;

public record EndSessionMessage;
