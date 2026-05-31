namespace NoMercy.Encoder.Pipeline.Stages;

/// <summary>
/// Named alias for the validation stage contract. Resolving
/// <see cref="IValidationStage"/> from DI returns the same instance as
/// <see cref="ValidateStage"/>. Strategies and plugins that need to
/// swap in a custom validation implementation can register a replacement
/// against this interface without coupling to the concrete class name.
/// </summary>
public interface IValidationStage : IPipelineStage<ValidateInput, ValidateInput> { }
