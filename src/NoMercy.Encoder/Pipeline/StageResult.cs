namespace NoMercy.Encoder.Pipeline;

using NoMercy.Encoder.Errors;

public abstract record StageResult;

public sealed record StageSuccess<T>(T Value) : StageResult;

public sealed record StageFailure(EncodingError Error) : StageResult;
