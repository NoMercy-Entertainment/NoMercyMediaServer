namespace NoMercy.Encoder.Pipeline.Stages;

/// <summary>
/// Named alias for the planning stage contract. Resolving
/// <see cref="IPlanStage"/> from DI returns the same instance as
/// <see cref="PlanStage"/>. Strategies and plugins that need to
/// swap in a custom planning implementation can register a replacement
/// against this interface without coupling to the concrete class name.
/// </summary>
public interface IPlanStage : IPipelineStage<ValidateInput, ExecutionPlan> { }
