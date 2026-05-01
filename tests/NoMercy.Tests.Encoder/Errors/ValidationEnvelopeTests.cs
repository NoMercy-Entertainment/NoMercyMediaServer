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
            EncoderRuleId.SubtitlesBurnInPermanent,
            EncoderRuleSeverity.Warning,
            "subtitle_outputs[0].mode",
            "Burn-in is permanent.",
            "Switch to extract or copy if you might change subtitle later."
        );
        EncoderRule info = new(
            EncoderRuleId.SubtitlesAssNeedsCapableClient,
            EncoderRuleSeverity.Info,
            "subtitle_outputs[0].codec",
            "ASS requires a capable client.",
            "NoMercy player handles ASS via libass-wasm."
        );

        ValidationEnvelope env = ValidationEnvelope.FromRules([warning, info]);

        env.Valid.Should().BeTrue();
        env.Errors.Should().BeEmpty();
        env.Warnings.Should().HaveCount(2);
    }

    [Fact]
    public void FromRules_marks_invalid_when_any_error_present()
    {
        EncoderRule error = new(
            EncoderRuleId.ProfileNameMissing,
            EncoderRuleSeverity.Error,
            "name",
            "Profile must have a name.",
            "Set the name field to a non-empty string."
        );
        EncoderRule warning = new(
            EncoderRuleId.CrfOutOfTypicalRange,
            EncoderRuleSeverity.Warning,
            "video_profiles[0].crf",
            "CRF outside the typical 18-28 range for H.264.",
            "Lower CRF for higher quality, raise it to shrink output."
        );

        ValidationEnvelope env = ValidationEnvelope.FromRules([error, warning]);

        env.Valid.Should().BeFalse();
        env.Errors.Should().ContainSingle().Which.Id.Should().Be(EncoderRuleId.ProfileNameMissing);
        env.Warnings.Should()
            .ContainSingle()
            .Which.Id.Should()
            .Be(EncoderRuleId.CrfOutOfTypicalRange);
    }

    [Fact]
    public void FromRules_handles_empty_input()
    {
        ValidationEnvelope env = ValidationEnvelope.FromRules([]);

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
            BindingFlags.Public | BindingFlags.Static
        );

        fields.Should().NotBeEmpty();
        foreach (FieldInfo f in fields)
        {
            string value = (string)f.GetValue(null)!;
            value
                .Should()
                .MatchRegex(
                    "^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*)*$",
                    $"rule id '{f.Name}' = '{value}' must be lowercase snake_case, optionally dotted"
                );
        }
    }

    [Fact]
    public void Every_catalogued_id_is_unique()
    {
        FieldInfo[] fields = typeof(EncoderRuleId).GetFields(
            BindingFlags.Public | BindingFlags.Static
        );

        IEnumerable<string> values = fields.Select(f => (string)f.GetValue(null)!);
        values.Should().OnlyHaveUniqueItems();
    }
}

public class EncoderErrorShapeTests
{
    [Fact]
    public void Records_round_trip_through_value_equality()
    {
        EncoderErrorShape a = new(
            EncoderRuleId.GpuCapacityExhausted,
            "All NVENC slots full.",
            "Wait or switch to CPU.",
            new { gpu = "RTX 4090", sessions = 3 }
        );
        EncoderErrorShape b = a with { Suggestion = "Wait or switch to CPU." };

        a.Should().Be(b);
    }
}
