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

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.ContentAnalysis;
using NoMercy.Encoder.ContentAnalysis.Fingerprinting;
using NoMercy.Encoder.Subtitles;
using NoMercy.Storage;

namespace NoMercy.Api.Controllers.V1.Dashboard.Analysis;

/// <summary>
/// On-demand content-analysis probes. Useful when dialing in profiles
/// (does this source actually have letterbox bars?) or debugging the
/// auto-detection pipeline without kicking off a full encode.
/// </summary>
[ApiController]
[Tags(tags: "Dashboard Content Analysis")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "Owner")]
[Route(template: "api/v{version:apiVersion}/dashboard/content-analysis")]
public class ContentAnalysisController(
    ICropDetector cropDetector,
    ISubtitleOcrEngine? ocrEngine,
    IWhisperTranscriber? whisperTranscriber,
    IAudioFingerprinter fingerprinter,
    IIntroDetector introDetector,
    IVideoFileRepository videoFileRepository,
    IStorageDriver storageDriver
) : BaseController
{
    /// <summary>
    /// Runs the crop detector against a VideoFile by id and returns the
    /// detected rectangle (or <c>should_crop=false</c> if the frame is
    /// already letterbox-free). Ffmpeg-bound — can take up to 60 seconds
    /// on large sources. Owner-only to avoid DoS-by-probe.
    /// </summary>
    /// <remarks>
    /// <b>Deprecated.</b> Use <c>POST /api/v1/encoder/content-analysis/crop/{videoFileId}</c>
    /// instead. This alias will be removed in a future release.
    /// </remarks>
    [HttpGet(template: "crop/{videoFileId}")]
    public async Task<IActionResult> DetectCrop(string videoFileId, CancellationToken ct)
    {
        if (!Ulid.TryParse(base32: videoFileId, ulid: out Ulid fileId))
            return BadRequestResponse(detail: "Invalid video file id");

        VideoFile? file = await videoFileRepository.GetByIdAsync(id: fileId, ct: ct);

        if (file is null)
            return NotFoundResponse(detail: "Video file not found");

        string path = storageDriver.CombinePath(parent: file.HostFolder, child: file.Filename);
        if (!storageDriver.FileExists(path: path))
            return NotFoundResponse(detail: $"Source file missing on disk: {path}");

        Guid sourceVideoFileId = new(b: fileId.ToByteArray());

        try
        {
            CropResult result = await cropDetector.DetectAsync(
                inputPath: path,
                sourceVideoFileId: sourceVideoFileId,
                sourceIsHdr: null,
                ct: ct
            );
            return Ok(
                value: new
                {
                    source_video_file_id = result.SourceVideoFileId,
                    should_crop = result.ShouldCrop,
                    width = result.Width,
                    height = result.Height,
                    x = result.X,
                    y = result.Y,
                    sample_frames_analyzed = result.SampleFramesAnalyzed,
                    confidence = result.Confidence,
                }
            );
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse(detail: $"Crop detection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs subtitle OCR on a single bitmap subtitle stream (PGS / VobSub /
    /// DVB) inside the requested VideoFile and returns the resulting
    /// WebVTT track. Useful for spot-checking OCR quality before enabling
    /// it on a library-wide re-encode.
    /// </summary>
    [HttpPost(template: "ocr/{videoFileId}")]
    public async Task<IActionResult> OcrBitmapSubtitle(
        string videoFileId,
        [FromQuery] int streamIndex,
        [FromQuery] string language,
        CancellationToken ct
    )
    {
        if (ocrEngine is null)
            return NotImplementedResponse(detail: "Subtitle OCR engine is not registered on this build");

        if (!Ulid.TryParse(base32: videoFileId, ulid: out Ulid fileId))
            return BadRequestResponse(detail: "Invalid video file id");

        if (string.IsNullOrWhiteSpace(value: language))
            return BadRequestResponse(detail: "language query parameter is required");

        VideoFile? file = await videoFileRepository.GetByIdAsync(id: fileId, ct: ct);

        if (file is null)
            return NotFoundResponse(detail: "Video file not found");

        string path = storageDriver.CombinePath(parent: file.HostFolder, child: file.Filename);
        if (!storageDriver.FileExists(path: path))
            return NotFoundResponse(detail: $"Source file missing on disk: {path}");

        try
        {
            SubtitleTrack track = await ocrEngine.OcrAsync(
                inputPath: path,
                streamIndex: streamIndex,
                language: language,
                outputFormat: SubtitleCodecType.WebVtt,
                ct: ct
            );
            return Ok(
                value: new
                {
                    language,
                    stream_index = streamIndex,
                    cue_count = track.CueCount,
                    file_path = track.FilePath,
                }
            );
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse(detail: $"OCR failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs whisper.cpp against the first audio stream of a VideoFile and
    /// writes the resulting WebVTT next to the source. Heavy — whisper is
    /// multi-minute work even on decent hardware — so owner-only and
    /// intended for dashboard spot-checks of transcription quality, not
    /// library-wide jobs (the encode pipeline handles those).
    /// </summary>
    [HttpPost(template: "transcribe/{videoFileId}")]
    public async Task<IActionResult> Transcribe(
        string videoFileId,
        [FromQuery] string language,
        [FromQuery] bool translateToEnglish = false,
        CancellationToken ct = default
    )
    {
        if (whisperTranscriber is null)
            return NotImplementedResponse(detail: "Whisper transcriber is not registered on this build");

        if (!Ulid.TryParse(base32: videoFileId, ulid: out Ulid fileId))
            return BadRequestResponse(detail: "Invalid video file id");

        if (string.IsNullOrWhiteSpace(value: language))
            return BadRequestResponse(detail: "language query parameter is required");

        VideoFile? file = await videoFileRepository.GetByIdAsync(id: fileId, ct: ct);

        if (file is null)
            return NotFoundResponse(detail: "Video file not found");

        string path = storageDriver.CombinePath(parent: file.HostFolder, child: file.Filename);
        if (!storageDriver.FileExists(path: path))
            return NotFoundResponse(detail: $"Source file missing on disk: {path}");

        WhisperOptions options = new(
            ModelPath: string.Empty, // Transcriber reads from EncoderOptions.WhisperModelPath.
            ModelSize: WhisperModelSize.LargeV3,
            TranslateToEnglish: translateToEnglish
        );

        try
        {
            SubtitleTrack track = await whisperTranscriber.TranscribeAsync(
                inputPath: path,
                audioStreamIndex: 0,
                language: language,
                options: options,
                progress: null,
                ct: ct
            );
            return Ok(
                value: new
                {
                    language,
                    translate_to_english = translateToEnglish,
                    file_path = track.FilePath,
                    cue_count = track.CueCount,
                }
            );
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse(detail: $"Transcription failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs the chromaprint intro / outro detector against every episode in
    /// a season that has at least one VideoFile on disk and returns the
    /// detected marker ranges. Useful for validating chromaprint quality on
    /// a specific season before trusting the auto-detection subscriber's
    /// output. Owner-only — fingerprinting every episode is minutes of
    /// ffmpeg work per episode.
    /// </summary>
    [HttpPost(template: "intro/{seasonId:int}")]
    public async Task<IActionResult> DetectIntroForSeason(int seasonId, CancellationToken ct)
    {
        List<Episode> encoded = await videoFileRepository.GetEncodedEpisodesForSeasonAsync(
            seasonId: seasonId,
            ct: ct
        );

        if (encoded.Count < 2)
            return BadRequestResponse(
                detail: $"Need at least 2 encoded episodes, season has {encoded.Count}"
            );

        List<AudioFingerprint> intros = [];
        List<AudioFingerprint> outros = [];

        foreach (Episode ep in encoded)
        {
            ct.ThrowIfCancellationRequested();
            VideoFile? source = ep.VideoFiles.FirstOrDefault();
            if (source is null)
                continue;

            string path = storageDriver.CombinePath(parent: source.HostFolder, child: source.Filename);
            if (!storageDriver.FileExists(path: path))
                continue;

            try
            {
                AudioFingerprint introPrint = await fingerprinter.FingerprintAsync(
                    filePath: path,
                    window: new(Start: TimeSpan.Zero, Duration: TimeSpan.FromMinutes(minutes: 3)),
                    ct: ct
                );
                intros.Add(item: introPrint);

                TimeSpan duration = TimeSpan.TryParse(s: source.Duration, result: out TimeSpan parsed)
                    ? parsed
                    : TimeSpan.Zero;
                TimeSpan outroStart =
                    duration > TimeSpan.FromMinutes(minutes: 3)
                        ? duration - TimeSpan.FromMinutes(minutes: 3)
                        : TimeSpan.Zero;

                AudioFingerprint outroPrint = await fingerprinter.FingerprintAsync(
                    filePath: path,
                    window: new(Start: outroStart, Duration: TimeSpan.FromMinutes(minutes: 3)),
                    ct: ct
                );
                outros.Add(item: outroPrint);
            }
            catch (Exception ex)
            {
                return InternalServerErrorResponse(
                    detail: $"Fingerprinting failed for episode {ep.Id}: {ex.Message}"
                );
            }
        }

        if (intros.Count < 2)
            return BadRequestResponse(detail: "Not enough successful fingerprints to compare");

        IntroMarker? introMarker = introDetector.DetectIntro(episodeFingerprints: intros);
        IntroMarker? outroMarker = introDetector.DetectOutro(episodeFingerprints: outros);

        return Ok(
            value: new
            {
                season_id = seasonId,
                episodes_scanned = intros.Count,
                intro = introMarker is null
                    ? null
                    : new
                    {
                        start_seconds = introMarker.Start.TotalSeconds,
                        end_seconds = introMarker.End.TotalSeconds,
                        confidence = introMarker.Confidence,
                    },
                outro = outroMarker is null
                    ? null
                    : new
                    {
                        start_seconds = outroMarker.Start.TotalSeconds,
                        end_seconds = outroMarker.End.TotalSeconds,
                        confidence = outroMarker.Confidence,
                    },
            }
        );
    }
}
