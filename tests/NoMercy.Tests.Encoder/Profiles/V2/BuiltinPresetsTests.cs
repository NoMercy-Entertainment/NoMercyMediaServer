using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles.V2;

public class BuiltinPresetsTests
{
    [Fact]
    public void AbrStandard_uses_Auto_mode_with_Standard_tiers()
    {
        EncodingProfile preset = BuiltinPresets
            .All()
            .Single(p => p.Name == "ABR Standard 480/720/1080");

        preset.Ladder.Should().NotBeNull();
        preset.Ladder!.Mode.Should().Be(LadderMode.Auto);
        preset.Ladder.AutoConfig.Should().NotBeNull();
        preset.Ladder.AutoConfig!.Tiers.Should().BeSameAs(LadderTiers.Standard);
        preset.Ladder.AutoConfig.BitrateStrategy.Should().Be(BitrateStrategy.AppleHlsRecommended);
        preset.Ladder.AutoConfig.CodecPolicy.Should().Be(LadderCodecPolicy.Uniform);
    }

    [Fact]
    public void AbrPremiumHevc_uses_Auto_mode_with_Premium_720p_plus_tiers()
    {
        EncodingProfile preset = BuiltinPresets
            .All()
            .Single(p => p.Name == "ABR Premium HEVC 720/1080/2160");

        preset.Ladder.Should().NotBeNull();
        preset.Ladder!.Mode.Should().Be(LadderMode.Auto);
        preset.Ladder.AutoConfig.Should().NotBeNull();

        AutoLadderConfig config = preset.Ladder.AutoConfig!;
        config.BitrateStrategy.Should().Be(BitrateStrategy.AppleHlsRecommended);
        config.CodecPolicy.Should().Be(LadderCodecPolicy.Uniform);
        config.Crf.Should().Be(20);

        // Tiers = LadderTiers.Premium.Skip(1) → 720p, 1080p, 2160p (no 480p)
        config.Tiers.Should().HaveCount(3);
        config.Tiers.Select(t => t.Label).Should().Equal("720p", "1080p", "2160p");
    }

    [Fact]
    public void All_returns_23_presets()
    {
        EncodingProfile[] all = BuiltinPresets.All();
        all.Length.Should().Be(23);
    }

    [Fact]
    public void All_preset_ulids_are_deterministic_and_unique()
    {
        EncodingProfile[] all = BuiltinPresets.All();
        IEnumerable<Ulid> ids = all.Select(p => p.Id);
        ids.Should().OnlyHaveUniqueItems();

        EncodingProfile[] again = BuiltinPresets.All();
        again.Select(p => p.Id).Should().BeEquivalentTo(ids);
    }

    [Fact]
    public void All_presets_pass_validation()
    {
        foreach (EncodingProfile profile in BuiltinPresets.All())
        {
            ProfileValidationResult result = ProfileValidator.Validate(profile);
            result
                .IsValid.Should()
                .BeTrue(
                    $"preset '{profile.Name}' must be valid; errors: {string.Join("; ", result.Errors)}"
                );
        }
    }

    [Fact]
    public void All_presets_marked_isbuiltin()
    {
        BuiltinPresets.All().Should().AllSatisfy(p => p.IsBuiltin.Should().BeTrue());
    }

    [Theory]
    [InlineData("Web 1080p Balanced")]
    [InlineData("Web 720p Fast")]
    [InlineData("Mobile 480p Low Bandwidth")]
    [InlineData("ABR Standard 480/720/1080")]
    [InlineData("ABR Premium HEVC 720/1080/2160")]
    [InlineData("Anime HEVC 1080p 10-bit")]
    [InlineData("Full Remux HLS")]
    [InlineData("Smart Remux HLS")]
    [InlineData("Direct Stream Audio HLS")]
    [InlineData("Compress HEVC 1080p MKV")]
    [InlineData("Compress HEVC 4K MKV")]
    [InlineData("Compress H.264 1080p MKV")]
    [InlineData("Archive Remux MKV")]
    [InlineData("Music FLAC Lossless")]
    [InlineData("Music AAC 256k")]
    [InlineData("Music MP3 320k")]
    [InlineData("Chromecast 1080p")]
    [InlineData("Chromecast 4K HEVC")]
    [InlineData("Apple TV HD 1080p HEVC")]
    [InlineData("Apple TV 4K HEVC")]
    [InlineData("Legacy Device H.264 Baseline")]
    [InlineData("DASH 1080p Balanced")]
    [InlineData("YouTube ABR Full Range")]
    public void Named_preset_exists(string name)
    {
        BuiltinPresets.All().Should().Contain(p => p.Name == name);
    }
}
