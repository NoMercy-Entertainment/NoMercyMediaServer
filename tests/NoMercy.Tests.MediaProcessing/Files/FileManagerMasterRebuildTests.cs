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

using System.Reflection;
using System.Text.RegularExpressions;
using Moq;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Analysis;
using NoMercy.MediaProcessing.Files;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.MediaProcessing.Files;

/// <summary>
/// The master rebuild moved OUT of the encode-finalize path (fragile scope: a
/// preset the decode-aware bundler split into several self-finalizing bundles
/// only ever saw its own last rendition there) and INTO the scan
/// (<see cref="FileManager.MakeMetadata"/> via the extracted
/// <c>RebuildHlsMasterFromDiskAsync</c>), because a completed scan reliably
/// walks the fully-published media root — every rendition every bundle ever
/// wrote, not just the last one. This exercises that extracted method
/// directly against a fixture published output folder, exactly like a
/// real post-encode rescan would see it.
/// </summary>
public sealed class FileManagerMasterRebuildTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly Mock<IMediaAnalyzer> _mediaAnalyzer = new();

    public FileManagerMasterRebuildTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"nm-master-rebuild-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public async Task Scan_PublishedCascadeOutput_RebuildsCompleteMasterFromDisk()
    {
        string hostDir = Path.Combine(_tempRoot, "The.Punisher.One.Last.Kill.(2026).NoMercy");
        Directory.CreateDirectory(hostDir);

        WriteVariant(hostDir, "video_3840x2160", segmentBytes: 900_000, extension: ".m4s");
        WriteVariant(hostDir, "video_1920x1080_SDR", segmentBytes: 300_000, extension: ".m4s");
        WriteVariant(hostDir, "audio_eng_eac3", segmentBytes: 60_000, extension: ".m4s");
        WriteInitMp4(hostDir, "video_3840x2160");
        WriteInitMp4(hostDir, "video_1920x1080_SDR");
        WriteSubtitle(hostDir, "eng", "full");

        SetupProbe(
            "video_3840x2160",
            codec: "hevc",
            width: 3840,
            height: 2160,
            bitDepth: 10,
            colorTransfer: "smpte2084"
        );
        SetupProbe(
            "video_1920x1080_SDR",
            codec: "hevc",
            width: 1920,
            height: 1080,
            bitDepth: 8,
            colorTransfer: "bt709"
        );

        LocalStorageDriver driver = new();
        LocalStorage storage = new(driver, new StoragePathGuard([], driver));

        FileManager manager = BuildFileManager();
        List<IVideo> video = InvokeGetVideoHashList(manager, storage, hostDir);
        video.Should().HaveCount(2, "both rendition dirs must be picked up by the scan");

        string fileName = "/The.Punisher.One.Last.Kill.(2026).NoMercy.m3u8";

        await InvokeRebuildHlsMasterFromDiskAsync(manager, storage, hostDir, fileName, video);

        string masterPath = Path.Combine(hostDir, "The.Punisher.One.Last.Kill.(2026).NoMercy.m3u8");
        File.Exists(masterPath)
            .Should()
            .BeTrue("the rebuild must write the master onto the host folder");

        string master = await File.ReadAllTextAsync(masterPath);

        List<string> streamInfLines = master
            .Split('\n')
            .Where(line => line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.Ordinal))
            .ToList();
        streamInfLines.Should().HaveCount(2);

        string? hdrLine = streamInfLines.FirstOrDefault(line =>
            line.Contains("RESOLUTION=3840x2160", StringComparison.Ordinal)
        );
        string? sdrLine = streamInfLines.FirstOrDefault(line =>
            line.Contains("RESOLUTION=1920x1080", StringComparison.Ordinal)
        );
        hdrLine.Should().NotBeNull();
        sdrLine.Should().NotBeNull();

        hdrLine.Should().Contain("VIDEO-RANGE=PQ");
        sdrLine.Should().Contain("VIDEO-RANGE=SDR");

        hdrLine.Should().MatchRegex(@"CODECS=""hvc1\.2\.4\.L150\.B0,ec-3""");
        sdrLine.Should().MatchRegex(@"CODECS=""hvc1\.1\.6\.L120\.B0,ec-3""");

        int hdrBandwidth = ExtractInt(hdrLine!, "BANDWIDTH");
        int sdrBandwidth = ExtractInt(sdrLine!, "BANDWIDTH");
        hdrBandwidth.Should().NotBe(sdrBandwidth);

        master.Should().Contain("#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio_eac3\",LANGUAGE=\"eng\"");
        master.Should().Contain("#EXT-X-MEDIA:TYPE=SUBTITLES");
        hdrLine.Should().Contain("AUDIO=\"audio_eac3\"");
        hdrLine.Should().Contain("SUBTITLES=\"subs\"");

        // Renditions-on-disk == entries-in-master, for every kind.
        int videoDirsOnDisk = Directory.GetDirectories(hostDir, "video_*").Length;
        int audioDirsOnDisk = Directory.GetDirectories(hostDir, "audio_*").Length;
        int subtitleTracksOnDisk = Directory
            .GetDirectories(Path.Combine(hostDir, "subtitles"))
            .Sum(languageDir => Directory.GetFiles(languageDir, "*.ass").Length);

        streamInfLines.Count.Should().Be(videoDirsOnDisk);
        Regex.Matches(master, "#EXT-X-MEDIA:TYPE=AUDIO").Count.Should().Be(audioDirsOnDisk);
        Regex
            .Matches(master, "#EXT-X-MEDIA:TYPE=SUBTITLES")
            .Count.Should()
            .Be(subtitleTracksOnDisk);
    }

    [Fact]
    public async Task Scan_NonHlsItem_NoVideoRenditions_DoesNotWriteAMaster()
    {
        string hostDir = Path.Combine(_tempRoot, "Some.Raw.File.(2020).NoMercy");
        Directory.CreateDirectory(hostDir);

        LocalStorageDriver driver = new();
        LocalStorage storage = new(driver, new StoragePathGuard([], driver));
        FileManager manager = BuildFileManager();

        await InvokeRebuildHlsMasterFromDiskAsync(
            manager,
            storage,
            hostDir,
            "/Some.Raw.File.(2020).NoMercy.mkv",
            []
        );

        Directory.GetFiles(hostDir, "*.m3u8").Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Fixture helpers
    // -----------------------------------------------------------------------

    private static void WriteVariant(
        string hostDir,
        string dirName,
        int segmentBytes,
        string extension
    )
    {
        string variantDirectory = Path.Combine(hostDir, dirName);
        Directory.CreateDirectory(variantDirectory);

        byte[] segment = new byte[segmentBytes];
        File.WriteAllBytes(Path.Combine(variantDirectory, $"{dirName}_00000{extension}"), segment);

        string playlist =
            $"#EXTM3U\n#EXTINF:6.000000,\n{dirName}_00000{extension}\n#EXT-X-ENDLIST\n";
        File.WriteAllText(Path.Combine(variantDirectory, $"{dirName}.m3u8"), playlist);
    }

    private static void WriteInitMp4(string hostDir, string dirName) =>
        File.WriteAllBytes(Path.Combine(hostDir, dirName, "init.mp4"), new byte[512]);

    private static void WriteSubtitle(string hostDir, string language, string variant)
    {
        string subtitleDirectory = Path.Combine(hostDir, "subtitles", language);
        Directory.CreateDirectory(subtitleDirectory);
        File.WriteAllText(Path.Combine(subtitleDirectory, $"{variant}.ass"), "[Script Info]\n");
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
            Duration: TimeSpan.FromSeconds(6),
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
            .Setup(analyzer =>
                analyzer.AnalyzeAsync(
                    It.Is<string>(path => path.Contains(dirName)),
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(info);
    }

    private static int ExtractInt(string streamInfLine, string attribute)
    {
        System.Text.RegularExpressions.Match match = Regex.Match(
            streamInfLine,
            $@"{attribute}=(?<value>\d+)"
        );
        return int.Parse(match.Groups["value"].Value);
    }

    // -----------------------------------------------------------------------
    // Private-method helpers via reflection — same convention as
    // FileManagerHashListTests.
    // -----------------------------------------------------------------------

    private FileManager BuildFileManager()
    {
        Mock<IFileRepository> repoMock = new();
        Mock<IStorageFactory> factoryMock = new();
        Mock<IStorageDriver> driverMock = new();
        return new(repoMock.Object, factoryMock.Object, driverMock.Object, _mediaAnalyzer.Object);
    }

    private static List<IVideo> InvokeGetVideoHashList(
        FileManager manager,
        IStorage storage,
        string hostFolder
    )
    {
        MethodInfo method =
            typeof(FileManager).GetMethod(
                "GetVideoHashList",
                BindingFlags.NonPublic | BindingFlags.Instance
            ) ?? throw new InvalidOperationException("GetVideoHashList not found");

        return (List<IVideo>)method.Invoke(manager, [storage, hostFolder])!;
    }

    private static async Task InvokeRebuildHlsMasterFromDiskAsync(
        FileManager manager,
        IStorage storage,
        string hostFolder,
        string fileName,
        List<IVideo> video
    )
    {
        MethodInfo method =
            typeof(FileManager).GetMethod(
                "RebuildHlsMasterFromDiskAsync",
                BindingFlags.NonPublic | BindingFlags.Instance
            ) ?? throw new InvalidOperationException("RebuildHlsMasterFromDiskAsync not found");

        await (Task)method.Invoke(manager, [storage, hostFolder, fileName, video])!;
    }
}
