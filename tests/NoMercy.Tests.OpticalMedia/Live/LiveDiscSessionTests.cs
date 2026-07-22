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
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Live;

namespace NoMercy.Tests.OpticalMedia.Live;

/// <summary>
/// REQUIREMENT: <see cref="LiveDiscSession"/> must build the correct ffmpeg
/// input URL per disc type — <c>bluray:&lt;mount&gt;/?playlist=N</c> for
/// Blu-ray, <c>&lt;mount&gt;/</c> for DVD, the raw drive path for CD — probe
/// it via <see cref="IMediaAnalyzer"/>, and hand off to
/// <see cref="ILiveEncoder.StartAsync"/> with that probed <see cref="MediaInfo"/>.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class LiveDiscSessionTests
{
    private static MediaInfo MakeMediaInfo(string path) =>
        new(
            FilePath: path,
            Format: "mpegts",
            Duration: TimeSpan.FromMinutes(minutes: 90),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 0,
            VideoStreams: [],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static (
        LiveDiscSession Session,
        Mock<IMediaAnalyzer> Analyzer,
        Mock<ILiveEncoder> Encoder
    ) MakeSut()
    {
        Mock<IMediaAnalyzer> analyzerMock = new();
        Mock<ILiveEncoder> encoderMock = new();
        LiveDiscSession session = new(
            mediaAnalyzer: analyzerMock.Object,
            liveEncoder: encoderMock.Object,
            logger: NullLogger<LiveDiscSession>.Instance
        );
        return (session, analyzerMock, encoderMock);
    }

    [Theory]
    [InlineData(data: [OpticalDiscType.BluRay, "D:\\", 3, "bluray:D:/?playlist=3"])]
    [InlineData(data: [OpticalDiscType.Dvd, "D:\\", 1, "D:/"])]
    [InlineData(data: [OpticalDiscType.Cd, "/dev/sr0", 2, "/dev/sr0"])]
    [InlineData(data: [OpticalDiscType.None, "D:\\", 0, "D:"])]
    public async Task StartAsync_BuildsInputPathForDiscType(
        OpticalDiscType discType,
        string drivePath,
        int titleIndex,
        string expectedInputPath
    )
    {
        (
            LiveDiscSession session,
            Mock<IMediaAnalyzer> analyzerMock,
            Mock<ILiveEncoder> encoderMock
        ) = MakeSut();

        string? capturedPath = null;
        analyzerMock
            .Setup(expression: a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>(action: (path, _) => capturedPath = path)
            .ReturnsAsync(valueFunction: (string path, CancellationToken _) => MakeMediaInfo(path: path));

        encoderMock
            .Setup(expression: e => e.StartAsync(It.IsAny<LiveEncodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: Mock.Of<ILiveSession>());

        DiscDrive drive = new(Path: drivePath, Label: "LABEL", HasDisc: true, DiscType: discType);
        await session.StartAsync(drive: drive, titleIndex: titleIndex, startPosition: TimeSpan.Zero, preferredQuality: null, ct: CancellationToken.None);

        capturedPath.Should().Be(expected: expectedInputPath);
    }

    [Fact]
    public async Task StartAsync_PassesProbedMediaInfoIntoLiveEncodeRequest()
    {
        (
            LiveDiscSession session,
            Mock<IMediaAnalyzer> analyzerMock,
            Mock<ILiveEncoder> encoderMock
        ) = MakeSut();

        MediaInfo probedInfo = MakeMediaInfo(path: "bluray:D:/?playlist=1");
        analyzerMock
            .Setup(expression: a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: probedInfo);

        LiveEncodeRequest? capturedRequest = null;
        encoderMock
            .Setup(expression: e => e.StartAsync(It.IsAny<LiveEncodeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LiveEncodeRequest, CancellationToken>(action: (req, _) => capturedRequest = req)
            .ReturnsAsync(value: Mock.Of<ILiveSession>());

        DiscDrive drive = new(Path: "D:\\", Label: "LABEL", HasDisc: true, DiscType: OpticalDiscType.BluRay);
        TimeSpan startPosition = TimeSpan.FromMinutes(minutes: 5);
        await session.StartAsync(drive: drive, titleIndex: 1, startPosition: startPosition, preferredQuality: "1080p", ct: CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.CachedInfo.Should().BeSameAs(expected: probedInfo);
        capturedRequest.StartPosition.Should().Be(expected: startPosition);
        capturedRequest.PreferredQuality.Should().Be(expected: "1080p");
        capturedRequest.Client.MaxWidth.Should().Be(expected: 1920);
        capturedRequest.Client.MaxHeight.Should().Be(expected: 1080);
    }

    [Fact]
    public async Task StartAsync_ReturnsSessionFromLiveEncoder()
    {
        (
            LiveDiscSession session,
            Mock<IMediaAnalyzer> analyzerMock,
            Mock<ILiveEncoder> encoderMock
        ) = MakeSut();

        analyzerMock
            .Setup(expression: a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: MakeMediaInfo(path: "D:/"));

        ILiveSession expectedSession = Mock.Of<ILiveSession>();
        encoderMock
            .Setup(expression: e => e.StartAsync(It.IsAny<LiveEncodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: expectedSession);

        DiscDrive drive = new(Path: "D:\\", Label: "LABEL", HasDisc: true, DiscType: OpticalDiscType.Dvd);
        ILiveSession result = await session.StartAsync(
            drive: drive,
            titleIndex: 1,
            startPosition: TimeSpan.Zero,
            preferredQuality: null,
            ct: CancellationToken.None
        );

        result.Should().BeSameAs(expected: expectedSession);
    }

    [Fact]
    public async Task StartAsync_TrimsTrailingSeparatorsFromDrivePath()
    {
        (
            LiveDiscSession session,
            Mock<IMediaAnalyzer> analyzerMock,
            Mock<ILiveEncoder> encoderMock
        ) = MakeSut();

        string? capturedPath = null;
        analyzerMock
            .Setup(expression: a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>(action: (path, _) => capturedPath = path)
            .ReturnsAsync(valueFunction: (string path, CancellationToken _) => MakeMediaInfo(path: path));
        encoderMock
            .Setup(expression: e => e.StartAsync(It.IsAny<LiveEncodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: Mock.Of<ILiveSession>());

        DiscDrive drive = new(Path: "/media/bluray///", Label: "LABEL", HasDisc: true, DiscType: OpticalDiscType.BluRay);
        await session.StartAsync(drive: drive, titleIndex: 5, startPosition: TimeSpan.Zero, preferredQuality: null, ct: CancellationToken.None);

        capturedPath.Should().Be(expected: "bluray:/media/bluray/?playlist=5");
    }
}
