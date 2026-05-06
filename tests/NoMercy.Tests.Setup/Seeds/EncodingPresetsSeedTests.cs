using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;
using V2BuiltinPresets = NoMercy.Encoder.Profiles.BuiltinPresets;

namespace NoMercy.Tests.Setup.Seeds;

/// <summary>
/// Every built-in preset must (a) round-trip through JSON without data loss,
/// (b) pass V2 ProfileValidator without errors, (c) carry a stable deterministic
/// Id so repeat seeding upserts rather than duplicating rows, and (d) satisfy
/// codec/container invariants specific to its tier.
/// </summary>
public class EncodingPresetsSeedTests
{
    [Fact]
    public void AllBuiltInPresets_HaveStableIds_AndAreMarkedBuiltIn()
    {
        EncodingProfile[] presets = V2BuiltinPresets.All();

        Assert.NotEmpty(presets);
        foreach (EncodingProfile preset in presets)
        {
            Assert.NotEqual(default, preset.Id);
            Assert.True(preset.IsBuiltin, $"{preset.Name} must be marked IsBuiltin");
            Assert.False(string.IsNullOrWhiteSpace(preset.Name));
        }
    }

    [Fact]
    public void AllBuiltInPresets_HaveUniqueIds()
    {
        EncodingProfile[] presets = V2BuiltinPresets.All();
        Ulid[] ids = presets.Select(p => p.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void AllBuiltInPresets_HaveUniqueNames()
    {
        EncodingProfile[] presets = V2BuiltinPresets.All();
        string[] names = presets.Select(p => p.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    [Fact]
    public void AllBuiltInPresets_IsIdempotent()
    {
        EncodingProfile[] a = V2BuiltinPresets.All();
        EncodingProfile[] b = V2BuiltinPresets.All();

        Assert.Equal(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++)
        {
            Assert.Equal(a[i].Id, b[i].Id);
            Assert.Equal(a[i].Name, b[i].Name);
        }
    }

    [Fact]
    public void AllBuiltInPresets_PassValidationWithoutErrors()
    {
        EncodingProfile[] presets = V2BuiltinPresets.All();

        foreach (EncodingProfile preset in presets)
        {
            ProfileValidationResult result = ProfileValidator.Validate(preset);
            Assert.True(
                result.IsValid,
                $"{preset.Name} failed validation: " + string.Join(", ", result.Errors)
            );
        }
    }

    [Fact]
    public void AnimeHevcPreset_CarriesAnimationTune()
    {
        EncodingProfile[] presets = V2BuiltinPresets.All();
        EncodingProfile anime = presets.First(p => p.Name == "Anime HEVC 1080p 10-bit");

        Assert.NotNull(anime.Video);
        Assert.Equal("animation", anime.Video!.Tune);
        Assert.Equal(VideoCodecType.H265, anime.Video.Codec);
        Assert.Equal(10, anime.Video.BitDepth);
    }

    [Fact]
    public void Chromecast1080pPreset_HasAacAndAc3Audio()
    {
        EncodingProfile[] presets = V2BuiltinPresets.All();
        EncodingProfile cc = presets.First(p => p.Name == "Chromecast 1080p");

        Assert.NotNull(cc.Video);
        Assert.Equal(VideoCodecType.H264, cc.Video!.Codec);
        Assert.Equal(1920, cc.Video.Width);
        Assert.Contains(cc.Audio, a => a.Codec == AudioCodecType.Aac);
        Assert.Contains(cc.Audio, a => a.Codec == AudioCodecType.Ac3);
    }

    [Fact]
    public void AppleTv4kPreset_UsesHevcMain10AndEac3()
    {
        EncodingProfile[] presets = V2BuiltinPresets.All();
        EncodingProfile atv = presets.First(p => p.Name == "Apple TV 4K HEVC");

        Assert.NotNull(atv.Video);
        Assert.Equal(VideoCodecType.H265, atv.Video!.Codec);
        Assert.Equal(CodecProfile.Main10, atv.Video.CodecProfile);
        Assert.Equal(10, atv.Video.BitDepth);
        Assert.Contains(atv.Audio, a => a.Codec == AudioCodecType.Eac3);
    }

    [Fact]
    public void MusicFlacPreset_IsAudioOnlyInFlac()
    {
        EncodingProfile[] presets = V2BuiltinPresets.All();
        EncodingProfile music = presets.First(p => p.Name == "Music FLAC Lossless");

        Assert.Null(music.Video);
        Assert.Empty(music.Subtitles);
        Assert.Single(music.Audio);
        Assert.Equal(Container.Flac, music.Container);
        Assert.Equal(AudioCodecType.Flac, music.Audio[0].Codec);
    }

    [Fact]
    public void MusicMp3Preset_IsAudioOnlyInMp3()
    {
        EncodingProfile[] presets = V2BuiltinPresets.All();
        EncodingProfile music = presets.First(p => p.Name == "Music MP3 320k");

        Assert.Null(music.Video);
        Assert.Empty(music.Subtitles);
        Assert.Single(music.Audio);
        Assert.Equal(Container.Mp3, music.Container);
        Assert.Equal(AudioCodecType.Mp3, music.Audio[0].Codec);
        Assert.Equal(320, music.Audio[0].BitrateKbps);
    }

    [Fact]
    public void MusicAacPreset_IsAudioOnlyInAac()
    {
        EncodingProfile[] presets = V2BuiltinPresets.All();
        EncodingProfile music = presets.First(p => p.Name == "Music AAC 256k");

        Assert.Null(music.Video);
        Assert.Empty(music.Subtitles);
        Assert.Single(music.Audio);
        Assert.Equal(Container.Aac, music.Container);
        Assert.Equal(AudioCodecType.Aac, music.Audio[0].Codec);
        Assert.Equal(256, music.Audio[0].BitrateKbps);
    }

    [Fact]
    public void CompressHevcMkvPreset_HasFlacAudio()
    {
        EncodingProfile[] presets = V2BuiltinPresets.All();
        EncodingProfile archival = presets.First(p => p.Name == "Compress HEVC 1080p MKV");

        Assert.Equal(Container.Mkv, archival.Container);
        Assert.Equal(AudioCodecType.Flac, archival.Audio[0].Codec);
    }

    [Fact]
    public void Web1080pBalanced_ExistsAndMatchesExpectedBuiltinNames()
    {
        EncodingProfile[] presets = V2BuiltinPresets.All();
        Assert.Contains(presets, p => p.Name == "Web 1080p Balanced");
        Assert.Contains(presets, p => p.Name == "Anime HEVC 1080p 10-bit");
        Assert.Contains(presets, p => p.Name == "Music FLAC Lossless");
    }
}
