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
using NoMercy.Api.EventHandlers;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Subscribers;
using NoMercy.Encoder.Subtitles;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.Networking.Messaging;

namespace NoMercy.Tests.Encoder.Subscribers;

public class OcrAndSignalRJourneyTests
{
    private static MediaInfo HlsMediaInfo(params string[] subtitleCodecs)
    {
        List<SubtitleStreamInfo> subs = subtitleCodecs
            .Select(
                (codec, idx) =>
                    new SubtitleStreamInfo(
                        Index: idx,
                        Codec: codec,
                        Language: "eng",
                        IsDefault: false,
                        IsForced: false
                    )
            )
            .ToList();

        return new(
            FilePath: "/out/master.m3u8",
            Format: "mpegts",
            Duration: TimeSpan.FromMinutes(60),
            OverallBitRateKbps: 0,
            FileSizeBytes: 0,
            VideoStreams: [],
            AudioStreams: [],
            SubtitleStreams: subs,
            Chapters: []
        );
    }

    [Fact]
    public async Task HlsWithBitmapSubtitle_OcrFires_AndSignalRCompletedSent()
    {
        InMemoryEventBus bus = new();

        Mock<IMediaAnalyzer> analyzer = new();
        analyzer
            .Setup(a => a.AnalyzeAsync("/out/master.m3u8", It.IsAny<CancellationToken>()))
            .ReturnsAsync(HlsMediaInfo("hdmv_pgs_subtitle"));

        Mock<ISubtitleOcrEngine> ocr = new();
        ocr.Setup(o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new SubtitleTrack("/out/sub.vtt", "eng", SubtitleCodecType.WebVtt, 10));

        Mock<IClientMessenger> messenger = new();
        messenger
            .Setup(m => m.SendToAll(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        using OcrPostEncodeSubscriber ocrSubscriber = new(
            bus,
            analyzer.Object,
            new(),
            NullLogger<OcrPostEncodeSubscriber>.Instance,
            ocr.Object
        );

        using SignalREncodingEventHandler signalRHandler = new(
            NullLogger<SignalREncodingEventHandler>.Instance,
            bus,
            messenger.Object
        );

        await bus.PublishAsync(
            new EncodingCompletedEvent
            {
                JobId = 42,
                OutputPath = "/out/master.m3u8",
                Duration = TimeSpan.FromMinutes(60),
            }
        );

        ocr.Verify(
            o =>
                o.OcrAsync(
                    "/out/master.m3u8",
                    0,
                    "eng",
                    SubtitleCodecType.WebVtt,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        messenger.Verify(
            m => m.SendToAll("EncodingCompleted", "dashboardHub", It.IsAny<object>()),
            Times.Once
        );
    }

    [Fact]
    public async Task NonHlsOutput_OcrSkipped_SignalRCompletedStillSent()
    {
        InMemoryEventBus bus = new();

        Mock<IMediaAnalyzer> analyzer = new();
        Mock<ISubtitleOcrEngine> ocr = new();

        Mock<IClientMessenger> messenger = new();
        messenger
            .Setup(m => m.SendToAll(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        using OcrPostEncodeSubscriber ocrSubscriber = new(
            bus,
            analyzer.Object,
            new(),
            NullLogger<OcrPostEncodeSubscriber>.Instance,
            ocr.Object
        );

        using SignalREncodingEventHandler signalRHandler = new(
            NullLogger<SignalREncodingEventHandler>.Instance,
            bus,
            messenger.Object
        );

        await bus.PublishAsync(
            new EncodingCompletedEvent
            {
                JobId = 7,
                OutputPath = "/out/movie.mp4",
                Duration = TimeSpan.FromMinutes(90),
            }
        );

        analyzer.VerifyNoOtherCalls();
        ocr.VerifyNoOtherCalls();

        messenger.Verify(
            m => m.SendToAll("EncodingCompleted", "dashboardHub", It.IsAny<object>()),
            Times.Once
        );
    }

    [Fact]
    public async Task HlsWithNoBitmapSubs_OcrNotCalled_SignalRCompletedSent()
    {
        InMemoryEventBus bus = new();

        Mock<IMediaAnalyzer> analyzer = new();
        analyzer
            .Setup(a => a.AnalyzeAsync("/out/master.m3u8", It.IsAny<CancellationToken>()))
            .ReturnsAsync(HlsMediaInfo("subrip", "ass"));

        Mock<ISubtitleOcrEngine> ocr = new();

        Mock<IClientMessenger> messenger = new();
        messenger
            .Setup(m => m.SendToAll(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        using OcrPostEncodeSubscriber ocrSubscriber = new(
            bus,
            analyzer.Object,
            new(),
            NullLogger<OcrPostEncodeSubscriber>.Instance,
            ocr.Object
        );

        using SignalREncodingEventHandler signalRHandler = new(
            NullLogger<SignalREncodingEventHandler>.Instance,
            bus,
            messenger.Object
        );

        await bus.PublishAsync(
            new EncodingCompletedEvent
            {
                JobId = 9,
                OutputPath = "/out/master.m3u8",
                Duration = TimeSpan.FromMinutes(45),
            }
        );

        ocr.VerifyNoOtherCalls();

        messenger.Verify(
            m => m.SendToAll("EncodingCompleted", "dashboardHub", It.IsAny<object>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SingleEncodingCompletedEvent_TriggersOcrArm_AndSignalRArm_Independently()
    {
        InMemoryEventBus bus = new();

        List<string> reactionOrder = [];

        Mock<IMediaAnalyzer> analyzer = new();
        analyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HlsMediaInfo("hdmv_pgs_subtitle"))
            .Callback(() => reactionOrder.Add("ocr-analyzer"));

        Mock<ISubtitleOcrEngine> ocr = new();
        ocr.Setup(o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new SubtitleTrack("/out/sub.vtt", "eng", SubtitleCodecType.WebVtt, 10))
            .Callback(() => reactionOrder.Add("ocr-engine"));

        Mock<IClientMessenger> messenger = new();
        messenger
            .Setup(m => m.SendToAll(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask)
            .Callback<string, string, object>(
                (name, _, _) =>
                {
                    if (name == "EncodingCompleted")
                        reactionOrder.Add("signalr-completed");
                }
            );

        using OcrPostEncodeSubscriber ocrSubscriber = new(
            bus,
            analyzer.Object,
            new(),
            NullLogger<OcrPostEncodeSubscriber>.Instance,
            ocr.Object
        );

        using SignalREncodingEventHandler signalRHandler = new(
            NullLogger<SignalREncodingEventHandler>.Instance,
            bus,
            messenger.Object
        );

        await bus.PublishAsync(
            new EncodingCompletedEvent
            {
                JobId = 55,
                OutputPath = "/out/master.m3u8",
                Duration = TimeSpan.FromMinutes(60),
            }
        );

        reactionOrder.Should().Contain("ocr-analyzer");
        reactionOrder.Should().Contain("ocr-engine");
        reactionOrder.Should().Contain("signalr-completed");
        reactionOrder.Should().HaveCount(3);
    }

    [Fact]
    public async Task DisposeOcrSubscriber_SignalRStillReceivesEvent()
    {
        InMemoryEventBus bus = new();

        Mock<IMediaAnalyzer> analyzer = new();
        Mock<ISubtitleOcrEngine> ocr = new();

        Mock<IClientMessenger> messenger = new();
        messenger
            .Setup(m => m.SendToAll(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        OcrPostEncodeSubscriber ocrSubscriber = new(
            bus,
            analyzer.Object,
            new(),
            NullLogger<OcrPostEncodeSubscriber>.Instance,
            ocr.Object
        );

        using SignalREncodingEventHandler signalRHandler = new(
            NullLogger<SignalREncodingEventHandler>.Instance,
            bus,
            messenger.Object
        );

        ocrSubscriber.Dispose();

        await bus.PublishAsync(
            new EncodingCompletedEvent
            {
                JobId = 99,
                OutputPath = "/out/master.m3u8",
                Duration = TimeSpan.FromMinutes(30),
            }
        );

        analyzer.VerifyNoOtherCalls();
        ocr.VerifyNoOtherCalls();

        messenger.Verify(
            m => m.SendToAll("EncodingCompleted", "dashboardHub", It.IsAny<object>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SignalRCompletedPayload_CarriesCorrectJobIdOutputPathDuration()
    {
        InMemoryEventBus bus = new();

        Mock<IMediaAnalyzer> analyzer = new();
        analyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HlsMediaInfo());

        Mock<ISubtitleOcrEngine> ocr = new();

        object? capturedPayload = null;
        Mock<IClientMessenger> messenger = new();
        messenger
            .Setup(m => m.SendToAll("EncodingCompleted", "dashboardHub", It.IsAny<object>()))
            .Callback<string, string, object>((_, _, payload) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        using OcrPostEncodeSubscriber ocrSubscriber = new(
            bus,
            analyzer.Object,
            new(),
            NullLogger<OcrPostEncodeSubscriber>.Instance,
            ocr.Object
        );

        using SignalREncodingEventHandler signalRHandler = new(
            NullLogger<SignalREncodingEventHandler>.Instance,
            bus,
            messenger.Object
        );

        TimeSpan duration = TimeSpan.FromMinutes(120);

        await bus.PublishAsync(
            new EncodingCompletedEvent
            {
                JobId = 77,
                OutputPath = "/out/feature.m3u8",
                Duration = duration,
            }
        );

        capturedPayload.Should().NotBeNull();

        Api.DTOs.Encoding.EncodingCompletedDto dto = capturedPayload
            .Should()
            .BeOfType<Api.DTOs.Encoding.EncodingCompletedDto>()
            .Subject;

        dto.Id.Should().Be(77);
        dto.OutputPath.Should().Be("/out/feature.m3u8");
        dto.Duration.Should().BeApproximately(duration.TotalSeconds, precision: 0.001);
    }
}
