namespace NoMercy.Encoder.Pipeline.Stages;

using NoMercy.Encoder.Commands;

/// <summary>
/// Named alias for the build stage contract. Resolving
/// <see cref="IBuildStage"/> from DI returns the same instance as
/// <see cref="BuildStage"/>. Strategies and plugins that need to
/// swap in a custom command-build implementation can register a replacement
/// against this interface without coupling to the concrete class name.
/// </summary>
public interface IBuildStage : IPipelineStage<BuildInput, FfmpegCommand[]> { }
