using NoMercy.Encoder.Errors;

namespace NoMercy.Encoder.Pipeline;

public abstract record StageResult;

public sealed record StageSuccess<T>(T Value) : StageResult;

public sealed record StageFailure(EncodingError Error) : StageResult;
