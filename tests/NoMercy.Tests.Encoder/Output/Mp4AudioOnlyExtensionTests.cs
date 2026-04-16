namespace NoMercy.Tests.Encoder.Output;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

/// <summary>
/// Verifies Phase 13 audio single-file output — when an MP4 encode has no
/// video tracks, the finalized file extension switches to .m4a so music
/// players pick it up correctly (V1 parity).
/// </summary>
public class Mp4AudioOnlyExtensionTests : IDisposable
{
    private readonly string _outputDir;

    public Mp4AudioOnlyExtensionTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), $"Mp4Audio_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Finalize_AudioOnlyPlan_RenamesToM4a()
    {
        Mp4OutputStrategy strategy = new();
        string sourcePath = Path.Combine(_outputDir, "output.mp4");
        await File.WriteAllBytesAsync(sourcePath, [0x00, 0x01]);

        OutputPlan audioOnly = new(
            Format: OutputFormat.Mp4,
            VideoOutputs: [],
            AudioOutputs:
            [
                new AudioOutputPlan(
                    EncoderName: "libfdk_aac",
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Transcode,
                    Language: "en",
                    MapLabel: "0:a:0"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        await strategy.FinalizeAsync(_outputDir, audioOnly, "Track01", CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(_outputDir, "Track01.m4a")));
        Assert.False(File.Exists(Path.Combine(_outputDir, "Track01.mp4")));
    }

    [Fact]
    public async Task Finalize_VideoPlan_StaysMp4()
    {
        Mp4OutputStrategy strategy = new();
        string sourcePath = Path.Combine(_outputDir, "output.mp4");
        await File.WriteAllBytesAsync(sourcePath, [0x00, 0x01]);

        OutputPlan videoPlan = new(
            Format: OutputFormat.Mp4,
            VideoOutputs:
            [
                new VideoOutputPlan(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 4000,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.1",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new Dictionary<string, string>()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        await strategy.FinalizeAsync(_outputDir, videoPlan, "Movie", CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(_outputDir, "Movie.mp4")));
        Assert.False(File.Exists(Path.Combine(_outputDir, "Movie.m4a")));
    }
}
