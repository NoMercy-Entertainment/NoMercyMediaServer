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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Subscribers;
using NoMercy.Encoder.Subtitles;
using NoMercy.Events;
using NoMercy.Events.Encoding;

namespace NoMercy.Tests.Encoder.Subscribers;

public class OcrPostEncodeSubscriberTests
{
    private static MediaInfo BuildMediaInfo(params string[] subtitleCodecs)
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

        return new MediaInfo(
            FilePath: "/out/test.m3u8",
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

    private static OcrPostEncodeSubscriber Build(
        EncoderOptions options,
        Mock<IEventBus> bus,
        Mock<IMediaAnalyzer> analyzer,
        Mock<ISubtitleOcrEngine>? ocrEngine = null
    ) =>
        new(
            bus.Object,
            analyzer.Object,
            options,
            NullLogger<OcrPostEncodeSubscriber>.Instance,
            ocrEngine?.Object
        );

    [Fact]
    public async Task Disabled_subscriber_skips_event()
    {
        Mock<IEventBus> bus = new();
        Mock<IMediaAnalyzer> analyzer = new();
        Mock<ISubtitleOcrEngine> ocr = new();

        EncoderOptions options = new() { EnableOcrPostEncodeSubscriber = false };
        OcrPostEncodeSubscriber subscriber = Build(options, bus, analyzer, ocr);

        await subscriber.OnEncodingCompleted(
            new EncodingCompletedEvent
            {
                JobId = 1,
                OutputPath = "/out/x.m3u8",
                Duration = TimeSpan.FromMinutes(1),
            },
            CancellationToken.None
        );

        analyzer.VerifyNoOtherCalls();
        ocr.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Bitmap_subtitle_in_HLS_output_triggers_OCR()
    {
        Mock<IEventBus> bus = new();
        Mock<IMediaAnalyzer> analyzer = new();
        Mock<ISubtitleOcrEngine> ocr = new();

        analyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildMediaInfo("hdmv_pgs_subtitle"));

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

        OcrPostEncodeSubscriber subscriber = Build(new EncoderOptions(), bus, analyzer, ocr);

        await subscriber.OnEncodingCompleted(
            new EncodingCompletedEvent
            {
                JobId = 7,
                OutputPath = "/out/master.m3u8",
                Duration = TimeSpan.FromMinutes(60),
            },
            CancellationToken.None
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
    }

    [Fact]
    public async Task Text_subtitle_streams_are_skipped()
    {
        Mock<IEventBus> bus = new();
        Mock<IMediaAnalyzer> analyzer = new();
        Mock<ISubtitleOcrEngine> ocr = new();

        analyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildMediaInfo("subrip", "ass"));

        OcrPostEncodeSubscriber subscriber = Build(new EncoderOptions(), bus, analyzer, ocr);

        await subscriber.OnEncodingCompleted(
            new EncodingCompletedEvent
            {
                JobId = 7,
                OutputPath = "/out/master.m3u8",
                Duration = TimeSpan.FromMinutes(60),
            },
            CancellationToken.None
        );

        ocr.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NonStreaming_output_is_skipped()
    {
        Mock<IEventBus> bus = new();
        Mock<IMediaAnalyzer> analyzer = new();
        Mock<ISubtitleOcrEngine> ocr = new();

        OcrPostEncodeSubscriber subscriber = Build(new EncoderOptions(), bus, analyzer, ocr);

        await subscriber.OnEncodingCompleted(
            new EncodingCompletedEvent
            {
                JobId = 7,
                OutputPath = "/out/movie.mp4",
                Duration = TimeSpan.FromMinutes(60),
            },
            CancellationToken.None
        );

        analyzer.VerifyNoOtherCalls();
        ocr.VerifyNoOtherCalls();
    }
}
