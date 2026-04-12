namespace NoMercy.Encoder.V3.Errors;

public record EncodingError(
    EncodingErrorKind Kind,
    string Message,
    string? FfmpegStderr,
    string? StageName,
    bool Recoverable
);
