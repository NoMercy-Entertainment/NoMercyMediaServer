namespace NoMercy.Tests.Encoder.V3.Profiles;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Profiles;

public class EncodingProfileTests
{
    [Fact]
    public void EncodingProfile_RoundTrips_AllFields()
    {
        VideoOutput video = new VideoOutput(
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

        AudioOutput audio = new AudioOutput(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );

        SubtitleOutput subtitle = new SubtitleOutput(
            Codec: SubtitleCodecType.WebVtt,
            Mode: SubtitleMode.Extract,
            AllowedLanguages: ["en"]
        );

        ThumbnailOutput thumbnail = new ThumbnailOutput(Width: 320, IntervalSeconds: 10);

        EncodingProfile profile = new EncodingProfile(
            Id: "test-profile-1",
            Name: "Test Profile",
            Format: OutputFormat.Hls,
            VideoOutputs: [video],
            AudioOutputs: [audio],
            SubtitleOutputs: [subtitle],
            Thumbnails: thumbnail
        );

        profile.Id.Should().Be("test-profile-1");
        profile.Name.Should().Be("Test Profile");
        profile.Format.Should().Be(OutputFormat.Hls);
        profile.VideoOutputs.Should().HaveCount(1);
        profile.VideoOutputs[0].Codec.Should().Be(VideoCodecType.H264);
        profile.VideoOutputs[0].Width.Should().Be(1920);
        profile.VideoOutputs[0].Height.Should().Be(1080);
        profile.VideoOutputs[0].BitrateKbps.Should().Be(4000);
        profile.VideoOutputs[0].Crf.Should().Be(23);
        profile.VideoOutputs[0].Preset.Should().Be("medium");
        profile.VideoOutputs[0].Profile.Should().Be("high");
        profile.VideoOutputs[0].Level.Should().Be("4.1");
        profile.VideoOutputs[0].ConvertHdrToSdr.Should().BeFalse();
        profile.VideoOutputs[0].KeyframeIntervalSeconds.Should().Be(2);
        profile.VideoOutputs[0].TenBit.Should().BeFalse();
        profile.AudioOutputs.Should().HaveCount(1);
        profile.AudioOutputs[0].Codec.Should().Be(AudioCodecType.Aac);
        profile.AudioOutputs[0].BitrateKbps.Should().Be(192);
        profile.AudioOutputs[0].Channels.Should().Be(2);
        profile.AudioOutputs[0].SampleRateHz.Should().Be(48000);
        profile.AudioOutputs[0].AllowedLanguages.Should().Equal("en");
        profile.SubtitleOutputs.Should().HaveCount(1);
        profile.SubtitleOutputs[0].Codec.Should().Be(SubtitleCodecType.WebVtt);
        profile.SubtitleOutputs[0].Mode.Should().Be(SubtitleMode.Extract);
        profile.SubtitleOutputs[0].AllowedLanguages.Should().Equal("en");
        profile.Thumbnails.Should().NotBeNull();
        profile.Thumbnails!.Width.Should().Be(320);
        profile.Thumbnails!.IntervalSeconds.Should().Be(10);
    }

    [Fact]
    public void EncodingProfile_AudioOnly_EmptyVideoArray_IsValid()
    {
        AudioOutput audio = new AudioOutput(
            Codec: AudioCodecType.Flac,
            BitrateKbps: 0,
            Channels: 2,
            SampleRateHz: 44100,
            AllowedLanguages: []
        );

        EncodingProfile profile = new EncodingProfile(
            Id: "audio-only",
            Name: "Audio Only",
            Format: OutputFormat.Mkv,
            VideoOutputs: [],
            AudioOutputs: [audio],
            SubtitleOutputs: []
        );

        profile.VideoOutputs.Should().BeEmpty();
        profile.AudioOutputs.Should().HaveCount(1);
        profile.AudioOutputs[0].Codec.Should().Be(AudioCodecType.Flac);
    }

    [Fact]
    public void EncodingProfile_MultipleVideoOutputs_AllRoundTrip()
    {
        VideoOutput output1080p = new VideoOutput(
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

        VideoOutput output720p = new VideoOutput(
            Codec: VideoCodecType.H264,
            Width: 1280,
            Height: 720,
            BitrateKbps: 2500,
            Crf: 23,
            Preset: "medium",
            Profile: "high",
            Level: "3.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );

        VideoOutput output480p = new VideoOutput(
            Codec: VideoCodecType.H264,
            Width: 854,
            Height: 480,
            BitrateKbps: 1000,
            Crf: 23,
            Preset: "medium",
            Profile: "main",
            Level: "3.0",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );

        EncodingProfile profile = new EncodingProfile(
            Id: "multi-res",
            Name: "Multi-Resolution HLS",
            Format: OutputFormat.Hls,
            VideoOutputs: [output1080p, output720p, output480p],
            AudioOutputs: [],
            SubtitleOutputs: []
        );

        profile.VideoOutputs.Should().HaveCount(3);
        profile.VideoOutputs[0].Width.Should().Be(1920);
        profile.VideoOutputs[0].Height.Should().Be(1080);
        profile.VideoOutputs[1].Width.Should().Be(1280);
        profile.VideoOutputs[1].Height.Should().Be(720);
        profile.VideoOutputs[2].Width.Should().Be(854);
        profile.VideoOutputs[2].Height.Should().Be(480);
    }

    [Fact]
    public void EncodingProfile_ThumbnailOutput_IsOptional_DefaultsToNull()
    {
        EncodingProfile profile = new EncodingProfile(
            Id: "no-thumbs",
            Name: "No Thumbnails",
            Format: OutputFormat.Mp4,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: []
        );

        profile.Thumbnails.Should().BeNull();
    }

    [Fact]
    public void EncodingProfile_MultipleAudioOutputs_DifferentLanguages()
    {
        AudioOutput englishAudio = new AudioOutput(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );

        AudioOutput spanishAudio = new AudioOutput(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["es"]
        );

        AudioOutput frenchAudio = new AudioOutput(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["fr"]
        );

        EncodingProfile profile = new EncodingProfile(
            Id: "multi-lang",
            Name: "Multi-Language",
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [englishAudio, spanishAudio, frenchAudio],
            SubtitleOutputs: []
        );

        profile.AudioOutputs.Should().HaveCount(3);
        profile.AudioOutputs[0].AllowedLanguages.Should().Equal("en");
        profile.AudioOutputs[1].AllowedLanguages.Should().Equal("es");
        profile.AudioOutputs[2].AllowedLanguages.Should().Equal("fr");
    }

    [Fact]
    public void SubtitleMode_HasAllThreeValues()
    {
        SubtitleMode[] values = Enum.GetValues<SubtitleMode>();

        values.Should().Contain(SubtitleMode.Extract);
        values.Should().Contain(SubtitleMode.BurnIn);
        values.Should().Contain(SubtitleMode.PassThrough);
        values.Should().HaveCount(3);
    }

    [Fact]
    public void VideoOutput_HeightIsNullable_MaintainsAspectRatio()
    {
        VideoOutput output = new VideoOutput(
            Codec: VideoCodecType.H265,
            Width: 1920,
            Height: null,
            BitrateKbps: 0,
            Crf: 28,
            Preset: null,
            Profile: null,
            Level: null,
            ConvertHdrToSdr: true,
            KeyframeIntervalSeconds: 0,
            TenBit: true
        );

        output.Height.Should().BeNull();
        output.Preset.Should().BeNull();
        output.Profile.Should().BeNull();
        output.Level.Should().BeNull();
        output.TenBit.Should().BeTrue();
        output.ConvertHdrToSdr.Should().BeTrue();
    }

    [Fact]
    public void AudioOutput_AllowedLanguages_EmptyMeansAllLanguages()
    {
        AudioOutput audio = new AudioOutput(
            Codec: AudioCodecType.Opus,
            BitrateKbps: 128,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: []
        );

        audio.AllowedLanguages.Should().BeEmpty();
    }

    [Fact]
    public void SubtitleOutput_AllSubtitleModes_AreUsable()
    {
        SubtitleOutput extract = new SubtitleOutput(
            Codec: SubtitleCodecType.WebVtt,
            Mode: SubtitleMode.Extract,
            AllowedLanguages: []
        );

        SubtitleOutput burnIn = new SubtitleOutput(
            Codec: SubtitleCodecType.Ass,
            Mode: SubtitleMode.BurnIn,
            AllowedLanguages: ["en"]
        );

        SubtitleOutput passThrough = new SubtitleOutput(
            Codec: SubtitleCodecType.Srt,
            Mode: SubtitleMode.PassThrough,
            AllowedLanguages: ["en", "es"]
        );

        extract.Mode.Should().Be(SubtitleMode.Extract);
        burnIn.Mode.Should().Be(SubtitleMode.BurnIn);
        passThrough.Mode.Should().Be(SubtitleMode.PassThrough);
        passThrough.AllowedLanguages.Should().HaveCount(2);
    }
}
