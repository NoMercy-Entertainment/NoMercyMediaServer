using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.ContentAnalysis;
using NoMercy.Encoder.ContentAnalysis.Fingerprinting;
using NoMercy.Encoder.Subtitles;
using NoMercy.Helpers.Extensions;
using NoMercy.Storage;

namespace NoMercy.Api.Controllers.V1.Dashboard;

/// <summary>
/// On-demand content-analysis probes. Useful when dialing in profiles
/// (does this source actually have letterbox bars?) or debugging the
/// auto-detection pipeline without kicking off a full encode.
/// </summary>
[ApiController]
[Tags("Dashboard Content Analysis")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/content-analysis")]
public class ContentAnalysisController(
    ICropDetector cropDetector,
    ISubtitleOcrEngine? ocrEngine,
    IWhisperTranscriber? whisperTranscriber,
    IAudioFingerprinter fingerprinter,
    IIntroDetector introDetector,
    IDbContextFactory<MediaContext> contextFactory,
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
    [HttpGet("crop/{videoFileId}")]
    public async Task<IActionResult> DetectCrop(string videoFileId, CancellationToken ct)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("Only the server owner can probe crop detection");

        if (!Ulid.TryParse(videoFileId, out Ulid fileId))
            return BadRequestResponse("Invalid video file id");

        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        VideoFile? file = await context.VideoFiles.FirstOrDefaultAsync(v => v.Id == fileId, ct);

        if (file is null)
            return NotFoundResponse("Video file not found");

        string path = storageDriver.CombinePath(file.HostFolder, file.Filename);
        if (!storageDriver.FileExists(path))
            return NotFoundResponse($"Source file missing on disk: {path}");

        Guid sourceVideoFileId = new(fileId.ToByteArray());

        try
        {
            CropResult result = await cropDetector.DetectAsync(path, sourceVideoFileId, ct);
            return Ok(
                new
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
            return InternalServerErrorResponse($"Crop detection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs subtitle OCR on a single bitmap subtitle stream (PGS / VobSub /
    /// DVB) inside the requested VideoFile and returns the resulting
    /// WebVTT track. Useful for spot-checking OCR quality before enabling
    /// it on a library-wide re-encode.
    /// </summary>
    [HttpPost("ocr/{videoFileId}")]
    public async Task<IActionResult> OcrBitmapSubtitle(
        string videoFileId,
        [FromQuery] int streamIndex,
        [FromQuery] string language,
        CancellationToken ct
    )
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("Only the server owner can probe OCR");

        if (ocrEngine is null)
            return NotImplementedResponse("Subtitle OCR engine is not registered on this build");

        if (!Ulid.TryParse(videoFileId, out Ulid fileId))
            return BadRequestResponse("Invalid video file id");

        if (string.IsNullOrWhiteSpace(language))
            return BadRequestResponse("language query parameter is required");

        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        VideoFile? file = await context.VideoFiles.FirstOrDefaultAsync(v => v.Id == fileId, ct);

        if (file is null)
            return NotFoundResponse("Video file not found");

        string path = storageDriver.CombinePath(file.HostFolder, file.Filename);
        if (!storageDriver.FileExists(path))
            return NotFoundResponse($"Source file missing on disk: {path}");

        try
        {
            SubtitleTrack track = await ocrEngine.OcrAsync(
                path,
                streamIndex,
                language,
                SubtitleCodecType.WebVtt,
                ct
            );
            return Ok(
                new
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
            return InternalServerErrorResponse($"OCR failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs whisper.cpp against the first audio stream of a VideoFile and
    /// writes the resulting WebVTT next to the source. Heavy — whisper is
    /// multi-minute work even on decent hardware — so owner-only and
    /// intended for dashboard spot-checks of transcription quality, not
    /// library-wide jobs (the encode pipeline handles those).
    /// </summary>
    [HttpPost("transcribe/{videoFileId}")]
    public async Task<IActionResult> Transcribe(
        string videoFileId,
        [FromQuery] string language,
        [FromQuery] bool translateToEnglish = false,
        CancellationToken ct = default
    )
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("Only the server owner can probe transcription");

        if (whisperTranscriber is null)
            return NotImplementedResponse("Whisper transcriber is not registered on this build");

        if (!Ulid.TryParse(videoFileId, out Ulid fileId))
            return BadRequestResponse("Invalid video file id");

        if (string.IsNullOrWhiteSpace(language))
            return BadRequestResponse("language query parameter is required");

        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        VideoFile? file = await context.VideoFiles.FirstOrDefaultAsync(v => v.Id == fileId, ct);

        if (file is null)
            return NotFoundResponse("Video file not found");

        string path = storageDriver.CombinePath(file.HostFolder, file.Filename);
        if (!storageDriver.FileExists(path))
            return NotFoundResponse($"Source file missing on disk: {path}");

        WhisperOptions options = new(
            ModelPath: string.Empty, // Transcriber reads from EncoderOptions.WhisperModelPath.
            ModelSize: WhisperModelSize.LargeV3,
            TranslateToEnglish: translateToEnglish
        );

        try
        {
            SubtitleTrack track = await whisperTranscriber.TranscribeAsync(
                path,
                audioStreamIndex: 0,
                language: language,
                options: options,
                progress: null,
                ct: ct
            );
            return Ok(
                new
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
            return InternalServerErrorResponse($"Transcription failed: {ex.Message}");
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
    [HttpPost("intro/{seasonId:int}")]
    public async Task<IActionResult> DetectIntroForSeason(int seasonId, CancellationToken ct)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("Only the server owner can probe intro detection");

        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        List<Episode> encoded = await context
            .Episodes.AsNoTracking()
            .Include(e => e.VideoFiles)
            .Where(e => e.SeasonId == seasonId && e.VideoFiles.Count > 0)
            .OrderBy(e => e.EpisodeNumber)
            .ToListAsync(ct);

        if (encoded.Count < 2)
            return BadRequestResponse(
                $"Need at least 2 encoded episodes, season has {encoded.Count}"
            );

        List<AudioFingerprint> intros = [];
        List<AudioFingerprint> outros = [];

        foreach (Episode ep in encoded)
        {
            ct.ThrowIfCancellationRequested();
            VideoFile? source = ep.VideoFiles.FirstOrDefault();
            if (source is null)
                continue;

            string path = storageDriver.CombinePath(source.HostFolder, source.Filename);
            if (!storageDriver.FileExists(path))
                continue;

            try
            {
                AudioFingerprint introPrint = await fingerprinter.FingerprintAsync(
                    path,
                    new(TimeSpan.Zero, TimeSpan.FromMinutes(3)),
                    ct
                );
                intros.Add(introPrint);

                TimeSpan duration = TimeSpan.TryParse(source.Duration, out TimeSpan parsed)
                    ? parsed
                    : TimeSpan.Zero;
                TimeSpan outroStart =
                    duration > TimeSpan.FromMinutes(3)
                        ? duration - TimeSpan.FromMinutes(3)
                        : TimeSpan.Zero;

                AudioFingerprint outroPrint = await fingerprinter.FingerprintAsync(
                    path,
                    new(outroStart, TimeSpan.FromMinutes(3)),
                    ct
                );
                outros.Add(outroPrint);
            }
            catch (Exception ex)
            {
                return InternalServerErrorResponse(
                    $"Fingerprinting failed for episode {ep.Id}: {ex.Message}"
                );
            }
        }

        if (intros.Count < 2)
            return BadRequestResponse("Not enough successful fingerprints to compare");

        IntroMarker? introMarker = introDetector.DetectIntro(intros);
        IntroMarker? outroMarker = introDetector.DetectOutro(outros);

        return Ok(
            new
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
