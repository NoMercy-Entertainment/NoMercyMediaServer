namespace NoMercy.Encoder.Execution;

using NoMercy.Encoder.Errors;

public record ExecutionResult(
    bool Success,
    int ExitCode,
    string StdErr,
    TimeSpan Duration,
    EncodingError? Error
);
