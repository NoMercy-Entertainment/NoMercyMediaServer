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
using NoMercy.OpticalMedia.Sources;
using NoMercy.Storage;

namespace NoMercy.Tests.OpticalMedia.Live;

/// <summary>
/// REQUIREMENT: <see cref="LiveDiscSession"/> must build the correct ffmpeg
/// input URL per disc type — <c>bluray:&lt;mount&gt;/</c> with a
/// <c>-playlist N</c> extra arg for Blu-ray, <c>&lt;mount&gt;/</c> with
/// <c>-f dvdvideo -title N</c> for DVD, the raw drive path with
/// <c>-f libcdio</c> for CD — probe it via <see cref="IMediaAnalyzer"/>, and
/// hand off to <see cref="ILiveEncoder.StartAsync"/> with that probed
/// <see cref="MediaInfo"/>. It must also fan a multi-track audio selection
/// out into per-track rendition children stamped on the streaming runtime,
/// mirroring <c>LiveTranscodeService.StartAudioChildrenAsync</c> for raw
/// multi-audio file sources.
/// </summary>
[Trait("Category", "Unit")]
public class LiveDiscSessionTests
{
    private static readonly AudioTrackSelection[] NoAudioSelection = [];

    private static MediaInfo MakeMediaInfo(
        string path,
        IReadOnlyList<AudioStreamInfo>? audioStreams = null
    ) =>
        new(
            FilePath: path,
            Format: "mpegts",
            Duration: TimeSpan.FromMinutes(90),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 0,
            VideoStreams: [],
            AudioStreams: audioStreams ?? [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static AudioStreamInfo MakeAudioStream(int index, string language) =>
        new(
            Index: index,
            Codec: "ac3",
            Channels: 6,
            SampleRate: 48000,
            BitRateKbps: 448,
            Language: language,
            IsDefault: index == 0,
            IsForced: false
        );

    private static (
        LiveDiscSession Session,
        Mock<IMediaAnalyzer> Analyzer,
        Mock<ILiveEncoder> Encoder,
        Mock<ILiveStreamingService> Streaming
    ) MakeSut()
    {
        Mock<IMediaAnalyzer> analyzerMock = new();
        Mock<ILiveEncoder> encoderMock = new();
        Mock<ILiveStreamingService> streamingMock = new();
        // No test here exercises the mpls-language enrichment path — a
        // default mock's FileExists() returns false, so
        // ApplyMplsAudioLanguages short-circuits back to the probed
        // streams unchanged, same as every existing assertion expects.
        Mock<IStorageDriver> storageDriverMock = new();
        LiveDiscSession session = new(
            analyzerMock.Object,
            encoderMock.Object,
            streamingMock.Object,
            storageDriverMock.Object,
            NullLogger<LiveDiscSession>.Instance
        );
        return (session, analyzerMock, encoderMock, streamingMock);
    }

    [Theory]
    [InlineData(OpticalDiscType.BluRay, "D:\\", 3, "bluray:D:/", "-playlist 3")]
    [InlineData(OpticalDiscType.Dvd, "D:\\", 1, "D:/", "-f dvdvideo -title 1")]
    [InlineData(OpticalDiscType.Cd, "/dev/sr0", 2, "/dev/sr0", "-f libcdio")]
    [InlineData(OpticalDiscType.None, "D:\\", 0, "D:", "")]
    public async Task StartAsync_BuildsInputPathAndExtraArgsForDiscType(
        OpticalDiscType discType,
        string drivePath,
        int titleIndex,
        string expectedInputPath,
        string expectedExtraArgs
    )
    {
        (
            LiveDiscSession session,
            Mock<IMediaAnalyzer> analyzerMock,
            Mock<ILiveEncoder> encoderMock,
            _
        ) = MakeSut();

        string? capturedPath = null;
        string[]? capturedExtraArgs = null;
        analyzerMock
            .Setup(a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, string[], CancellationToken>(
                (path, extraArgs, _) =>
                {
                    capturedPath = path;
                    capturedExtraArgs = extraArgs;
                }
            )
            .ReturnsAsync((string path, string[] _, CancellationToken _) => MakeMediaInfo(path));

        encoderMock
            .Setup(e => e.StartAsync(It.IsAny<LiveEncodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ILiveSession>(s => s.SessionId == "session-1"));

        DiscDrive drive = new(drivePath, "LABEL", true, discType);
        await session.StartAsync(
            drive,
            titleIndex,
            TimeSpan.Zero,
            null,
            NoAudioSelection,
            CancellationToken.None
        );

        capturedPath.Should().Be(expectedInputPath);
        string.Join(' ', capturedExtraArgs ?? []).Should().Be(expectedExtraArgs);
    }

    [Fact]
    public async Task StartAsync_PassesProbedMediaInfoIntoLiveEncodeRequest()
    {
        (
            LiveDiscSession session,
            Mock<IMediaAnalyzer> analyzerMock,
            Mock<ILiveEncoder> encoderMock,
            _
        ) = MakeSut();

        MediaInfo probedInfo = MakeMediaInfo("bluray:D:/");
        analyzerMock
            .Setup(a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(probedInfo);

        LiveEncodeRequest? capturedRequest = null;
        encoderMock
            .Setup(e => e.StartAsync(It.IsAny<LiveEncodeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LiveEncodeRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(Mock.Of<ILiveSession>(s => s.SessionId == "session-1"));

        DiscDrive drive = new("D:\\", "LABEL", true, OpticalDiscType.BluRay);
        TimeSpan startPosition = TimeSpan.FromMinutes(5);
        await session.StartAsync(
            drive,
            1,
            startPosition,
            "1080p",
            NoAudioSelection,
            CancellationToken.None
        );

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
            Mock<ILiveEncoder> encoderMock,
            _
        ) = MakeSut();

        analyzerMock
            .Setup(a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(MakeMediaInfo("D:/"));

        ILiveSession expectedSession = Mock.Of<ILiveSession>(s => s.SessionId == "session-1");
        encoderMock
            .Setup(e => e.StartAsync(It.IsAny<LiveEncodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSession);

        DiscDrive drive = new("D:\\", "LABEL", true, OpticalDiscType.Dvd);
        ILiveSession result = await session.StartAsync(
            drive,
            1,
            TimeSpan.Zero,
            null,
            NoAudioSelection,
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
            Mock<ILiveEncoder> encoderMock,
            _
        ) = MakeSut();

        string? capturedPath = null;
        analyzerMock
            .Setup(a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, string[], CancellationToken>((path, _, _) => capturedPath = path)
            .ReturnsAsync((string path, string[] _, CancellationToken _) => MakeMediaInfo(path));
        encoderMock
            .Setup(e => e.StartAsync(It.IsAny<LiveEncodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ILiveSession>(s => s.SessionId == "session-1"));

        DiscDrive drive = new("/media/bluray///", "LABEL", true, OpticalDiscType.BluRay);
        await session.StartAsync(
            drive,
            5,
            TimeSpan.Zero,
            null,
            NoAudioSelection,
            CancellationToken.None
        );

        capturedPath.Should().Be("bluray:/media/bluray/");
    }

    [Fact]
    public async Task StartAsync_NoAudioSelection_KeepsSingleMuxedSession_NoRenditionsStamped()
    {
        (
            LiveDiscSession session,
            Mock<IMediaAnalyzer> analyzerMock,
            Mock<ILiveEncoder> encoderMock,
            Mock<ILiveStreamingService> streamingMock
        ) = MakeSut();

        analyzerMock
            .Setup(a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(MakeMediaInfo("D:/", [MakeAudioStream(0, "eng")]));

        LiveEncodeRequest? capturedRequest = null;
        encoderMock
            .Setup(e => e.StartAsync(It.IsAny<LiveEncodeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LiveEncodeRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(Mock.Of<ILiveSession>(s => s.SessionId == "session-1"));

        DiscDrive drive = new("D:\\", "LABEL", true, OpticalDiscType.Dvd);
        await session.StartAsync(
            drive,
            1,
            TimeSpan.Zero,
            null,
            NoAudioSelection,
            CancellationToken.None
        );

        capturedRequest!.VideoOnly.Should().BeFalse();
        capturedRequest.AudioStreamIndex.Should().Be(0);
        encoderMock.Verify(
            e =>
                e.StartAudioRenditionAsync(
                    It.IsAny<LiveEncodeRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        streamingMock.Verify(
            s =>
                s.StampAudioRenditions(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<LiveAudioRendition>>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task StartAsync_MultipleSelectedTracks_StartsVideoOnlyPlusOneChildPerTrack_StampsRenditions()
    {
        (
            LiveDiscSession session,
            Mock<IMediaAnalyzer> analyzerMock,
            Mock<ILiveEncoder> encoderMock,
            Mock<ILiveStreamingService> streamingMock
        ) = MakeSut();

        analyzerMock
            .Setup(a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                MakeMediaInfo("bluray:D:/", [MakeAudioStream(0, "eng"), MakeAudioStream(1, "jpn")])
            );

        LiveEncodeRequest? capturedVideoRequest = null;
        encoderMock
            .Setup(e => e.StartAsync(It.IsAny<LiveEncodeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LiveEncodeRequest, CancellationToken>((req, _) => capturedVideoRequest = req)
            .ReturnsAsync(Mock.Of<ILiveSession>(s => s.SessionId == "parent-session"));

        List<LiveEncodeRequest> capturedChildRequests = [];
        int childCounter = 0;
        encoderMock
            .Setup(e =>
                e.StartAudioRenditionAsync(
                    It.IsAny<LiveEncodeRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<LiveEncodeRequest, CancellationToken>(
                (req, _) => capturedChildRequests.Add(req)
            )
            .ReturnsAsync(() =>
            {
                childCounter++;
                return Mock.Of<ILiveSession>(s => s.SessionId == $"child-{childCounter}");
            });

        IReadOnlyList<LiveAudioRendition>? stampedRenditions = null;
        streamingMock
            .Setup(s =>
                s.StampAudioRenditions(
                    "parent-session",
                    It.IsAny<IReadOnlyList<LiveAudioRendition>>()
                )
            )
            .Callback<string, IReadOnlyList<LiveAudioRendition>>(
                (_, renditions) => stampedRenditions = renditions
            );

        DiscDrive drive = new("D:\\", "LABEL", true, OpticalDiscType.BluRay);
        AudioTrackSelection[] selection =
        [
            new(StreamIndex: 1, Include: true),
            new(StreamIndex: 0, Include: true),
            new(StreamIndex: 2, Include: false),
        ];

        ILiveSession result = await session.StartAsync(
            drive,
            1,
            TimeSpan.Zero,
            null,
            selection,
            CancellationToken.None
        );

        result.SessionId.Should().Be("parent-session");
        capturedVideoRequest!.VideoOnly.Should().BeTrue();

        // Selection order is [jpn(1), eng(0)] — jpn is listed first so it opens
        // as the default, matching the caller's own ordering.
        capturedChildRequests.Should().HaveCount(2);
        capturedChildRequests[0].AudioStreamIndex.Should().Be(1);
        capturedChildRequests[1].AudioStreamIndex.Should().Be(0);

        stampedRenditions.Should().NotBeNull();
        stampedRenditions!.Should().HaveCount(2);
        stampedRenditions![0].Language.Should().Be("jpn");
        stampedRenditions[0].IsDefault.Should().BeTrue();
        stampedRenditions[0]
            .Uri.Should()
            .Be("/api/v1/streaming/live/sessions/child-1/playlist.m3u8");
        stampedRenditions[1].Language.Should().Be("eng");
        stampedRenditions[1].IsDefault.Should().BeFalse();

        streamingMock.Verify(
            s =>
                s.StampChildAudioSessions(
                    "parent-session",
                    It.Is<IReadOnlyList<string>>(ids =>
                        ids.SequenceEqual(new[] { "child-1", "child-2" })
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task StartAsync_ExactlyOneSelectedTrack_KeepsSingleMuxedSessionAtThatIndex()
    {
        (
            LiveDiscSession session,
            Mock<IMediaAnalyzer> analyzerMock,
            Mock<ILiveEncoder> encoderMock,
            Mock<ILiveStreamingService> streamingMock
        ) = MakeSut();

        analyzerMock
            .Setup(a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                MakeMediaInfo("bluray:D:/", [MakeAudioStream(0, "eng"), MakeAudioStream(1, "jpn")])
            );

        LiveEncodeRequest? capturedRequest = null;
        encoderMock
            .Setup(e => e.StartAsync(It.IsAny<LiveEncodeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LiveEncodeRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(Mock.Of<ILiveSession>(s => s.SessionId == "session-1"));

        DiscDrive drive = new("D:\\", "LABEL", true, OpticalDiscType.BluRay);
        AudioTrackSelection[] selection = [new(StreamIndex: 1, Include: true)];

        await session.StartAsync(drive, 1, TimeSpan.Zero, null, selection, CancellationToken.None);

        capturedRequest!.VideoOnly.Should().BeFalse();
        capturedRequest.AudioStreamIndex.Should().Be(1);
        encoderMock.Verify(
            e =>
                e.StartAudioRenditionAsync(
                    It.IsAny<LiveEncodeRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        streamingMock.Verify(
            s =>
                s.StampAudioRenditions(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<LiveAudioRendition>>()
                ),
            Times.Never
        );
    }
}
