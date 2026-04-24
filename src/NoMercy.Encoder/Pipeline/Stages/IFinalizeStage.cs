namespace NoMercy.Encoder.Pipeline.Stages;

/// <summary>
/// Named alias for the finalization stage contract. Resolving
/// <see cref="IFinalizeStage"/> from DI returns the same instance as
/// <see cref="FinalizeStage"/>. Strategies and plugins that need to
/// swap in a custom finalization implementation can register a replacement
/// against this interface without coupling to the concrete class name.
/// </summary>
public interface IFinalizeStage : IPipelineStage<FinalizeInput, FinalizeOutput> { }
