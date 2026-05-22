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
        // Modern catalogued rules first — these carry stable IDs the dashboard deep-links into.
        ValidationEnvelope structured = ProfileRuleValidator.Validate(profile);

        // Legacy string-based validator runs on top so any rule not yet migrated to the
        // catalogued form still surfaces. Bucketed under a generic id until V2 strings are
        // converted to typed rules.
        ProfileValidationResult legacy = V2ProfileValidator.Validate(profile);
        List<EncoderRule> errors = [.. structured.Errors];
        List<EncoderRule> warnings = [.. structured.Warnings];

        foreach (string message in legacy.Errors)
        {
            errors.Add(
                new EncoderRule(
                    EncoderRuleId.ProfileNoOutputs,
                    EncoderRuleSeverity.Error,
                    "profile",
                    message,
                    "Edit the offending field per the message."
                )
            );
        }
        foreach (string message in legacy.Warnings)
        {
            warnings.Add(
                new EncoderRule(
                    EncoderRuleId.ProfileNoOutputs,
                    EncoderRuleSeverity.Warning,
                    "profile",
                    message,
                    "Review the warning and adjust the profile if needed."
                )
            );
        }

        return new ValidationEnvelope(errors.Count == 0, errors, warnings);
    }
}
