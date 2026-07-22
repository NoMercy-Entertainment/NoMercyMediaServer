// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

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
            path1: Path.GetTempPath(),
            path2: $"nomercy-master-guard-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(path: _outputDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _outputDirectory))
            Directory.Delete(path: _outputDirectory, recursive: true);
    }

    [Fact]
    public async Task FinalizeAsync_NoVariantProducedSegments_ThrowsAndPreservesExistingMaster()
    {
        HlsOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        OutputPlan plan = CreateSimplePlan();

        string masterPath = Path.Combine(path1: _outputDirectory, path2: "Title.m3u8");
        string existingMaster = "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1\nold/old.m3u8\n";
        await File.WriteAllTextAsync(path: masterPath, contents: existingMaster);

        Func<Task> act = () =>
            strategy.FinalizeAsync(outputDirectory: _outputDirectory, plan: plan, mediaTitle: "Title", ct: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*zero variants*");

        string preserved = await File.ReadAllTextAsync(path: masterPath);
        preserved.Should().Be(expected: existingMaster);
    }

    [Fact]
    public async Task FinalizeAsync_NoVariantsAndNoExistingMaster_WritesNothing()
    {
        HlsOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        OutputPlan plan = CreateSimplePlan();

        Func<Task> act = () =>
            strategy.FinalizeAsync(outputDirectory: _outputDirectory, plan: plan, mediaTitle: "Title", ct: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        File.Exists(path: Path.Combine(path1: _outputDirectory, path2: "Title.m3u8")).Should().BeFalse();
    }

    [Fact]
    public async Task FinalizeAsync_VariantsWithSegments_WritesPopulatedMaster()
    {
        HlsOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        OutputPlan plan = CreateSimplePlan();

        WriteVariant(subDirectory: "video_1920x1080_SDR", name: "video_1920x1080_SDR");
        WriteVariant(subDirectory: "audio_eng_aac", name: "audio_eng_aac");

        await strategy.FinalizeAsync(outputDirectory: _outputDirectory, plan: plan, mediaTitle: "Title", ct: CancellationToken.None);

        string master = await File.ReadAllTextAsync(path: Path.Combine(path1: _outputDirectory, path2: "Title.m3u8"));
        master.Should().Contain(expected: "#EXT-X-STREAM-INF");
        master.Should().Contain(expected: "video_1920x1080_SDR/video_1920x1080_SDR.m3u8");
        master.Should().Contain(expected: "audio_eng_aac/audio_eng_aac.m3u8");
    }

    private void WriteVariant(string subDirectory, string name)
    {
        string variantDirectory = Path.Combine(path1: _outputDirectory, path2: subDirectory);
        Directory.CreateDirectory(path: variantDirectory);

        byte[] segmentBytes = new byte[120_000];
        File.WriteAllBytes(path: Path.Combine(path1: variantDirectory, path2: $"{name}_00000.ts"), bytes: segmentBytes);

        string playlist = $"#EXTM3U\n#EXTINF:6.000000,\n{name}_00000.ts\n#EXT-X-ENDLIST\n";
        File.WriteAllText(path: Path.Combine(path1: variantDirectory, path2: $"{name}.m3u8"), contents: playlist);
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
