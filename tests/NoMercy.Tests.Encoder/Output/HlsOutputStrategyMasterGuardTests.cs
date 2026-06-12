using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Output;

public class HlsOutputStrategyMasterGuardTests : IDisposable
{
    private readonly string _outputDirectory;

    public HlsOutputStrategyMasterGuardTests()
    {
        _outputDirectory = Path.Combine(
            Path.GetTempPath(),
            $"nomercy-master-guard-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(_outputDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
            Directory.Delete(_outputDirectory, true);
    }

    [Fact]
    public async Task FinalizeAsync_NoVariantProducedSegments_ThrowsAndPreservesExistingMaster()
    {
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        OutputPlan plan = CreateSimplePlan();

        string masterPath = Path.Combine(_outputDirectory, "Title.m3u8");
        string existingMaster = "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1\nold/old.m3u8\n";
        await File.WriteAllTextAsync(masterPath, existingMaster);

        Func<Task> act = () =>
            strategy.FinalizeAsync(_outputDirectory, plan, "Title", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*zero variants*");

        string preserved = await File.ReadAllTextAsync(masterPath);
        preserved.Should().Be(existingMaster);
    }

    [Fact]
    public async Task FinalizeAsync_NoVariantsAndNoExistingMaster_WritesNothing()
    {
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        OutputPlan plan = CreateSimplePlan();

        Func<Task> act = () =>
            strategy.FinalizeAsync(_outputDirectory, plan, "Title", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        File.Exists(Path.Combine(_outputDirectory, "Title.m3u8")).Should().BeFalse();
    }

    [Fact]
    public async Task FinalizeAsync_VariantsWithSegments_WritesPopulatedMaster()
    {
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        OutputPlan plan = CreateSimplePlan();

        WriteVariant("video_1920x1080_SDR", "video_1920x1080_SDR");
        WriteVariant("audio_eng_aac", "audio_eng_aac");

        await strategy.FinalizeAsync(_outputDirectory, plan, "Title", CancellationToken.None);

        string master = await File.ReadAllTextAsync(Path.Combine(_outputDirectory, "Title.m3u8"));
        master.Should().Contain("#EXT-X-STREAM-INF");
        master.Should().Contain("video_1920x1080_SDR/video_1920x1080_SDR.m3u8");
        master.Should().Contain("audio_eng_aac/audio_eng_aac.m3u8");
    }

    private void WriteVariant(string subDirectory, string name)
    {
        string variantDirectory = Path.Combine(_outputDirectory, subDirectory);
        Directory.CreateDirectory(variantDirectory);

        byte[] segmentBytes = new byte[120_000];
        File.WriteAllBytes(Path.Combine(variantDirectory, $"{name}_00000.ts"), segmentBytes);

        string playlist = $"#EXTM3U\n#EXTINF:6.000000,\n{name}_00000.ts\n#EXT-X-ENDLIST\n";
        File.WriteAllText(Path.Combine(variantDirectory, $"{name}.m3u8"), playlist);
    }

    private static OutputPlan CreateSimplePlan()
    {
        return new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 0,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.0",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new(),
                    SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                    PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
                ),
            ],
            AudioOutputs:
            [
                new(
                    EncoderName: "aac",
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Transcode,
                    Language: "eng",
                    MapLabel: "0:a:0",
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );
    }
}
