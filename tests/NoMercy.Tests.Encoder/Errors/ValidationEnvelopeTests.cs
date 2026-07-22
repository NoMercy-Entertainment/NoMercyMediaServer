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

using System.Reflection;
using NoMercy.Encoder.Errors;

namespace NoMercy.Tests.Encoder.Errors;

public class ValidationEnvelopeTests
{
    [Fact]
    public void Ok_returns_empty_valid_envelope()
    {
        ValidationEnvelope env = ValidationEnvelope.Ok();

        env.Valid.Should().BeTrue();
        env.Errors.Should().BeEmpty();
        env.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void FromRules_buckets_by_severity_and_marks_valid_when_no_errors()
    {
        EncoderRule warning = new(
            Id: EncoderRuleId.SubtitlesBurnInPermanent,
            Severity: EncoderRuleSeverity.Warning,
            Field: "subtitle_outputs[0].mode",
            Message: "Burn-in is permanent.",
            Fix: "Switch to extract or copy if you might change subtitle later."
        );
        EncoderRule info = new(
            Id: EncoderRuleId.SubtitlesAssNeedsCapableClient,
            Severity: EncoderRuleSeverity.Info,
            Field: "subtitle_outputs[0].codec",
            Message: "ASS requires a capable client.",
            Fix: "NoMercy player handles ASS via libass-wasm."
        );

        ValidationEnvelope env = ValidationEnvelope.FromRules(rules: [warning, info]);

        env.Valid.Should().BeTrue();
        env.Errors.Should().BeEmpty();
        env.Warnings.Should().HaveCount(expected: 2);
    }

    [Fact]
    public void FromRules_marks_invalid_when_any_error_present()
    {
        EncoderRule error = new(
            Id: EncoderRuleId.ProfileNameMissing,
            Severity: EncoderRuleSeverity.Error,
            Field: "name",
            Message: "Profile must have a name.",
            Fix: "Set the name field to a non-empty string."
        );
        EncoderRule warning = new(
            Id: EncoderRuleId.CrfOutOfTypicalRange,
            Severity: EncoderRuleSeverity.Warning,
            Field: "video_profiles[0].crf",
            Message: "CRF outside the typical 18-28 range for H.264.",
            Fix: "Lower CRF for higher quality, raise it to shrink output."
        );

        ValidationEnvelope env = ValidationEnvelope.FromRules(rules: [error, warning]);

        env.Valid.Should().BeFalse();
        env.Errors.Should().ContainSingle().Which.Id.Should().Be(expected: EncoderRuleId.ProfileNameMissing);
        env.Warnings.Should()
            .ContainSingle()
            .Which.Id.Should()
            .Be(expected: EncoderRuleId.CrfOutOfTypicalRange);
    }

    [Fact]
    public void FromRules_handles_empty_input()
    {
        ValidationEnvelope env = ValidationEnvelope.FromRules(rules: []);

        env.Valid.Should().BeTrue();
        env.Errors.Should().BeEmpty();
        env.Warnings.Should().BeEmpty();
    }
}

public class EncoderRuleIdCatalogueTests
{
    [Fact]
    public void Every_catalogued_id_is_lowercase_dotted()
    {
        // Pin the dot-separated lowercase convention so a refactor can't
        // sneak a "ProfileNameMissing" or "PROFILE_NAME_MISSING" in.
        FieldInfo[] fields = typeof(EncoderRuleId).GetFields(
            bindingAttr: BindingFlags.Public | BindingFlags.Static
        );

        fields.Should().NotBeEmpty();
        foreach (FieldInfo f in fields)
        {
            string value = (string)f.GetValue(obj: null)!;
            value
                .Should()
                .MatchRegex(
                    regularExpression: "^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*)*$",
                    because: $"rule id '{f.Name}' = '{value}' must be lowercase snake_case, optionally dotted"
                );
        }
    }

    [Fact]
    public void Every_catalogued_id_is_unique()
    {
        FieldInfo[] fields = typeof(EncoderRuleId).GetFields(
            bindingAttr: BindingFlags.Public | BindingFlags.Static
        );

        IEnumerable<string> values = fields.Select(selector: f => (string)f.GetValue(obj: null)!);
        values.Should().OnlyHaveUniqueItems();
    }
}

public class EncoderErrorShapeTests
{
    [Fact]
    public void Records_round_trip_through_value_equality()
    {
        EncoderErrorShape a = new(
            Id: EncoderRuleId.GpuCapacityExhausted,
            Message: "All NVENC slots full.",
            Suggestion: "Wait or switch to CPU.",
            Details: new { gpu = "RTX 4090", sessions = 3 }
        );
        EncoderErrorShape b = a with { Suggestion = "Wait or switch to CPU." };

        a.Should().Be(expected: b);
    }
}
