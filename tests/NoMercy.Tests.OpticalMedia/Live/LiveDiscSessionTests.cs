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
[Trait("Category", "Unit")]
public class LiveDiscSessionTests
{
    private static MediaInfo MakeMediaInfo(string path) =>
        new(
            FilePath: path,
            Format: "mpegts",
            Duration: TimeSpan.FromMinutes(90),
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
            analyzerMock.Object,
            encoderMock.Object,
            NullLogger<LiveDiscSession>.Instance
        );
        return (session, analyzerMock, encoderMock);
    }

    [Theory]
    [InlineData(OpticalDiscType.BluRay, "D:\\", 3, "bluray:D:/?playlist=3")]
    [InlineData(OpticalDiscType.Dvd, "D:\\", 1, "D:/")]
    [InlineData(OpticalDiscType.Cd, "/dev/sr0", 2, "/dev/sr0")]
    [InlineData(OpticalDiscType.None, "D:\\", 0, "D:")]
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
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((path, _) => capturedPath = path)
            .ReturnsAsync((string path, CancellationToken _) => MakeMediaInfo(path));

        encoderMock
            .Setup(e => e.StartAsync(It.IsAny<LiveEncodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ILiveSession>());

        DiscDrive drive = new(drivePath, "LABEL", true, discType);
        await session.StartAsync(drive, titleIndex, TimeSpan.Zero, null, CancellationToken.None);

        capturedPath.Should().Be(expectedInputPath);
    }

    [Fact]
    public async Task StartAsync_PassesProbedMediaInfoIntoLiveEncodeRequest()
    {
        (
            LiveDiscSession session,
            Mock<IMediaAnalyzer> analyzerMock,
            Mock<ILiveEncoder> encoderMock
        ) = MakeSut();

        MediaInfo probedInfo = MakeMediaInfo("bluray:D:/?playlist=1");
        analyzerMock
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(probedInfo);

        LiveEncodeRequest? capturedRequest = null;
        encoderMock
            .Setup(e => e.StartAsync(It.IsAny<LiveEncodeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LiveEncodeRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(Mock.Of<ILiveSession>());

        DiscDrive drive = new("D:\\", "LABEL", true, OpticalDiscType.BluRay);
        TimeSpan startPosition = TimeSpan.FromMinutes(5);
        await session.StartAsync(drive, 1, startPosition, "1080p", CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.CachedInfo.Should().BeSameAs(probedInfo);
        capturedRequest.StartPosition.Should().Be(startPosition);
        capturedRequest.PreferredQuality.Should().Be("1080p");
        capturedRequest.Client.MaxWidth.Should().Be(1920);
        capturedRequest.Client.MaxHeight.Should().Be(1080);
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
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMediaInfo("D:/"));

        ILiveSession expectedSession = Mock.Of<ILiveSession>();
        encoderMock
            .Setup(e => e.StartAsync(It.IsAny<LiveEncodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSession);

        DiscDrive drive = new("D:\\", "LABEL", true, OpticalDiscType.Dvd);
        ILiveSession result = await session.StartAsync(
            drive,
            1,
            TimeSpan.Zero,
            null,
            CancellationToken.None
        );

        result.Should().BeSameAs(expectedSession);
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
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((path, _) => capturedPath = path)
            .ReturnsAsync((string path, CancellationToken _) => MakeMediaInfo(path));
        encoderMock
            .Setup(e => e.StartAsync(It.IsAny<LiveEncodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ILiveSession>());

        DiscDrive drive = new("/media/bluray///", "LABEL", true, OpticalDiscType.BluRay);
        await session.StartAsync(drive, 5, TimeSpan.Zero, null, CancellationToken.None);

        capturedPath.Should().Be("bluray:/media/bluray/?playlist=5");
    }
}
