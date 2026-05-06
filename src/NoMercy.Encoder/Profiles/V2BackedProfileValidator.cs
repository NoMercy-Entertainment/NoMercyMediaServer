using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;
using V2ProfileValidator = NoMercy.Encoder.Profiles.ProfileValidator;

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// V2-backed implementation of the legacy <see cref="IProfileValidator"/>
/// surface kept for the API controllers that still inject it. Delegates the
/// actual validation to the static V2 <see cref="V2.ProfileValidator"/> and
/// translates the plain-string error/warning lists into the richer
/// <see cref="ValidationEnvelope"/> shape the dashboard renders. The
/// <see cref="EncoderRuleId.ProfileNoOutputs"/> rule id is used as a generic
/// bucket — V2's validator emits free-form strings rather than catalogued
/// rule ids, so we cannot deep-link until V2 grows a rule registry.
/// </summary>
public sealed class V2BackedProfileValidator : IProfileValidator
{
    public ValidationResult Validate(EncodingProfile profile)
    {
        ProfileValidationResult result = V2ProfileValidator.Validate(profile);
        ValidationError[] errors = result
            .Errors.Select(e => new ValidationError("profile", e, ValidationSeverity.Error))
            .Concat(
                result.Warnings.Select(w => new ValidationError(
                    "profile",
                    w,
                    ValidationSeverity.Warning
                ))
            )
            .ToArray();
        return new(result.IsValid, errors);
    }

    public ValidationEnvelope ValidateAsEnvelope(EncodingProfile profile)
    {
        ProfileValidationResult result = V2ProfileValidator.Validate(profile);
        List<EncoderRule> errors = result
            .Errors.Select(e => new EncoderRule(
                EncoderRuleId.ProfileNoOutputs,
                EncoderRuleSeverity.Error,
                "profile",
                e,
                "Edit the offending field per the message."
            ))
            .ToList();
        List<EncoderRule> warnings = result
            .Warnings.Select(w => new EncoderRule(
                EncoderRuleId.ProfileNoOutputs,
                EncoderRuleSeverity.Warning,
                "profile",
                w,
                "Review the warning and adjust the profile if needed."
            ))
            .ToList();
        return new(errors.Count == 0, errors, warnings);
    }
}
