using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

public class BuiltinPresetsTests
{
    private readonly ProfileValidator _validator = new(new());

    [Fact]
    public void All_returns_12_presets()
    {
        IReadOnlyList<EncodingProfile> presets = BuiltinPresets.All();
        presets.Count.Should().Be(12);
    }

    [Fact]
    public void Each_preset_has_unique_stable_id()
    {
        // Call each factory twice — must return the same Id both times.
        Ulid id1A = BuiltinPresets.GeneralH264_1080p_Fast().Id;
        Ulid id1B = BuiltinPresets.GeneralH264_1080p_Fast().Id;
        id1A.Should().Be(id1B, "built-in IDs must be deterministic across calls");

        Ulid id2A = BuiltinPresets.Web_H264_720p().Id;
        Ulid id2B = BuiltinPresets.Web_H264_720p().Id;
        id2A.Should().Be(id2B);

        // All 12 IDs must be distinct.
        IReadOnlyList<EncodingProfile> all = BuiltinPresets.All();
        HashSet<Ulid> ids = all.Select(p => p.Id).ToHashSet();
        ids.Count.Should().Be(12, "every built-in preset must have a unique Id");
    }

    [Fact]
    public void Each_preset_validates_clean()
    {
        IReadOnlyList<EncodingProfile> presets = BuiltinPresets.All();

        foreach (EncodingProfile preset in presets)
        {
            ValidationEnvelope envelope = _validator.ValidateAsEnvelope(preset);
            Assert.True(
                envelope.Valid,
                $"{preset.Name}: {string.Join(", ", envelope.Errors.Select(e => e.Message))}"
            );
        }
    }

    [Fact]
    public void Each_preset_has_IsBuiltin_true_and_Description_set()
    {
        IReadOnlyList<EncodingProfile> presets = BuiltinPresets.All();

        foreach (EncodingProfile preset in presets)
        {
            preset.IsBuiltin.Should().BeTrue($"{preset.Name} must have IsBuiltin = true");
            preset
                .Description.Should()
                .NotBeNullOrWhiteSpace($"{preset.Name} must have a non-empty Description");
        }
    }

    [Fact]
    public void AppleTV_HEVC_4K_Main10_uses_fmp4_segments()
    {
        EncodingProfile profile = BuiltinPresets.AppleTV_HEVC_4K_Main10();

        profile.CustomArguments.Should().NotBeNull();
        profile.CustomArguments!.Should().ContainKey("hls_segment_type");
        profile.CustomArguments["hls_segment_type"].Should().Be("fmp4");
    }

    [Fact]
    public void Music_AAC_192k_has_no_video_outputs()
    {
        EncodingProfile profile = BuiltinPresets.Music_AAC_192k();

        profile.VideoOutputs.Should().BeEmpty("music-only presets must not carry video outputs");
        profile.AudioOutputs.Should().NotBeEmpty();
    }

    [Fact]
    public void Music_MP3_320k_has_no_video_outputs()
    {
        EncodingProfile profile = BuiltinPresets.Music_MP3_320k();
        profile.VideoOutputs.Should().BeEmpty();
    }

    [Fact]
    public void Music_FLAC_Lossless_has_no_video_outputs()
    {
        EncodingProfile profile = BuiltinPresets.Music_FLAC_Lossless();
        profile.VideoOutputs.Should().BeEmpty();
    }

    [Fact]
    public void Archival_HEVC_4K_Main10_uses_TwoPass()
    {
        EncodingProfile profile = BuiltinPresets.Archival_HEVC_4K_Main10();
        profile.EncodeMode.Should().Be(EncodeMode.TwoPass);
    }
}
