namespace NoMercy.Encoder.V3.Pipeline;

using NoMercy.Encoder.V3.Errors;

public abstract record StageResult;

public sealed record StageSuccess<T>(T Value) : StageResult;

public sealed record StageFailure(EncodingError Error) : StageResult;
