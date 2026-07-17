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

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Subtitles;
using NoMercy.Events;
using NoMercy.Events.Encoding;

namespace NoMercy.Encoder.Subscribers;

/// <summary>
/// After every encode completes, inspect the output for bitmap subtitle
/// streams (PGS / VobSub / DVB). When the output is HLS or DASH (detected by
/// the presence of <c>.m3u8</c> / <c>.mpd</c> files next to the output path),
/// schedule a follow-up OCR job per stream so dashboard players don't have to
/// burn the bitmap subtitles or skip them.
///
/// Skips text-based subtitle streams (SRT / WebVTT / ASS) — those are
/// handled by <c>WebVttSegmenter</c> at encode time.
///
/// Opt-out: set <see cref="EncoderOptions.EnableOcrPostEncodeSubscriber"/> = false.
/// </summary>
public sealed class OcrPostEncodeSubscriber : IDisposable
{
    private readonly IMediaAnalyzer _analyzer;
    private readonly ISubtitleOcrEngine? _ocrEngine;
    private readonly EncoderOptions _options;
    private readonly ILogger<OcrPostEncodeSubscriber> _logger;
    private readonly List<IDisposable> _subscriptions = [];

    public OcrPostEncodeSubscriber(
        IEventBus eventBus,
        IMediaAnalyzer analyzer,
        EncoderOptions options,
        ILogger<OcrPostEncodeSubscriber> logger,
        ISubtitleOcrEngine? ocrEngine = null
    )
    {
        _analyzer = analyzer;
        _ocrEngine = ocrEngine;
        _options = options;
        _logger = logger;

        _subscriptions.Add(eventBus.Subscribe<EncodingCompletedEvent>(OnEncodingCompleted));
    }

    internal async Task OnEncodingCompleted(EncodingCompletedEvent @event, CancellationToken ct)
    {
        if (!_options.EnableOcrPostEncodeSubscriber)
            return;

        if (_ocrEngine is null)
        {
            _logger.LogDebug(
                "OcrPostEncode: no ISubtitleOcrEngine registered — skipping job {JobId}",
                @event.JobId
            );
            return;
        }

        if (!IsStreamingOutput(@event.OutputPath))
        {
            _logger.LogDebug(
                "OcrPostEncode: output {OutputPath} is not HLS/DASH — skipping",
                @event.OutputPath
            );
            return;
        }

        MediaInfo info;
        try
        {
            info = await _analyzer.AnalyzeAsync(@event.OutputPath, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "OcrPostEncode: probe failed for {OutputPath}",
                @event.OutputPath
            );
            return;
        }

        foreach (
            (int subtitleIndex, SubtitleStreamInfo stream) in BitmapSubtitleSelector.Select(
                info.SubtitleStreams
            )
        )
        {
            try
            {
                await _ocrEngine
                    .OcrAsync(
                        @event.OutputPath,
                        subtitleIndex,
                        stream.Language ?? "eng",
                        SubtitleCodecType.WebVtt,
                        ct
                    )
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "OcrPostEncode: wrote OCR sidecar for stream {Index} ({Codec}/{Lang}) of job {JobId}",
                    subtitleIndex,
                    stream.Codec,
                    stream.Language,
                    @event.JobId
                );
            }
            catch (Exception ex)
            {
                // Best-effort. OCR failure must never bubble up and fail an
                // already-completed encode.
                _logger.LogWarning(
                    ex,
                    "OcrPostEncode: OCR failed for stream {Index} of job {JobId}",
                    subtitleIndex,
                    @event.JobId
                );
            }
        }
    }

    private static bool IsStreamingOutput(string outputPath)
    {
        // Output path is the master playlist or manifest. The encoder writes
        // .m3u8 for HLS, .mpd for DASH, .mp4/.mkv for non-streaming.
        string ext = Path.GetExtension(outputPath);
        return ext.Equals(".m3u8", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mpd", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        foreach (IDisposable sub in _subscriptions)
            sub.Dispose();
    }
}
