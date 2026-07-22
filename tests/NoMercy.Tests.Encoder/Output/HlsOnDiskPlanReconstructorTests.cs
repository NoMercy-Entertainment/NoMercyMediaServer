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

using System.Text.RegularExpressions;
using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Output;
using NoMercy.Storage;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Output;

/// <summary>
/// The scenario a plan-based reconciliation cannot cover: a single preset
/// the decode-aware bundler split into several self-finalizing bundles, so
/// there is no in-memory <c>OutputPlan</c> anywhere that describes every
/// rendition — only the destination directory does. Exercises
/// <see cref="HlsOnDiskPlanReconstructor"/> straight into
/// <see cref="HlsOutputStrategy.FinalizeAsync"/> exactly as
/// <c>VideoEncodeJob.ReconcileMasterPlaylistAsync</c> now does, with no plan
/// in play at all — this is the single-preset / self-finalized-bundles path
/// the earlier plan-based fix never actually reached.
/// </summary>
public class HlsOnDiskPlanReconstructorTests : IDisposable
{
    private readonly string _outputDirectory;
    private readonly Mock<IMediaAnalyzer> _mediaAnalyzer = new();

    public HlsOnDiskPlanReconstructorTests()
    {
        _outputDirectory = Path.Combine(
            path1: Path.GetTempPath(),
            path2: $"nomercy-disk-union-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(path: _outputDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _outputDirectory))
            Directory.Delete(path: _outputDirectory, recursive: true);
    }

    [Fact]
    public async Task ReconstructAndFinalize_NoPlanInvolved_ProducesCompleteMasterMatchingDisk()
    {
        IStorage storage = TestStorageFactory.CreateLocal();

        // Published output of a single preset the decode-aware bundler split
        // into two self-finalizing Whole bundles — video/audio/subtitles were
        // never in the SAME in-memory OutputPlan at any point in this test.
        WriteVariant(subDirectory: "video_3840x2160", name: "video_3840x2160", segmentBytes: 900_000);
        WriteVariant(subDirectory: "video_1920x1080_SDR", name: "video_1920x1080_SDR", segmentBytes: 300_000);
        WriteVariant(subDirectory: "audio_eng_eac3", name: "audio_eng_eac3", segmentBytes: 60_000);
        WriteInitMp4(subDirectory: "video_3840x2160");
        WriteInitMp4(subDirectory: "video_1920x1080_SDR");
        WriteSubtitle(language: "eng", variant: "full");

        SetupProbe(
            dirName: "video_3840x2160",
            codec: "hevc",
            width: 3840,
            height: 2160,
            bitDepth: 10,
            colorTransfer: "smpte2084"
        );
        SetupProbe(
            dirName: "video_1920x1080_SDR",
            codec: "hevc",
            width: 1920,
            height: 1080,
            bitDepth: 8,
            colorTransfer: "bt709"
        );

        HlsOnDiskPlanReconstructor reconstructor = new(mediaAnalyzer: _mediaAnalyzer.Object);
        OutputPlan plan = await reconstructor.ReconstructAsync(
            storage: storage,
            outputDirectory: _outputDirectory,
            ct: CancellationToken.None
        );

        HlsOutputStrategy strategy = new(storage: storage);
        await strategy.FinalizeAsync(outputDirectory: _outputDirectory, plan: plan, mediaTitle: "Title", ct: CancellationToken.None);

        string master = await File.ReadAllTextAsync(path: Path.Combine(path1: _outputDirectory, path2: "Title.m3u8"));

        List<string> streamInfLines = master
            .Split(separator: '\n')
            .Where(predicate: line => line.StartsWith(value: "#EXT-X-STREAM-INF:", comparisonType: StringComparison.Ordinal))
            .ToList();
        streamInfLines.Should().HaveCount(expected: 2);

        string? hdrLine = streamInfLines.FirstOrDefault(predicate: line =>
            line.Contains(value: "RESOLUTION=3840x2160", comparisonType: StringComparison.Ordinal)
        );
        string? sdrLine = streamInfLines.FirstOrDefault(predicate: line =>
            line.Contains(value: "RESOLUTION=1920x1080", comparisonType: StringComparison.Ordinal)
        );
        hdrLine.Should().NotBeNull();
        sdrLine.Should().NotBeNull();

        hdrLine.Should().Contain(expected: "VIDEO-RANGE=PQ");
        sdrLine.Should().Contain(expected: "VIDEO-RANGE=SDR");

        hdrLine.Should().MatchRegex(regularExpression: @"CODECS=""hvc1\.2\.4\.L150\.B0,ec-3""");
        sdrLine.Should().MatchRegex(regularExpression: @"CODECS=""hvc1\.1\.6\.L120\.B0,ec-3""");

        int hdrBandwidth = ExtractInt(streamInfLine: hdrLine!, attribute: "BANDWIDTH");
        int sdrBandwidth = ExtractInt(streamInfLine: sdrLine!, attribute: "BANDWIDTH");
        hdrBandwidth.Should().NotBe(unexpected: sdrBandwidth);

        // Populated audio group — not a dangling AUDIO="..." referencing a
        // group with zero EXT-X-MEDIA entries.
        hdrLine.Should().Contain(expected: "AUDIO=\"audio_eac3\"");
        master.Should().Contain(expected: "#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio_eac3\",LANGUAGE=\"eng\"");

        master.Should().Contain(expected: "#EXT-X-MEDIA:TYPE=SUBTITLES");
        hdrLine.Should().Contain(expected: "SUBTITLES=\"subs\"");

        // Renditions-on-disk == entries-in-master, for every kind.
        int videoDirsOnDisk = Directory.GetDirectories(path: _outputDirectory, searchPattern: "video_*").Length;
        int audioDirsOnDisk = Directory.GetDirectories(path: _outputDirectory, searchPattern: "audio_*").Length;
        int subtitleTracksOnDisk = Directory
            .GetDirectories(path: Path.Combine(path1: _outputDirectory, path2: "subtitles"))
            .Sum(selector: languageDir => Directory.GetFiles(path: languageDir, searchPattern: "*.ass").Length);

        int videoEntriesInMaster = streamInfLines.Count;
        int audioEntriesInMaster = Regex.Matches(input: master, pattern: "#EXT-X-MEDIA:TYPE=AUDIO").Count;
        int subtitleEntriesInMaster = Regex.Matches(input: master, pattern: "#EXT-X-MEDIA:TYPE=SUBTITLES").Count;

        videoEntriesInMaster.Should().Be(expected: videoDirsOnDisk);
        audioEntriesInMaster.Should().Be(expected: audioDirsOnDisk);
        subtitleEntriesInMaster.Should().Be(expected: subtitleTracksOnDisk);
    }

    private void SetupProbe(
        string dirName,
        string codec,
        int width,
        int height,
        int bitDepth,
        string colorTransfer
    )
    {
        MediaInfo info = new(
            FilePath: dirName,
            Format: "mov,mp4,m4a,3gp,3g2,mj2",
            Duration: TimeSpan.FromSeconds(seconds: 6),
            OverallBitRateKbps: 0,
            FileSizeBytes: 0,
            VideoStreams:
            [
                new VideoStreamInfo(
                    Index: 0,
                    Codec: codec,
                    Width: width,
                    Height: height,
                    FrameRate: 23.976,
                    BitDepth: bitDepth,
                    PixelFormat: bitDepth >= 10 ? "yuv420p10le" : "yuv420p",
                    ColorPrimaries: "bt2020",
                    ColorTransfer: colorTransfer,
                    ColorSpace: "bt2020nc",
                    IsDefault: true,
                    BitRateKbps: 0
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

        _mediaAnalyzer
            .Setup(expression: analyzer =>
                analyzer.AnalyzeAsync(
                    It.Is<string>(path => path.Contains(dirName)),
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: info);
    }

    private static int ExtractInt(string streamInfLine, string attribute)
    {
        System.Text.RegularExpressions.Match match = Regex.Match(
            input: streamInfLine,
            pattern: $@"{attribute}=(?<value>\d+)"
        );
        return int.Parse(s: match.Groups[groupname: "value"].Value);
    }

    private void WriteVariant(string subDirectory, string name, int segmentBytes)
    {
        string variantDirectory = Path.Combine(path1: _outputDirectory, path2: subDirectory);
        Directory.CreateDirectory(path: variantDirectory);

        byte[] segment = new byte[segmentBytes];
        File.WriteAllBytes(path: Path.Combine(path1: variantDirectory, path2: $"{name}_00000.m4s"), bytes: segment);

        string playlist = $"#EXTM3U\n#EXTINF:6.000000,\n{name}_00000.m4s\n#EXT-X-ENDLIST\n";
        File.WriteAllText(path: Path.Combine(path1: variantDirectory, path2: $"{name}.m3u8"), contents: playlist);
    }

    private void WriteInitMp4(string subDirectory)
    {
        string variantDirectory = Path.Combine(path1: _outputDirectory, path2: subDirectory);
        File.WriteAllBytes(path: Path.Combine(path1: variantDirectory, path2: "init.mp4"), bytes: new byte[512]);
    }

    private void WriteSubtitle(string language, string variant)
    {
        string subtitleDirectory = Path.Combine(path1: _outputDirectory, path2: "subtitles", path3: language);
        Directory.CreateDirectory(path: subtitleDirectory);
        File.WriteAllText(path: Path.Combine(path1: subtitleDirectory, path2: $"{variant}.ass"), contents: "[Script Info]\n");
    }
}
