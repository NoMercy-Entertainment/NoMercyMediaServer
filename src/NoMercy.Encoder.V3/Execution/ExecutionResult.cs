namespace NoMercy.Encoder.V3.Execution;

using NoMercy.Encoder.V3.Errors;

public record ExecutionResult(
    bool Success,
    int ExitCode,
    string StdErr,
    TimeSpan Duration,
    EncodingError? Error
);
