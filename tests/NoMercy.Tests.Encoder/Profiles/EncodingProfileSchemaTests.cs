namespace NoMercy.Tests.Encoder.Profiles;

using Newtonsoft.Json;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

public class EncodingProfileSchemaTests
{
    private static EncodingProfile BuildMinimalProfile() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Legacy",
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: []
        );

    private static VideoOutput BuildMinimalVideoOutput() =>
        new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 4000,
            Crf: 23,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );

    [Fact]
    public void EncodingProfile_default_construction_keeps_existing_positional_args_valid()
    {
        EncodingProfile profile = BuildMinimalProfile();

        profile.SegmentDurationSeconds.Should().Be(6);
        profile.EncodeMode.Should().Be(EncodeMode.SinglePass);
        profile.AutoLadder.Should().BeFalse();
        profile.AutoDetectCrop.Should().BeFalse();
        profile.Drm.Should().BeNull();
        profile.SchemaVersion.Should().Be(1);

        // new init-only fields must carry their declared defaults
        profile.Description.Should().BeNull();
        profile.ParentId.Should().BeNull();
        profile.IsBuiltin.Should().BeFalse();
        profile.HardwarePreference.Should().Be(HardwarePreference.PreferHardware);
        profile.BitDepthPolicy.Should().Be(BitDepthPolicy.WarnAndDowngrade);
        profile.HdrPolicy.Should().Be(HdrPolicy.PassthroughWhenPossible);
        profile.TonemapAlgorithm.Should().BeNull();
        profile.HdrOptions.Should().BeNull();
        profile.LadderRungs.Should().BeNull();
        profile.PublisherKeyFingerprint.Should().BeNull();
        profile.Signature.Should().BeNull();
        profile.CustomArguments.Should().BeNull();
    }

    [Fact]
    public void EncodingProfile_with_expression_can_set_new_fields()
    {
        Ulid parentId = Ulid.NewUlid();

        EncodingProfile profile = BuildMinimalProfile() with
        {
            ParentId = parentId,
            HardwarePreference = HardwarePreference.ForceSoftware,
            BitDepthPolicy = BitDepthPolicy.Strict,
            HdrPolicy = HdrPolicy.AlwaysTonemap,
            Description = "Derived profile",
            IsBuiltin = true,
        };

        profile.ParentId.Should().Be(parentId);
        profile.HardwarePreference.Should().Be(HardwarePreference.ForceSoftware);
        profile.BitDepthPolicy.Should().Be(BitDepthPolicy.Strict);
        profile.HdrPolicy.Should().Be(HdrPolicy.AlwaysTonemap);
        profile.Description.Should().Be("Derived profile");
        profile.IsBuiltin.Should().BeTrue();
    }

    [Fact]
    public void VideoOutput_BitDepth_PixelFormat_default_to_null()
    {
        VideoOutput output = BuildMinimalVideoOutput();

        output.BitDepth.Should().BeNull();
        output.PixelFormat.Should().BeNull();
        output.MaxBitrateKbps.Should().BeNull();
        output.BufferSizeKbps.Should().BeNull();
        output.RateControlMode.Should().Be(RateControlMode.Crf);
        output.CodecProfile.Should().Be(CodecProfile.Auto);
    }

    [Fact]
    public void AudioOutput_DefaultLanguage_default_null()
    {
        AudioOutput audio = new(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );

        audio.DefaultLanguage.Should().BeNull();
    }

    [Fact]
    public void SubtitleOutput_IncludeForced_default_false()
    {
        SubtitleOutput subtitle = new(
            Codec: SubtitleCodecType.WebVtt,
            Mode: SubtitleMode.Extract,
            AllowedLanguages: ["en"]
        );

        subtitle.IncludeForced.Should().BeFalse();
        subtitle.OcrLanguage.Should().BeNull();
    }

    [Fact]
    public void HlsOptions_default_PlaylistType_is_vod()
    {
        HlsOptions opts = new();

        opts.PlaylistType.Should().Be("vod");
    }

    [Fact]
    public void HlsOptions_default_SegmentType_is_mpegts()
    {
        HlsOptions opts = new();

        opts.SegmentType.Should().Be("mpegts");
    }

    [Fact]
    public void HlsOptions_with_fmp4_round_trips_through_with_expression()
    {
        HlsOptions original = new();
        HlsOptions fmp4 = original with { SegmentType = "fmp4", IndependentSegments = true };

        fmp4.SegmentType.Should().Be("fmp4");
        fmp4.IndependentSegments.Should().BeTrue();
        fmp4.PlaylistType.Should().Be("vod");
    }

    [Fact]
    public void EncodingProfile_round_trips_through_Newtonsoft_JSON_with_new_fields_present()
    {
        Ulid parentId = Ulid.NewUlid();
        Ulid profileId = Ulid.NewUlid();

        EncodingProfile original = new(
            Id: profileId,
            Name: "Full Profile",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                BuildMinimalVideoOutput() with
                {
                    BitDepth = 10,
                    PixelFormat = "yuv420p10le",
                    MaxBitrateKbps = 6000,
                    BufferSizeKbps = 12000,
                    RateControlMode = RateControlMode.CrfCapped,
                    CodecProfile = CodecProfile.High10,
                },
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["en"]
                )
                {
                    DefaultLanguage = "en",
                },
            ],
            SubtitleOutputs:
            [
                new(
                    Codec: SubtitleCodecType.WebVtt,
                    Mode: SubtitleMode.Extract,
                    AllowedLanguages: ["en"]
                )
                {
                    IncludeForced = true,
                    OcrLanguage = "eng",
                },
            ]
        )
        {
            ParentId = parentId,
            HardwarePreference = HardwarePreference.ForceHardware,
            BitDepthPolicy = BitDepthPolicy.PreferSoftware,
            HdrPolicy = HdrPolicy.AlwaysTonemap,
            TonemapAlgorithm = "hable",
            HdrOptions = new HdrOptions("hable", 1000, "/luts/rec709.cube"),
            LadderRungs = [new LadderRung(1920, 1080, 4000), new LadderRung(1280, 720, 2500)],
            PublisherKeyFingerprint = "abc123",
            Signature = "sig456",
            Description = "Round-trip test profile",
            IsBuiltin = true,
        };

        string json = JsonConvert.SerializeObject(original);
        EncodingProfile? restored = JsonConvert.DeserializeObject<EncodingProfile>(json);

        restored.Should().NotBeNull();
        restored!.Id.Should().Be(profileId);
        restored.ParentId.Should().Be(parentId);
        restored.HardwarePreference.Should().Be(HardwarePreference.ForceHardware);
        restored.BitDepthPolicy.Should().Be(BitDepthPolicy.PreferSoftware);
        restored.HdrPolicy.Should().Be(HdrPolicy.AlwaysTonemap);
        restored.TonemapAlgorithm.Should().Be("hable");
        restored.HdrOptions.Should().NotBeNull();
        restored.HdrOptions!.TonemapAlgorithm.Should().Be("hable");
        restored.HdrOptions.TonemapPeakNits.Should().Be(1000);
        restored.HdrOptions.LutPath.Should().Be("/luts/rec709.cube");
        restored.LadderRungs.Should().HaveCount(2);
        restored.LadderRungs![0].Width.Should().Be(1920);
        restored.LadderRungs![1].BitrateKbps.Should().Be(2500);
        restored.PublisherKeyFingerprint.Should().Be("abc123");
        restored.Signature.Should().Be("sig456");
        restored.Description.Should().Be("Round-trip test profile");
        restored.IsBuiltin.Should().BeTrue();

        restored.VideoOutputs[0].BitDepth.Should().Be(10);
        restored.VideoOutputs[0].PixelFormat.Should().Be("yuv420p10le");
        restored.VideoOutputs[0].MaxBitrateKbps.Should().Be(6000);
        restored.VideoOutputs[0].BufferSizeKbps.Should().Be(12000);
        restored.VideoOutputs[0].RateControlMode.Should().Be(RateControlMode.CrfCapped);
        restored.VideoOutputs[0].CodecProfile.Should().Be(CodecProfile.High10);

        restored.AudioOutputs[0].DefaultLanguage.Should().Be("en");

        restored.SubtitleOutputs[0].IncludeForced.Should().BeTrue();
        restored.SubtitleOutputs[0].OcrLanguage.Should().Be("eng");
    }

    [Fact]
    public void EncodingProfile_legacy_JSON_without_new_fields_deserialises_with_defaults()
    {
        // Simulate a JSON blob written before Phase 2.1 by serialising a minimal profile
        // and then removing the new fields, leaving only the pre-2.1 positional properties.
        EncodingProfile source = BuildMinimalProfile();
        string fullJson = JsonConvert.SerializeObject(source);

        Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(fullJson);
        string[] newFields =
        [
            "Description",
            "ParentId",
            "IsBuiltin",
            "HardwarePreference",
            "BitDepthPolicy",
            "HdrPolicy",
            "TonemapAlgorithm",
            "HdrOptions",
            "LadderRungs",
            "PublisherKeyFingerprint",
            "Signature",
            "CustomArguments",
        ];
        foreach (string field in newFields)
            obj.Remove(field);

        string legacyJson = obj.ToString();

        EncodingProfile? profile = JsonConvert.DeserializeObject<EncodingProfile>(legacyJson);

        profile.Should().NotBeNull();
        profile!.Description.Should().BeNull();
        profile.ParentId.Should().BeNull();
        profile.IsBuiltin.Should().BeFalse();
        profile.HardwarePreference.Should().Be(HardwarePreference.PreferHardware);
        profile.BitDepthPolicy.Should().Be(BitDepthPolicy.WarnAndDowngrade);
        profile.HdrPolicy.Should().Be(HdrPolicy.PassthroughWhenPossible);
        profile.TonemapAlgorithm.Should().BeNull();
        profile.HdrOptions.Should().BeNull();
        profile.LadderRungs.Should().BeNull();
        profile.PublisherKeyFingerprint.Should().BeNull();
        profile.Signature.Should().BeNull();
        profile.CustomArguments.Should().BeNull();
    }
}
