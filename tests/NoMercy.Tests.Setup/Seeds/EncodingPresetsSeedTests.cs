using Newtonsoft.Json;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;
using NoMercy.Service.Seeds;

namespace NoMercy.Tests.Setup.Seeds;

/// <summary>
/// Every built-in preset must (a) deserialize into a valid EncodingProfile,
/// (b) pass ProfileValidator without ERROR-severity issues, (c) carry a
/// stable Ulid so repeat seeding upserts rather than duplicating rows.
/// These tests break loudly at CI time when a future edit accidentally
/// ships a broken template — much better than the seed swallowing the
/// exception and the preset going missing on every install.
/// </summary>
public class EncodingPresetsSeedTests
{
    private readonly ProfileValidator _validator = new(new CodecRegistry());

    [Fact]
    public void AllBuiltInPresets_HaveStableIds_AndAreMarkedBuiltIn()
    {
        EncodingPreset[] presets = EncodingPresetsSeed.BuildBuiltInPresets();

        Assert.NotEmpty(presets);
        foreach (EncodingPreset preset in presets)
        {
            Assert.NotEqual(default, preset.Id);
            Assert.True(preset.IsBuiltIn, $"{preset.Name} must be marked IsBuiltIn");
            Assert.False(string.IsNullOrWhiteSpace(preset.Name));
            Assert.False(string.IsNullOrWhiteSpace(preset.ProfileJson));
        }
    }

    [Fact]
    public void AllBuiltInPresets_HaveUniqueIds()
    {
        EncodingPreset[] presets = EncodingPresetsSeed.BuildBuiltInPresets();
        Ulid[] ids = presets.Select(p => p.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void AllBuiltInPresets_HaveUniqueNames()
    {
        // Duplicate names would be confusing in the picker. Repository's
        // GetByNameAsync also relies on name uniqueness.
        EncodingPreset[] presets = EncodingPresetsSeed.BuildBuiltInPresets();
        string[] names = presets.Select(p => p.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    [Fact]
    public void BuildBuiltInPresets_IsIdempotent()
    {
        EncodingPreset[] a = EncodingPresetsSeed.BuildBuiltInPresets();
        EncodingPreset[] b = EncodingPresetsSeed.BuildBuiltInPresets();

        Assert.Equal(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++)
        {
            Assert.Equal(a[i].Id, b[i].Id);
            Assert.Equal(a[i].Name, b[i].Name);
            Assert.Equal(a[i].ProfileJson, b[i].ProfileJson);
        }
    }

    [Fact]
    public void AllBuiltInPresets_DeserializeToValidProfiles()
    {
        EncodingPreset[] presets = EncodingPresetsSeed.BuildBuiltInPresets();

        foreach (EncodingPreset preset in presets)
        {
            EncodingProfile? profile = JsonConvert.DeserializeObject<EncodingProfile>(
                preset.ProfileJson
            );
            Assert.NotNull(profile);
            Assert.False(
                string.IsNullOrWhiteSpace(profile!.Name),
                $"{preset.Name}: deserialized profile Name is blank"
            );
        }
    }

    [Fact]
    public void AllBuiltInPresets_PassValidationWithoutErrors()
    {
        EncodingPreset[] presets = EncodingPresetsSeed.BuildBuiltInPresets();

        foreach (EncodingPreset preset in presets)
        {
            EncodingProfile? profile = JsonConvert.DeserializeObject<EncodingProfile>(
                preset.ProfileJson
            );
            Assert.NotNull(profile);

            ValidationResult result = _validator.Validate(profile!);
            ValidationError[] errors = result
                .Errors.Where(e => e.Severity == ValidationSeverity.Error)
                .ToArray();
            Assert.True(
                errors.Length == 0,
                $"{preset.Name} failed validation: "
                    + string.Join(", ", errors.Select(e => $"{e.Field}={e.Message}"))
            );
            Assert.True(result.IsValid, preset.Name);
        }
    }

    [Fact]
    public void AnimePreset_CarriesAnimationTune()
    {
        EncodingPreset[] presets = EncodingPresetsSeed.BuildBuiltInPresets();
        EncodingPreset anime = presets.First(p => p.Name == "Anime — 1080p");

        EncodingProfile profile = JsonConvert.DeserializeObject<EncodingProfile>(
            anime.ProfileJson
        )!;
        Assert.Equal("animation", profile.VideoOutputs[0].Tune);
    }

    [Fact]
    public void ChromecastPreset_TargetsLevel41()
    {
        EncodingPreset[] presets = EncodingPresetsSeed.BuildBuiltInPresets();
        EncodingPreset cc = presets.First(p => p.Name.Contains("Chromecast"));

        EncodingProfile profile = JsonConvert.DeserializeObject<EncodingProfile>(cc.ProfileJson)!;
        Assert.Equal(VideoCodecType.H264, profile.VideoOutputs[0].Codec);
        Assert.Equal("4.1", profile.VideoOutputs[0].Level);
        Assert.Equal(1920, profile.VideoOutputs[0].Width);
    }

    [Fact]
    public void AppleTv4KPreset_UsesHevcMain10AndEac3()
    {
        EncodingPreset[] presets = EncodingPresetsSeed.BuildBuiltInPresets();
        EncodingPreset atv = presets.First(p => p.Name.Contains("Apple TV"));

        EncodingProfile profile = JsonConvert.DeserializeObject<EncodingProfile>(atv.ProfileJson)!;
        Assert.Equal(VideoCodecType.H265, profile.VideoOutputs[0].Codec);
        Assert.Equal("main10", profile.VideoOutputs[0].Profile);
        Assert.True(profile.VideoOutputs[0].TenBit);
        Assert.Equal(AudioCodecType.Eac3, profile.AudioOutputs[0].Codec);
    }

    [Fact]
    public void MobileLowPreset_UsesBitrateMode()
    {
        // Mobile preset pins file size via bitrate, not CRF — the bandwidth
        // budget matters more than perfect quality.
        EncodingPreset[] presets = EncodingPresetsSeed.BuildBuiltInPresets();
        EncodingPreset mobile = presets.First(p => p.Name.Contains("Mobile"));

        EncodingProfile profile = JsonConvert.DeserializeObject<EncodingProfile>(
            mobile.ProfileJson
        )!;
        Assert.Equal(0, profile.VideoOutputs[0].Crf);
        Assert.True(profile.VideoOutputs[0].BitrateKbps > 0);
        Assert.Equal("3.1", profile.VideoOutputs[0].Level);
    }

    [Fact]
    public void ArchivalPresets_UseLosslessOrNearLosslessAudio()
    {
        // Both archival presets (1080p + 4K) should keep audio lossless or
        // near it. User can always re-encode from archive; losing audio
        // fidelity on the archive copy is unrecoverable.
        EncodingPreset[] presets = EncodingPresetsSeed.BuildBuiltInPresets();
        EncodingPreset uhd = presets.First(p => p.Name.Contains("4K Archival"));

        EncodingProfile profile = JsonConvert.DeserializeObject<EncodingProfile>(uhd.ProfileJson)!;
        Assert.Equal(AudioCodecType.Flac, profile.AudioOutputs[0].Codec);
    }

    [Fact]
    public void AnimeHevc10bitPreset_IsMkvForAssSubtitles()
    {
        EncodingPreset[] presets = EncodingPresetsSeed.BuildBuiltInPresets();
        EncodingPreset anime = presets.First(p => p.Name.Contains("HEVC"));

        EncodingProfile profile = JsonConvert.DeserializeObject<EncodingProfile>(
            anime.ProfileJson
        )!;
        Assert.Equal(OutputFormat.Mkv, profile.Format);
        Assert.Equal(VideoCodecType.H265, profile.VideoOutputs[0].Codec);
        Assert.True(profile.VideoOutputs[0].TenBit);
    }

    [Fact]
    public void MusicAacPreset_IsAudioOnlyInMp4()
    {
        EncodingPreset[] presets = EncodingPresetsSeed.BuildBuiltInPresets();
        EncodingPreset music = presets.First(p => p.Name.Contains("AAC"));

        EncodingProfile profile = JsonConvert.DeserializeObject<EncodingProfile>(
            music.ProfileJson
        )!;
        Assert.Empty(profile.VideoOutputs);
        Assert.Empty(profile.SubtitleOutputs);
        Assert.Single(profile.AudioOutputs);
        Assert.Equal(OutputFormat.Mp4, profile.Format);
        Assert.Equal(AudioCodecType.Aac, profile.AudioOutputs[0].Codec);
    }

    [Fact]
    public void MusicMp3Preset_IsAudioOnlyInMp3()
    {
        EncodingPreset[] presets = EncodingPresetsSeed.BuildBuiltInPresets();
        EncodingPreset music = presets.First(p => p.Name.Contains("MP3"));

        EncodingProfile profile = JsonConvert.DeserializeObject<EncodingProfile>(
            music.ProfileJson
        )!;
        Assert.Empty(profile.VideoOutputs);
        Assert.Empty(profile.SubtitleOutputs);
        Assert.Single(profile.AudioOutputs);
        Assert.Equal(OutputFormat.Mp3, profile.Format);
        Assert.Equal(AudioCodecType.Mp3, profile.AudioOutputs[0].Codec);
        Assert.Equal(320, profile.AudioOutputs[0].BitrateKbps);
    }

    [Fact]
    public void MusicFlacPreset_IsAudioOnlyInFlac()
    {
        EncodingPreset[] presets = EncodingPresetsSeed.BuildBuiltInPresets();
        EncodingPreset music = presets.First(p => p.Name.Contains("FLAC Lossless"));

        EncodingProfile profile = JsonConvert.DeserializeObject<EncodingProfile>(
            music.ProfileJson
        )!;
        Assert.Empty(profile.VideoOutputs);
        Assert.Empty(profile.SubtitleOutputs);
        Assert.Single(profile.AudioOutputs);
        Assert.Equal(OutputFormat.Flac, profile.Format);
        Assert.Equal(AudioCodecType.Flac, profile.AudioOutputs[0].Codec);
    }
}
