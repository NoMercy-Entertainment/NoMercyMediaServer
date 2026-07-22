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

public class HlsOnDiskPlanReconstructorAudioTests : IDisposable
{
    private readonly string _outputDirectory;
    private readonly Mock<IMediaAnalyzer> _mediaAnalyzer = new();

    public HlsOnDiskPlanReconstructorAudioTests()
    {
        _outputDirectory = Path.Combine(
            path1: Path.GetTempPath(),
            path2: $"nomercy-audio-reconstruct-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(path: _outputDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _outputDirectory))
            Directory.Delete(path: _outputDirectory, recursive: true);
    }

    [Fact]
    public async Task ReconstructAsync_OldStyleAudioDirNoCodecSuffix_ParsesLanguageCorrectly()
    {
        IStorage storage = TestStorageFactory.CreateLocal();

        WriteAudioVariant(subDirectory: "audio_jpn", name: "audio_jpn", segmentBytes: 60_000);
        WriteVideoVariant(subDirectory: "video_1920x1080_SDR", name: "video_1920x1080_SDR", segmentBytes: 300_000);
        SetupVideoProbe(dirName: "video_1920x1080_SDR", codec: "hevc", width: 1920, height: 1080, bitDepth: 8, colorTransfer: "bt709");

        HlsOnDiskPlanReconstructor reconstructor = new(mediaAnalyzer: _mediaAnalyzer.Object);
        OutputPlan plan = await reconstructor.ReconstructAsync(
            storage: storage,
            outputDirectory: _outputDirectory,
            ct: CancellationToken.None
        );

        AudioOutputPlan? audio = plan.AudioOutputs.FirstOrDefault();
        audio.Should().NotBeNull();
        audio!.Language.Should().Be(expected: "jpn");
        audio.EncoderName.Should().Be(expected: "aac");
    }

    [Fact]
    public async Task ReconstructAsync_NewStyleAudioDirWithCodecSuffix_ParsesLanguageAndCodec()
    {
        IStorage storage = TestStorageFactory.CreateLocal();

        WriteAudioVariant(subDirectory: "audio_jpn_aac", name: "audio_jpn_aac", segmentBytes: 60_000);
        WriteVideoVariant(subDirectory: "video_1920x1080_SDR", name: "video_1920x1080_SDR", segmentBytes: 300_000);
        SetupVideoProbe(dirName: "video_1920x1080_SDR", codec: "hevc", width: 1920, height: 1080, bitDepth: 8, colorTransfer: "bt709");

        HlsOnDiskPlanReconstructor reconstructor = new(mediaAnalyzer: _mediaAnalyzer.Object);
        OutputPlan plan = await reconstructor.ReconstructAsync(
            storage: storage,
            outputDirectory: _outputDirectory,
            ct: CancellationToken.None
        );

        AudioOutputPlan? audio = plan.AudioOutputs.FirstOrDefault();
        audio.Should().NotBeNull();
        audio!.Language.Should().Be(expected: "jpn");
        audio.EncoderName.Should().Be(expected: "aac");
    }

    [Fact]
    public async Task ReconstructAsync_MultipleLanguagesOldStyle_AllParsedCorrectly()
    {
        IStorage storage = TestStorageFactory.CreateLocal();

        WriteAudioVariant(subDirectory: "audio_eng", name: "audio_eng", segmentBytes: 60_000);
        WriteAudioVariant(subDirectory: "audio_fra", name: "audio_fra", segmentBytes: 60_000);
        WriteAudioVariant(subDirectory: "audio_jpn", name: "audio_jpn", segmentBytes: 60_000);
        WriteVideoVariant(subDirectory: "video_1920x1080_SDR", name: "video_1920x1080_SDR", segmentBytes: 300_000);
        SetupVideoProbe(dirName: "video_1920x1080_SDR", codec: "hevc", width: 1920, height: 1080, bitDepth: 8, colorTransfer: "bt709");

        HlsOnDiskPlanReconstructor reconstructor = new(mediaAnalyzer: _mediaAnalyzer.Object);
        OutputPlan plan = await reconstructor.ReconstructAsync(
            storage: storage,
            outputDirectory: _outputDirectory,
            ct: CancellationToken.None
        );

        plan.AudioOutputs.Should().HaveCount(expected: 3);

        string[] languages = plan.AudioOutputs
            .Select(selector: a => a.Language ?? string.Empty)
            .OrderBy(keySelector: l => l)
            .ToArray();
        languages.Should().Equal(expected: ["eng", "fra", "jpn"]);

        foreach (AudioOutputPlan audio in plan.AudioOutputs)
        {
            audio.EncoderName.Should().Be(expected: "aac");
        }
    }

    [Fact]
    public async Task ReconstructAsync_MixedOldAndNewStyleAudio_AllParsedWithCorrectCodecs()
    {
        IStorage storage = TestStorageFactory.CreateLocal();

        WriteAudioVariant(subDirectory: "audio_eng_aac", name: "audio_eng_aac", segmentBytes: 60_000);
        WriteAudioVariant(subDirectory: "audio_fra", name: "audio_fra", segmentBytes: 60_000);
        WriteAudioVariant(subDirectory: "audio_jpn_opus", name: "audio_jpn_opus", segmentBytes: 60_000);
        WriteVideoVariant(subDirectory: "video_1920x1080_SDR", name: "video_1920x1080_SDR", segmentBytes: 300_000);
        SetupVideoProbe(dirName: "video_1920x1080_SDR", codec: "hevc", width: 1920, height: 1080, bitDepth: 8, colorTransfer: "bt709");

        HlsOnDiskPlanReconstructor reconstructor = new(mediaAnalyzer: _mediaAnalyzer.Object);
        OutputPlan plan = await reconstructor.ReconstructAsync(
            storage: storage,
            outputDirectory: _outputDirectory,
            ct: CancellationToken.None
        );

        AudioOutputPlan[] audioByLang = plan.AudioOutputs
            .OrderBy(keySelector: a => a.Language ?? string.Empty)
            .ToArray();

        audioByLang.Should().HaveCount(expected: 3);
        audioByLang[0].Language.Should().Be(expected: "eng");
        audioByLang[0].EncoderName.Should().Be(expected: "aac");
        audioByLang[1].Language.Should().Be(expected: "fra");
        audioByLang[1].EncoderName.Should().Be(expected: "aac");
        audioByLang[2].Language.Should().Be(expected: "jpn");
        audioByLang[2].EncoderName.Should().Be(expected: "opus");
    }

    [Fact]
    public async Task ReconstructAsync_AudioWithChannelSuffix_StillParsesLanguageCorrectly()
    {
        IStorage storage = TestStorageFactory.CreateLocal();

        WriteAudioVariant(subDirectory: "audio_eng_aac_2", name: "audio_eng_aac_2", segmentBytes: 60_000);
        WriteVideoVariant(subDirectory: "video_1920x1080_SDR", name: "video_1920x1080_SDR", segmentBytes: 300_000);
        SetupVideoProbe(dirName: "video_1920x1080_SDR", codec: "hevc", width: 1920, height: 1080, bitDepth: 8, colorTransfer: "bt709");

        HlsOnDiskPlanReconstructor reconstructor = new(mediaAnalyzer: _mediaAnalyzer.Object);
        OutputPlan plan = await reconstructor.ReconstructAsync(
            storage: storage,
            outputDirectory: _outputDirectory,
            ct: CancellationToken.None
        );

        AudioOutputPlan? audio = plan.AudioOutputs.FirstOrDefault();
        audio.Should().NotBeNull();
        audio!.Language.Should().Be(expected: "eng");
        audio.EncoderName.Should().Be(expected: "aac");
    }

    [Fact]
    public async Task ReconstructAsync_InvalidAudioDirName_SkipsInvalidName()
    {
        IStorage storage = TestStorageFactory.CreateLocal();

        Directory.CreateDirectory(path: Path.Combine(path1: _outputDirectory, path2: "audio_123"));
        WriteAudioVariant(subDirectory: "audio_eng", name: "audio_eng", segmentBytes: 60_000);
        WriteVideoVariant(subDirectory: "video_1920x1080_SDR", name: "video_1920x1080_SDR", segmentBytes: 300_000);
        SetupVideoProbe(dirName: "video_1920x1080_SDR", codec: "hevc", width: 1920, height: 1080, bitDepth: 8, colorTransfer: "bt709");

        HlsOnDiskPlanReconstructor reconstructor = new(mediaAnalyzer: _mediaAnalyzer.Object);
        OutputPlan plan = await reconstructor.ReconstructAsync(
            storage: storage,
            outputDirectory: _outputDirectory,
            ct: CancellationToken.None
        );

        plan.AudioOutputs.Should().HaveCount(expected: 1);
        plan.AudioOutputs[0].Language.Should().Be(expected: "eng");
    }

    [Fact]
    public async Task ReconstructAsync_NoAudioDirs_ReturnsEmptyAudioOutputs()
    {
        IStorage storage = TestStorageFactory.CreateLocal();

        WriteVideoVariant(subDirectory: "video_1920x1080_SDR", name: "video_1920x1080_SDR", segmentBytes: 300_000);
        SetupVideoProbe(dirName: "video_1920x1080_SDR", codec: "hevc", width: 1920, height: 1080, bitDepth: 8, colorTransfer: "bt709");

        HlsOnDiskPlanReconstructor reconstructor = new(mediaAnalyzer: _mediaAnalyzer.Object);
        OutputPlan plan = await reconstructor.ReconstructAsync(
            storage: storage,
            outputDirectory: _outputDirectory,
            ct: CancellationToken.None
        );

        plan.AudioOutputs.Should().BeEmpty();
    }

    [Fact]
    public async Task ReconstructAsync_AudioMetricsNonZero_AudioIncludedInMaster()
    {
        IStorage storage = TestStorageFactory.CreateLocal();

        WriteAudioVariant(subDirectory: "audio_eng", name: "audio_eng", segmentBytes: 60_000);
        WriteVideoVariant(subDirectory: "video_1920x1080_SDR", name: "video_1920x1080_SDR", segmentBytes: 300_000);
        SetupVideoProbe(dirName: "video_1920x1080_SDR", codec: "hevc", width: 1920, height: 1080, bitDepth: 8, colorTransfer: "bt709");

        HlsOnDiskPlanReconstructor reconstructor = new(mediaAnalyzer: _mediaAnalyzer.Object);
        OutputPlan plan = await reconstructor.ReconstructAsync(
            storage: storage,
            outputDirectory: _outputDirectory,
            ct: CancellationToken.None
        );

        HlsOutputStrategy strategy = new(storage: storage);
        await strategy.FinalizeAsync(outputDirectory: _outputDirectory, plan: plan, mediaTitle: "Title", ct: CancellationToken.None);

        string master = await File.ReadAllTextAsync(path: Path.Combine(path1: _outputDirectory, path2: "Title.m3u8"));

        master.Should().Contain(expected: "#EXT-X-MEDIA:TYPE=AUDIO");
        master.Should().Contain(expected: "LANGUAGE=\"eng\"");
        master.Should().Contain(expected: "GROUP-ID=\"audio_aac\"");
    }

    private void WriteAudioVariant(string subDirectory, string name, int segmentBytes)
    {
        string variantDirectory = Path.Combine(path1: _outputDirectory, path2: subDirectory);
        Directory.CreateDirectory(path: variantDirectory);

        byte[] segment = new byte[segmentBytes];
        File.WriteAllBytes(path: Path.Combine(path1: variantDirectory, path2: $"{name}_00000.m4s"), bytes: segment);

        string playlist = $"#EXTM3U\n#EXTINF:6.000000,\n{name}_00000.m4s\n#EXT-X-ENDLIST\n";
        File.WriteAllText(path: Path.Combine(path1: variantDirectory, path2: $"{name}.m3u8"), contents: playlist);
    }

    private void WriteVideoVariant(string subDirectory, string name, int segmentBytes)
    {
        string variantDirectory = Path.Combine(path1: _outputDirectory, path2: subDirectory);
        Directory.CreateDirectory(path: variantDirectory);

        byte[] segment = new byte[segmentBytes];
        File.WriteAllBytes(path: Path.Combine(path1: variantDirectory, path2: $"{name}_00000.m4s"), bytes: segment);

        string playlist = $"#EXTM3U\n#EXTINF:6.000000,\n{name}_00000.m4s\n#EXT-X-ENDLIST\n";
        File.WriteAllText(path: Path.Combine(path1: variantDirectory, path2: $"{name}.m3u8"), contents: playlist);

        File.WriteAllBytes(path: Path.Combine(path1: variantDirectory, path2: "init.mp4"), bytes: new byte[512]);
    }

    private void SetupVideoProbe(
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
                    ColorPrimaries: "bt709",
                    ColorTransfer: colorTransfer,
                    ColorSpace: "bt709",
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
}
