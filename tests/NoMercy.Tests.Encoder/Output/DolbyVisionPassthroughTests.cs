namespace NoMercy.Tests.Encoder.Output;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Tests.Encoder.Storage;

/// <summary>
/// Verifies that the container-specific Dolby Vision tags land in the
/// ffmpeg argv when <see cref="OutputPlan.PreserveDolbyVision"/> is set.
/// Without the <c>dvh1</c> tag, Apple TV / QuickTime play DV MP4 content
/// as plain HDR10 and the effect is lost — so the check has to be explicit.
/// </summary>
public class DolbyVisionPassthroughTests
{
    [Fact]
    public void Mp4_PreserveDv_AddsDvh1CodecTag()
    {
        Mp4OutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(builder, Plan(preserveDv: true), "/output");

        string args = string.Join(" ", builder.Build("ffmpeg").Arguments);
        args.Should().Contain("-tag:v dvh1");
    }

    [Fact]
    public void Mp4_NoDv_UsesDefaultTag()
    {
        Mp4OutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(builder, Plan(preserveDv: false), "/output");

        string args = string.Join(" ", builder.Build("ffmpeg").Arguments);
        args.Should().NotContain("dvh1");
    }

    [Fact]
    public void Hls_PreserveDv_HevcVariant_OverridesHvc1WithDvh1()
    {
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(
            builder,
            HlsPlan(encoderName: "libx265", preserveDv: true),
            "/output"
        );

        string args = string.Join(" ", builder.Build("ffmpeg").Arguments);
        args.Should().Contain("-tag:v dvh1");
        // dvh1 replaces hvc1 — both appearing in the same argv is a muxer
        // contradiction and would break playback.
        args.Should().NotContain("-tag:v hvc1");
    }

    [Fact]
    public void Hls_PreserveDv_H264Variant_NoDvTag()
    {
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        // DV only rides HEVC. H.264 variants in the same ladder must not
        // inherit the dvh1 tag or the MP4 header becomes invalid.
        strategy.ConfigureOutput(
            builder,
            HlsPlan(encoderName: "libx264", preserveDv: true),
            "/output"
        );

        string args = string.Join(" ", builder.Build("ffmpeg").Arguments);
        args.Should().NotContain("dvh1");
    }

    private static OutputPlan Plan(bool preserveDv) =>
        new(
            Format: OutputFormat.Mp4,
            VideoOutputs:
            [
                new(
                    Width: 3840,
                    Height: 2160,
                    EncoderName: "libx265",
                    Crf: 22,
                    BitrateKbps: 0,
                    Preset: "medium",
                    Profile: "main10",
                    Level: "5.1",
                    TenBit: true,
                    PixelFormat: "yuv420p10le",
                    MapLabel: "[v0]",
                    ExtraFlags: new()
                ),
            ],
            AudioOutputs:
            [
                new(
                    EncoderName: "libfdk_aac",
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Transcode,
                    Language: "eng",
                    MapLabel: "0:a:0"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null,
            PreserveDolbyVision: preserveDv
        );

    private static OutputPlan HlsPlan(string encoderName, bool preserveDv) =>
        new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 3840,
                    Height: 2160,
                    EncoderName: encoderName,
                    Crf: 22,
                    BitrateKbps: 0,
                    Preset: "medium",
                    Profile: "main10",
                    Level: "5.1",
                    TenBit: true,
                    PixelFormat: "yuv420p10le",
                    MapLabel: "[v0]",
                    ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            PreserveDolbyVision: preserveDv
        );
}
