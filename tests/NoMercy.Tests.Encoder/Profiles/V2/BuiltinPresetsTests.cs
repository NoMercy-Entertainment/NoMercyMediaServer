using FluentAssertions;
using NoMercy.Encoder.Profiles.V2;

namespace NoMercy.Tests.Encoder.Profiles.V2;

public class BuiltinPresetsTests
{
    [Fact]
    public void All_returns_22_presets()
    {
        EncodingProfile[] all = BuiltinPresets.All();
        all.Length.Should().Be(22);
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
    public void Named_preset_exists(string name)
    {
        BuiltinPresets.All().Should().Contain(p => p.Name == name);
    }
}
