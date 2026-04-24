namespace NoMercy.Encoder.Pipeline.Stages;

using NoMercy.Encoder.Execution;

/// <summary>
/// Named alias for the execution stage contract. Resolving
/// <see cref="IExecutionStage"/> from DI returns the same instance as
/// <see cref="ExecuteStage"/>. Strategies and plugins that need to
/// swap in a custom execution implementation can register a replacement
/// against this interface without coupling to the concrete class name.
/// </summary>
public interface IExecutionStage : IPipelineStage<ExecuteInput, ExecutionResult[]> { }
