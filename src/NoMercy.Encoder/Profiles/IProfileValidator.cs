using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles.V2;

namespace NoMercy.Encoder.Profiles;

public interface IProfileValidator
{
    /// <summary>Legacy API — retained as a compat shim for existing API controllers.</summary>
    ValidationResult Validate(EncodingProfile profile);

    /// <summary>
    /// New API — returns a <see cref="ValidationEnvelope"/> whose rules each carry a
    /// stable <see cref="EncoderRuleId"/> and an actionable <c>Fix</c> string.
    /// </summary>
    ValidationEnvelope ValidateAsEnvelope(EncodingProfile profile);
}

/// <summary>Legacy result — kept as a compat shim. Do not extend.</summary>
public record ValidationResult(bool IsValid, ValidationError[] Errors)
{
    public static ValidationResult Success() => new(true, []);
}

/// <summary>Legacy error — kept as a compat shim. Do not extend.</summary>
public record ValidationError(string Field, string Message, ValidationSeverity Severity);

/// <summary>Legacy severity — kept as a compat shim.</summary>
public enum ValidationSeverity
{
    Error,
    Warning,
}
