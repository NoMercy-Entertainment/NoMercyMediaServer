// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Encoder.Errors;

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Adapts the static <see cref="ProfileValidator"/> to the injectable
/// <see cref="IProfileValidator"/> surface the API controllers take, and
/// translates its plain-string error/warning lists into the richer
/// <see cref="ValidationEnvelope"/> shape the dashboard renders.
///
/// Everything buckets under <see cref="EncoderRuleId.ProfileNoOutputs"/>:
/// the validator emits free-form strings rather than catalogued rule ids, so
/// there is nothing to deep-link to until it grows a rule registry.
/// </summary>
public sealed class BackedProfileValidator : IProfileValidator
{
    public ValidationResult Validate(EncodingProfile profile)
    {
        ProfileValidationResult result = ProfileValidator.Validate(profile);
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
        ProfileValidationResult legacy = ProfileValidator.Validate(profile);
        List<EncoderRule> errors = [.. structured.Errors];
        List<EncoderRule> warnings = [.. structured.Warnings];

        foreach (string message in legacy.Errors)
        {
            errors.Add(
                new(
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
                new(
                    EncoderRuleId.ProfileNoOutputs,
                    EncoderRuleSeverity.Warning,
                    "profile",
                    message,
                    "Review the warning and adjust the profile if needed."
                )
            );
        }

        return new(errors.Count == 0, errors, warnings);
    }
}
