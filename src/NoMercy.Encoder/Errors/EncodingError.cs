namespace NoMercy.Encoder.Errors;

public record EncodingError(
    EncodingErrorKind Kind,
    string Message,
    string? FfmpegStderr,
    string? StageName,
    bool Recoverable
);
