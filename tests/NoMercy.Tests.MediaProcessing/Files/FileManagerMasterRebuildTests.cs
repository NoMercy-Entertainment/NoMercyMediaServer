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
using Newtonsoft.Json;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Bundle;
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
        _tempRoot = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-master-rebuild-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempRoot))
            Directory.Delete(path: _tempRoot, recursive: true);
    }

    [Fact]
    public async Task Scan_PublishedCascadeOutput_RebuildsCompleteMasterFromDisk()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "The.Punisher.One.Last.Kill.(2026).NoMercy");
        Directory.CreateDirectory(path: hostDir);

        WriteVariant(hostDir: hostDir, dirName: "video_3840x2160", segmentBytes: 900_000, extension: ".m4s");
        WriteVariant(hostDir: hostDir, dirName: "video_1920x1080_SDR", segmentBytes: 300_000, extension: ".m4s");
        WriteVariant(hostDir: hostDir, dirName: "audio_eng_eac3", segmentBytes: 60_000, extension: ".m4s");
        WriteInitMp4(hostDir: hostDir, dirName: "video_3840x2160");
        WriteInitMp4(hostDir: hostDir, dirName: "video_1920x1080_SDR");
        WriteSubtitle(hostDir: hostDir, language: "eng", variant: "full");

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

        LocalStorageDriver driver = new();
        LocalStorage storage = new(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));

        FileManager manager = BuildFileManager();
        List<IVideo> video = InvokeGetVideoHashList(manager: manager, storage: storage, hostFolder: hostDir);
        video.Should().HaveCount(expected: 2, because: "both rendition dirs must be picked up by the scan");

        string fileName = "/The.Punisher.One.Last.Kill.(2026).NoMercy.m3u8";

        await InvokeRebuildHlsMasterFromDiskAsync(manager: manager, storage: storage, hostFolder: hostDir, fileName: fileName, video: video);

        string masterPath = Path.Combine(path1: hostDir, path2: "The.Punisher.One.Last.Kill.(2026).NoMercy.m3u8");
        File.Exists(path: masterPath)
            .Should()
            .BeTrue(because: "the rebuild must write the master onto the host folder");

        string master = await File.ReadAllTextAsync(path: masterPath);

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

        master.Should().Contain(expected: "#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio_eac3\",LANGUAGE=\"eng\"");
        master.Should().Contain(expected: "#EXT-X-MEDIA:TYPE=SUBTITLES");
        hdrLine.Should().Contain(expected: "AUDIO=\"audio_eac3\"");
        hdrLine.Should().Contain(expected: "SUBTITLES=\"subs\"");

        // Renditions-on-disk == entries-in-master, for every kind.
        int videoDirsOnDisk = Directory.GetDirectories(path: hostDir, searchPattern: "video_*").Length;
        int audioDirsOnDisk = Directory.GetDirectories(path: hostDir, searchPattern: "audio_*").Length;
        int subtitleTracksOnDisk = Directory
            .GetDirectories(path: Path.Combine(path1: hostDir, path2: "subtitles"))
            .Sum(selector: languageDir => Directory.GetFiles(path: languageDir, searchPattern: "*.ass").Length);

        streamInfLines.Count.Should().Be(expected: videoDirsOnDisk);
        Regex.Matches(input: master, pattern: "#EXT-X-MEDIA:TYPE=AUDIO").Count.Should().Be(expected: audioDirsOnDisk);
        Regex
            .Matches(input: master, pattern: "#EXT-X-MEDIA:TYPE=SUBTITLES")
            .Count.Should()
            .Be(expected: subtitleTracksOnDisk);
    }

    [Fact]
    public async Task Scan_SecondPass_MasterAlreadyComplete_SkipsReprobeAndKeepsMaster()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Repeat.Run.(2024).NoMercy");
        Directory.CreateDirectory(path: hostDir);
        WriteVariant(hostDir: hostDir, dirName: "video_1920x1080_SDR", segmentBytes: 300_000, extension: ".m4s");
        WriteInitMp4(hostDir: hostDir, dirName: "video_1920x1080_SDR");
        WriteVariant(hostDir: hostDir, dirName: "audio_eng_eac3", segmentBytes: 60_000, extension: ".m4s");
        SetupProbe(dirName: "video_1920x1080_SDR", codec: "hevc", width: 1920, height: 1080, bitDepth: 8, colorTransfer: "bt709");

        LocalStorageDriver driver = new();
        LocalStorage storage = new(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));
        FileManager manager = BuildFileManager();
        string fileName = "/Repeat.Run.(2024).NoMercy.m3u8";
        string masterPath = Path.Combine(path1: hostDir, path2: "Repeat.Run.(2024).NoMercy.m3u8");

        List<IVideo> firstVideo = InvokeGetVideoHashList(manager: manager, storage: storage, hostFolder: hostDir);
        await InvokeRebuildHlsMasterFromDiskAsync(manager: manager, storage: storage, hostFolder: hostDir, fileName: fileName, video: firstVideo);

        _mediaAnalyzer
            .Invocations.Count.Should()
            .BeGreaterThan(
                expected: 0,
                because: "the first pass has no master on disk and must probe every rendition"
            );
        string firstMaster = await File.ReadAllTextAsync(path: masterPath);

        // A second scan of the same, already-complete output must be a pure read:
        // no rendition probe, no master rewrite — this is the per-file cost that
        // made a rescan crawl.
        _mediaAnalyzer.Invocations.Clear();
        List<IVideo> secondVideo = InvokeGetVideoHashList(manager: manager, storage: storage, hostFolder: hostDir);
        await InvokeRebuildHlsMasterFromDiskAsync(manager: manager, storage: storage, hostFolder: hostDir, fileName: fileName, video: secondVideo);

        _mediaAnalyzer
            .Invocations.Should()
            .BeEmpty(because: "a master already advertising every on-disk rendition must not be reprobed");
        (await File.ReadAllTextAsync(path: masterPath))
            .Should()
            .Be(expected: firstMaster, because: "the skipped rebuild must leave the master untouched");
    }

    [Fact]
    public async Task Scan_SecondPass_NewLadderRungPublished_RecreatesMasterWithEveryRung()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Cascade.Grows.(2025).NoMercy");
        Directory.CreateDirectory(path: hostDir);
        WriteVariant(hostDir: hostDir, dirName: "video_1920x1080_SDR", segmentBytes: 300_000, extension: ".m4s");
        WriteInitMp4(hostDir: hostDir, dirName: "video_1920x1080_SDR");
        WriteVariant(hostDir: hostDir, dirName: "audio_eng_eac3", segmentBytes: 60_000, extension: ".m4s");
        SetupProbe(dirName: "video_1920x1080_SDR", codec: "hevc", width: 1920, height: 1080, bitDepth: 8, colorTransfer: "bt709");

        LocalStorageDriver driver = new();
        LocalStorage storage = new(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));
        FileManager manager = BuildFileManager();
        string fileName = "/Cascade.Grows.(2025).NoMercy.m3u8";
        string masterPath = Path.Combine(path1: hostDir, path2: "Cascade.Grows.(2025).NoMercy.m3u8");

        List<IVideo> firstVideo = InvokeGetVideoHashList(manager: manager, storage: storage, hostFolder: hostDir);
        await InvokeRebuildHlsMasterFromDiskAsync(manager: manager, storage: storage, hostFolder: hostDir, fileName: fileName, video: firstVideo);
        CountStreamInf(master: await File.ReadAllTextAsync(path: masterPath))
            .Should()
            .Be(expected: 1, because: "the first bundle published only the 1080p rung");

        // A later self-finalizing bundle publishes the 4K rung into the SAME output
        // folder — the exact multi-run case the disk-truth rebuild exists for. The
        // skip guard must NOT treat the now-stale master as complete.
        WriteVariant(hostDir: hostDir, dirName: "video_3840x2160", segmentBytes: 900_000, extension: ".m4s");
        WriteInitMp4(hostDir: hostDir, dirName: "video_3840x2160");
        SetupProbe(dirName: "video_3840x2160", codec: "hevc", width: 3840, height: 2160, bitDepth: 10, colorTransfer: "smpte2084");

        List<IVideo> secondVideo = InvokeGetVideoHashList(manager: manager, storage: storage, hostFolder: hostDir);
        secondVideo.Should().HaveCount(expected: 2, because: "the scan sees both published rungs on disk");
        await InvokeRebuildHlsMasterFromDiskAsync(manager: manager, storage: storage, hostFolder: hostDir, fileName: fileName, video: secondVideo);

        string secondMaster = await File.ReadAllTextAsync(path: masterPath);
        CountStreamInf(master: secondMaster)
            .Should()
            .Be(expected: 2, because: "a master missing an on-disk rung must be recreated to list every rung");
        secondMaster.Should().Contain(expected: "RESOLUTION=3840x2160");
        secondMaster.Should().Contain(expected: "RESOLUTION=1920x1080");
    }

    [Fact]
    public async Task Scan_NonHlsItem_NoVideoRenditions_DoesNotWriteAMaster()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Some.Raw.File.(2020).NoMercy");
        Directory.CreateDirectory(path: hostDir);

        LocalStorageDriver driver = new();
        LocalStorage storage = new(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));
        FileManager manager = BuildFileManager();

        await InvokeRebuildHlsMasterFromDiskAsync(
            manager: manager,
            storage: storage,
            hostFolder: hostDir,
            fileName: "/Some.Raw.File.(2020).NoMercy.mkv",
            video: []
        );

        Directory.GetFiles(path: hostDir, searchPattern: "*.m3u8").Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Regression: the audio-loss bug (ebc82df9's rebuild dropping the audio
    // group for older-encoded titles) and its blueprint-primary fix.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Scan_OldNamingAudioDirsNoCodecSuffix_NoBlueprint_RebuildsMasterWithAudioGroup()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Chainsaw.Man.S01E01.(2022).NoMercy");
        Directory.CreateDirectory(path: hostDir);

        WriteVariant(hostDir: hostDir, dirName: "video_1920x1080_SDR", segmentBytes: 300_000, extension: ".m4s");
        WriteInitMp4(hostDir: hostDir, dirName: "video_1920x1080_SDR");
        // Pre-codec-suffix encoder naming: no `_<codec>` token on the dir at
        // all — the exact on-disk shape that regressed audio to silent.
        WriteVariant(hostDir: hostDir, dirName: "audio_jpn", segmentBytes: 60_000, extension: ".m4s");
        WriteVariant(hostDir: hostDir, dirName: "audio_eng", segmentBytes: 60_000, extension: ".m4s");

        SetupProbe(dirName: "video_1920x1080_SDR", codec: "hevc", width: 1920, height: 1080, bitDepth: 8, colorTransfer: "bt709");

        LocalStorageDriver driver = new();
        LocalStorage storage = new(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));
        FileManager manager = BuildFileManager();
        string fileName = "/Chainsaw.Man.S01E01.(2022).NoMercy.m3u8";
        string masterPath = Path.Combine(path1: hostDir, path2: "Chainsaw.Man.S01E01.(2022).NoMercy.m3u8");

        List<IVideo> video = InvokeGetVideoHashList(manager: manager, storage: storage, hostFolder: hostDir);
        await InvokeRebuildHlsMasterFromDiskAsync(manager: manager, storage: storage, hostFolder: hostDir, fileName: fileName, video: video);

        File.Exists(path: masterPath).Should().BeTrue();
        string master = await File.ReadAllTextAsync(path: masterPath);

        Regex
            .Matches(input: master, pattern: "#EXT-X-MEDIA:TYPE=AUDIO")
            .Count.Should()
            .Be(expected: 2, because: "both old-naming audio dirs must surface as a group, not be silently skipped");
        master.Should().Contain(expected: "LANGUAGE=\"jpn\"");
        master.Should().Contain(expected: "LANGUAGE=\"eng\"");

        string streamInfLine = master
            .Split(separator: '\n')
            .First(predicate: line => line.StartsWith(value: "#EXT-X-STREAM-INF:", comparisonType: StringComparison.Ordinal));
        System.Text.RegularExpressions.Match audioAttr = Regex.Match(
            input: streamInfLine,
            pattern: "AUDIO=\"(?<id>[^\"]+)\""
        );
        audioAttr
            .Success.Should()
            .BeTrue(
                because: "the video variant must reference the audio group the rebuild actually emitted"
            );
        master.Should().Contain(expected: $"GROUP-ID=\"{audioAttr.Groups[groupname: "id"].Value}\"");
    }

    [Fact]
    public async Task Scan_BlueprintPresent_CustomAudioRenditionPath_MasterBuiltFromBlueprintFiles()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Custom.Naming.Title.(2025).NoMercy");
        Directory.CreateDirectory(path: hostDir);

        WriteVariant(hostDir: hostDir, dirName: "video_1920x1080_SDR", segmentBytes: 300_000, extension: ".m4s");
        WriteInitMp4(hostDir: hostDir, dirName: "video_1920x1080_SDR");
        SetupProbe(dirName: "video_1920x1080_SDR", codec: "hevc", width: 1920, height: 1080, bitDepth: 8, colorTransfer: "bt709");

        // A custom PlaylistNameTemplate produced this — no "audio_" prefix at
        // all. A directory-name parse (even a lenient one) can never resolve
        // this; only the blueprint's recorded Files[] can.
        string customAudioDir = Path.Combine(path1: hostDir, path2: "sound", path3: "japanese-track");
        Directory.CreateDirectory(path: customAudioDir);
        File.WriteAllBytes(path: Path.Combine(path1: customAudioDir, path2: "stream_00000.m4s"), bytes: new byte[60_000]);
        File.WriteAllText(
            path: Path.Combine(path1: customAudioDir, path2: "stream.m3u8"),
            contents: "#EXTM3U\n#EXTINF:6.000000,\nstream_00000.m4s\n#EXT-X-ENDLIST\n"
        );

        WriteAudioBlueprint(
            hostDir: hostDir,
            tracks:
            [
                new BlueprintTrack(
                    SourceStreamIndex: 1,
                    Kind: "audio",
                    SourceCodec: "aac",
                    SourceLanguage: "jpn",
                    Policy: "copy",
                    Fidelity: "lossless",
                    Reconstructable: true,
                    OriginalParams: null,
                    Container: "hls",
                    Files:
                    [
                        "sound/japanese-track/stream.m3u8",
                        "sound/japanese-track/stream_00000.m4s",
                    ],
                    Sha256: null
                ),
            ]
        );

        LocalStorageDriver driver = new();
        LocalStorage storage = new(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));
        FileManager manager = BuildFileManager();
        string fileName = "/Custom.Naming.Title.(2025).NoMercy.m3u8";
        string masterPath = Path.Combine(path1: hostDir, path2: "Custom.Naming.Title.(2025).NoMercy.m3u8");

        List<IVideo> video = InvokeGetVideoHashList(manager: manager, storage: storage, hostFolder: hostDir);
        await InvokeRebuildHlsMasterFromDiskAsync(manager: manager, storage: storage, hostFolder: hostDir, fileName: fileName, video: video);

        string master = await File.ReadAllTextAsync(path: masterPath);

        master.Should().Contain(expected: "#EXT-X-MEDIA:TYPE=AUDIO");
        master.Should().Contain(expected: "LANGUAGE=\"jpn\"");
        master
            .Should()
            .Contain(
                expected: "URI=\"sound/japanese-track/stream.m3u8\"",
                because: "the master must reference the blueprint's resolved output path, not a guessed directory name"
            );
    }

    [Fact]
    public async Task Scan_ZeroAudioRenditions_MasterHasNoAudioGroupAndNoDanglingReference()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Silent.Documentary.(2019).NoMercy");
        Directory.CreateDirectory(path: hostDir);

        WriteVariant(hostDir: hostDir, dirName: "video_1920x1080_SDR", segmentBytes: 300_000, extension: ".m4s");
        WriteInitMp4(hostDir: hostDir, dirName: "video_1920x1080_SDR");
        SetupProbe(dirName: "video_1920x1080_SDR", codec: "hevc", width: 1920, height: 1080, bitDepth: 8, colorTransfer: "bt709");

        LocalStorageDriver driver = new();
        LocalStorage storage = new(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));
        FileManager manager = BuildFileManager();
        string fileName = "/Silent.Documentary.(2019).NoMercy.m3u8";
        string masterPath = Path.Combine(path1: hostDir, path2: "Silent.Documentary.(2019).NoMercy.m3u8");

        List<IVideo> video = InvokeGetVideoHashList(manager: manager, storage: storage, hostFolder: hostDir);
        await InvokeRebuildHlsMasterFromDiskAsync(manager: manager, storage: storage, hostFolder: hostDir, fileName: fileName, video: video);

        string master = await File.ReadAllTextAsync(path: masterPath);

        master.Should().NotContain(unexpected: "#EXT-X-MEDIA:TYPE=AUDIO");
        master
            .Should()
            .NotContain(
                unexpected: "AUDIO=\"",
                because: "a title with zero audio renditions must not reference an audio group that was never emitted"
            );
    }

    private static void WriteAudioBlueprint(string hostDir, IReadOnlyList<BlueprintTrack> tracks)
    {
        MediaBlueprint blueprint = new(
            Version: 1,
            Identity: new BlueprintIdentity(
                Type: "movie",
                TmdbId: 1,
                Show: null,
                Season: null,
                Episode: null,
                Title: "Fixture",
                Year: 2025
            ),
            Source: new BlueprintSource(
                Path: "/source/fixture.mkv",
                Filename: "fixture.mkv",
                Container: "matroska",
                SizeBytes: 0,
                DurationSeconds: 0,
                Sha256: null,
                Ffprobe: null
            ),
            Encodes:
            [
                new BlueprintEncode(
                    PresetSlug: "fixture-preset",
                    PresetId: "1",
                    ProfileFingerprint: null,
                    EncoderVersion: "test",
                    TargetContainer: "hls",
                    OutputLocation: hostDir,
                    CreatedAt: DateTime.UtcNow,
                    CompletedAt: DateTime.UtcNow,
                    Tracks: tracks,
                    ReconstructionCommandTemplate: null,
                    LossyWarnings: []
                ),
            ]
        );

        string json = JsonConvert.SerializeObject(value: blueprint, formatting: Formatting.Indented);
        File.WriteAllText(path: Path.Combine(path1: hostDir, path2: MediaBlueprintWriter.FileName), contents: json);
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
        string variantDirectory = Path.Combine(path1: hostDir, path2: dirName);
        Directory.CreateDirectory(path: variantDirectory);

        byte[] segment = new byte[segmentBytes];
        File.WriteAllBytes(path: Path.Combine(path1: variantDirectory, path2: $"{dirName}_00000{extension}"), bytes: segment);

        string playlist =
            $"#EXTM3U\n#EXTINF:6.000000,\n{dirName}_00000{extension}\n#EXT-X-ENDLIST\n";
        File.WriteAllText(path: Path.Combine(path1: variantDirectory, path2: $"{dirName}.m3u8"), contents: playlist);
    }

    private static void WriteInitMp4(string hostDir, string dirName) =>
        File.WriteAllBytes(path: Path.Combine(path1: hostDir, path2: dirName, path3: "init.mp4"), bytes: new byte[512]);

    private static void WriteSubtitle(string hostDir, string language, string variant)
    {
        string subtitleDirectory = Path.Combine(path1: hostDir, path2: "subtitles", path3: language);
        Directory.CreateDirectory(path: subtitleDirectory);
        File.WriteAllText(path: Path.Combine(path1: subtitleDirectory, path2: $"{variant}.ass"), contents: "[Script Info]\n");
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

    private static int CountStreamInf(string master) =>
        master
            .Split(separator: '\n')
            .Count(predicate: line => line.StartsWith(value: "#EXT-X-STREAM-INF:", comparisonType: StringComparison.Ordinal));

    private static int ExtractInt(string streamInfLine, string attribute)
    {
        System.Text.RegularExpressions.Match match = Regex.Match(
            input: streamInfLine,
            pattern: $@"{attribute}=(?<value>\d+)"
        );
        return int.Parse(s: match.Groups[groupname: "value"].Value);
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
        return new(fileRepository: repoMock.Object, storageFactory: factoryMock.Object, storageDriver: driverMock.Object, mediaAnalyzer: _mediaAnalyzer.Object);
    }

    private static List<IVideo> InvokeGetVideoHashList(
        FileManager manager,
        IStorage storage,
        string hostFolder
    )
    {
        MethodInfo method =
            typeof(FileManager).GetMethod(
                name: "GetVideoHashList",
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
            ) ?? throw new InvalidOperationException(message: "GetVideoHashList not found");

        return (List<IVideo>)method.Invoke(obj: manager, parameters: [storage, hostFolder])!;
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
                name: "RebuildHlsMasterFromDiskAsync",
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
            ) ?? throw new InvalidOperationException(message: "RebuildHlsMasterFromDiskAsync not found");

        await (Task)method.Invoke(obj: manager, parameters: [storage, hostFolder, fileName, video])!;
    }
}
