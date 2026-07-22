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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Sources;
using NoMercy.OpticalMedia.Sources.AudioCd;
using NoMercy.Storage;

namespace NoMercy.Tests.OpticalMedia.Sources;

[Trait(name: "Category", value: "Unit")]
public class CdDiscSourceTests
{
    private static CdDiscSource MakeSut(
        IProcessRunner? processRunner = null,
        IStorageDriver? storageDriver = null
    )
    {
        EncoderOptions options = new() { FfprobePathOverride = "ffprobe" };
        IProcessRunner runner = processRunner ?? Mock.Of<IProcessRunner>();
        IStorageDriver driver = storageDriver ?? Mock.Of<IStorageDriver>();
        return new(options: options, processRunner: runner, storageDriver: driver, logger: NullLogger<CdDiscSource>.Instance);
    }

    // ── ParseTracks (static, internal — accessible via InternalsVisibleTo) ─

    [Fact]
    public void ParseTracks_EmptyStreams_ReturnsEmpty()
    {
        string json = """{"streams": []}""";
        DiscTrack[] tracks = CdDiscSource.ParseTracks(json: json);
        tracks.Should().BeEmpty();
    }

    [Fact]
    public void ParseTracks_NoStreamsProperty_ReturnsEmpty()
    {
        string json = """{"format":{}}""";
        DiscTrack[] tracks = CdDiscSource.ParseTracks(json: json);
        tracks.Should().BeEmpty();
    }

    [Fact]
    public void ParseTracks_NonAudioStream_IsSkipped()
    {
        string json = """
            {
              "streams": [
                { "index": 0, "codec_type": "video", "duration": "5.0" }
              ]
            }
            """;

        DiscTrack[] tracks = CdDiscSource.ParseTracks(json: json);
        tracks.Should().BeEmpty();
    }

    [Fact]
    public void ParseTracks_AudioStream_ParsesDurationAndIndex()
    {
        string json = """
            {
              "streams": [
                {
                  "index": 0,
                  "codec_type": "audio",
                  "duration": "240.5",
                  "sample_rate": "44100",
                  "channels": 2
                }
              ]
            }
            """;

        DiscTrack[] tracks = CdDiscSource.ParseTracks(json: json);

        tracks.Should().HaveCount(expected: 1);
        tracks[0].Index.Should().Be(expected: 1);
        tracks[0]
            .Duration.Should()
            .BeCloseTo(nearbyTime: TimeSpan.FromSeconds(value: 240.5), precision: TimeSpan.FromMilliseconds(milliseconds: 1));
        tracks[0].SampleRate.Should().Be(expected: 44100);
        tracks[0].Channels.Should().Be(expected: 2);
    }

    [Fact]
    public void ParseTracks_MultipleAudioStreams_AllParsed()
    {
        string json = """
            {
              "streams": [
                { "index": 0, "codec_type": "audio", "duration": "180.0", "channels": 2 },
                { "index": 1, "codec_type": "audio", "duration": "210.0", "channels": 2 },
                { "index": 2, "codec_type": "audio", "duration": "195.0", "channels": 2 }
              ]
            }
            """;

        DiscTrack[] tracks = CdDiscSource.ParseTracks(json: json);

        tracks.Should().HaveCount(expected: 3);
        tracks[0].Index.Should().Be(expected: 1);
        tracks[1].Index.Should().Be(expected: 2);
        tracks[2].Index.Should().Be(expected: 3);
    }

    // ── EnumerateDataCd uses IStorageDriver, not System.IO ─────────────────

    [Fact]
    public async Task ProbeAsync_DataCdFallback_UsesStorageDriver_NotSystemIo()
    {
        Mock<IProcessRunner> runnerMock = new();
        runnerMock
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ProcessResult(
                    ExitCode: 1,
                    StdOut: "",
                    StdErr: "no tracks",
                    Duration: TimeSpan.Zero
                )
            );

        Mock<IStorageDriver> driverMock = new();
        driverMock
            .Setup(expression: d =>
                d.EnumerateFileSystemEntries(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<SearchOption>()
                )
            )
            .Returns(value: ["D:\\file1.mp3", "D:\\file2.mp3"]);

        driverMock.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: false);

        CdDiscSource sut = MakeSut(processRunner: runnerMock.Object, storageDriver: driverMock.Object);

        DiscDrive drive = new(
            Path: "D:\\",
            Label: "DATA",
            HasDisc: true,
            DiscType: OpticalDiscType.Cd
        );
        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        driverMock.Verify(
            expression: d =>
                d.EnumerateFileSystemEntries(
                    It.IsAny<string>(),
                    "*",
                    SearchOption.TopDirectoryOnly
                ),
            times: Times.Once
        );

        info.AudioTracks.Should().HaveCount(expected: 2);
        info.AudioTracks![0].Title.Should().Be(expected: "file1.mp3");
        info.AudioTracks![1].Title.Should().Be(expected: "file2.mp3");
    }

    [Fact]
    public async Task ProbeAsync_DataCdFallback_SkipsDirectoryEntries()
    {
        Mock<IProcessRunner> runnerMock = new();
        runnerMock
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
            );

        Mock<IStorageDriver> driverMock = new();
        driverMock
            .Setup(expression: d =>
                d.EnumerateFileSystemEntries(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<SearchOption>()
                )
            )
            .Returns(value: ["D:\\subfolder", "D:\\readme.txt"]);

        driverMock.Setup(expression: d => d.DirectoryExists("D:\\subfolder")).Returns(value: true);
        driverMock.Setup(expression: d => d.DirectoryExists("D:\\readme.txt")).Returns(value: false);

        CdDiscSource sut = MakeSut(processRunner: runnerMock.Object, storageDriver: driverMock.Object);

        DiscDrive drive = new(
            Path: "D:\\",
            Label: "DATA",
            HasDisc: true,
            DiscType: OpticalDiscType.Cd
        );
        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        info.AudioTracks.Should().HaveCount(expected: 1);
        info.AudioTracks![0].Title.Should().Be(expected: "readme.txt");
    }

    [Fact]
    public async Task ProbeAsync_WhenAudioTracksFound_DoesNotCallStorageDriver()
    {
        string ffprobeJson = """
            {
              "streams": [
                { "index": 0, "codec_type": "audio", "duration": "200.0", "channels": 2 }
              ]
            }
            """;

        Mock<IProcessRunner> runnerMock = new();
        runnerMock
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ProcessResult(
                    ExitCode: 0,
                    StdOut: ffprobeJson,
                    StdErr: "",
                    Duration: TimeSpan.Zero
                )
            );

        Mock<IStorageDriver> driverMock = new();

        CdDiscSource sut = MakeSut(processRunner: runnerMock.Object, storageDriver: driverMock.Object);

        DiscDrive drive = new(
            Path: "/dev/sr0",
            Label: "AUDIO_CD",
            HasDisc: true,
            DiscType: OpticalDiscType.Cd
        );
        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        driverMock.Verify(
            expression: d =>
                d.EnumerateFileSystemEntries(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<SearchOption>()
                ),
            times: Times.Never
        );

        info.AudioTracks.Should().HaveCount(expected: 1);
    }

    // ── ProbeTitleAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ProbeTitleAsync_ReturnsSkeletonTitleForRequestedIndex()
    {
        CdDiscSource sut = MakeSut();
        DiscDrive drive = new(Path: "D:\\", Label: "AUDIO_CD", HasDisc: true, DiscType: OpticalDiscType.Cd);

        DiscTitle title = await sut.ProbeTitleAsync(drive: drive, titleIndex: 4, ct: CancellationToken.None);

        title.Index.Should().Be(expected: 4);
        title.Name.Should().Be(expected: "Track 04");
        title.Duration.Should().Be(expected: TimeSpan.Zero);
        title.IsMainFeature.Should().BeFalse();
        title.VideoStreams.Should().BeEmpty();
    }

    // ── Malformed ffprobe JSON → empty tracks, not a throw ─────────────────

    [Fact]
    public async Task ProbeAsync_MalformedFfprobeJson_ReturnsEmptyAudioTracks_FallsBackToDataCd()
    {
        Mock<IProcessRunner> runnerMock = new();
        runnerMock
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ProcessResult(
                    ExitCode: 0,
                    StdOut: "{ not valid json",
                    StdErr: "",
                    Duration: TimeSpan.Zero
                )
            );

        Mock<IStorageDriver> driverMock = new();
        driverMock
            .Setup(expression: d =>
                d.EnumerateFileSystemEntries(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<SearchOption>()
                )
            )
            .Returns(value: ["D:\\readme.txt"]);
        driverMock.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: false);

        CdDiscSource sut = MakeSut(processRunner: runnerMock.Object, storageDriver: driverMock.Object);

        DiscDrive drive = new(Path: "D:\\", Label: "DATA", HasDisc: true, DiscType: OpticalDiscType.Cd);
        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        info.AudioTracks.Should()
            .HaveCount(expected: 1, because: "malformed JSON must degrade to the data-CD fallback");
        info.AudioTracks![0].Title.Should().Be(expected: "readme.txt");
    }

    // ── EnumerateDataCd exception handling ─────────────────────────────────

    [Fact]
    public async Task ProbeAsync_DataCdEnumerationThrows_ReturnsEmptyAudioTracks()
    {
        Mock<IProcessRunner> runnerMock = new();
        runnerMock
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
            );

        Mock<IStorageDriver> driverMock = new();
        driverMock
            .Setup(expression: d =>
                d.EnumerateFileSystemEntries(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<SearchOption>()
                )
            )
            .Throws(exception: new UnauthorizedAccessException(message: "access denied"));

        CdDiscSource sut = MakeSut(processRunner: runnerMock.Object, storageDriver: driverMock.Object);

        DiscDrive drive = new(Path: "D:\\", Label: "DATA", HasDisc: true, DiscType: OpticalDiscType.Cd);
        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        info.AudioTracks.Should().BeEmpty(because: "enumeration failure must degrade gracefully, not throw");
    }

    // ── Type property ────────────────────────────────────────────────────────

    [Fact]
    public void Type_IsCd()
    {
        CdDiscSource sut = MakeSut();
        sut.Type.Should().Be(expected: OpticalDiscType.Cd);
    }
}
