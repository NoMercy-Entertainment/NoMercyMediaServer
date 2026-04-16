namespace NoMercy.Tests.Encoder.Profiles;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

/// <summary>
/// Audio-only single-file containers (Mp3 / Flac / Ogg) have tight codec
/// constraints. The validator must reject mismatched codecs and any
/// attempt to slip video or subtitle outputs through.
/// </summary>
public class AudioOnlyFormatValidationTests
{
    private readonly ProfileValidator _validator = new(new CodecRegistry());

    [Fact]
    public void Mp3_WithMp3Codec_Passes()
    {
        EncodingProfile profile = Build(OutputFormat.Mp3, AudioCodecType.Mp3);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Mp3_WithAacCodec_Fails()
    {
        EncodingProfile profile = Build(OutputFormat.Mp3, AudioCodecType.Aac);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field.Contains("Codec"));
    }

    [Fact]
    public void Flac_WithFlacCodec_Passes()
    {
        EncodingProfile profile = Build(OutputFormat.Flac, AudioCodecType.Flac);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Flac_WithMp3Codec_Fails()
    {
        EncodingProfile profile = Build(OutputFormat.Flac, AudioCodecType.Mp3);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(AudioCodecType.Vorbis)]
    [InlineData(AudioCodecType.Opus)]
    [InlineData(AudioCodecType.Flac)]
    public void Ogg_WithSupportedCodec_Passes(AudioCodecType codec)
    {
        EncodingProfile profile = Build(OutputFormat.Ogg, codec);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Ogg_WithAacCodec_Fails()
    {
        EncodingProfile profile = Build(OutputFormat.Ogg, AudioCodecType.Aac);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AudioOnly_WithVideoOutput_Fails()
    {
        VideoOutput video = new(
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

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "Broken",
            Format: OutputFormat.Mp3,
            VideoOutputs: [video],
            AudioOutputs: [Audio(AudioCodecType.Mp3)],
            SubtitleOutputs: []
        );

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "VideoOutputs");
    }

    [Fact]
    public void AudioOnly_WithSubtitleOutput_Fails()
    {
        SubtitleOutput sub = new(
            Codec: SubtitleCodecType.WebVtt,
            Mode: SubtitleMode.Extract,
            AllowedLanguages: ["en"]
        );

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "Broken",
            Format: OutputFormat.Flac,
            VideoOutputs: [],
            AudioOutputs: [Audio(AudioCodecType.Flac)],
            SubtitleOutputs: [sub]
        );

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "SubtitleOutputs");
    }

    [Fact]
    public void AudioOnly_WithNoAudioOutput_Fails()
    {
        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "Empty",
            Format: OutputFormat.Mp3,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: []
        );

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "AudioOutputs");
    }

    private static EncodingProfile Build(OutputFormat format, AudioCodecType codec) =>
        new(
            Id: Ulid.NewUlid(),
            Name: $"{format} {codec}",
            Format: format,
            VideoOutputs: [],
            AudioOutputs: [Audio(codec)],
            SubtitleOutputs: []
        );

    private static AudioOutput Audio(AudioCodecType codec) =>
        new(
            Codec: codec,
            // FLAC/TrueHD are lossless — no bitrate. Others use a real value.
            BitrateKbps: codec is AudioCodecType.Flac or AudioCodecType.TrueHd ? 0 : 192,
            Channels: 2,
            // 48 kHz is the one rate accepted by every codec the tests use
            // (Opus is 48-only; Vorbis / FLAC / Mp3 / AAC all accept 48).
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );
}
