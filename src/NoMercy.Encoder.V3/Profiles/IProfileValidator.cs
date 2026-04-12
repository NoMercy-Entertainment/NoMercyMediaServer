namespace NoMercy.Encoder.V3.Profiles;

public interface IProfileValidator
{
    ValidationResult Validate(EncodingProfile profile);
}

public record ValidationResult(bool IsValid, ValidationError[] Errors)
{
    public static ValidationResult Success() => new(true, []);
}

public record ValidationError(string Field, string Message, ValidationSeverity Severity);

public enum ValidationSeverity
{
    Error,
    Warning,
}
