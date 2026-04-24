namespace NoMercy.Encoder.Pipeline.Stages;

using NoMercy.Encoder.Analysis;

/// <summary>
/// Named alias for the analysis stage contract. Resolving
/// <see cref="IAnalysisStage"/> from DI returns the same instance as
/// <see cref="AnalyzeStage"/>. Strategies and plugins that need to
/// swap in a custom analysis implementation can register a replacement
/// against this interface without coupling to the concrete class name.
/// </summary>
public interface IAnalysisStage : IPipelineStage<string, MediaInfo> { }
