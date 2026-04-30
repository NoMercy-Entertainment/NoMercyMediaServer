namespace NoMercy.Encoder.Orchestration;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies;
using NoMercy.Storage;

public class EncodingOrchestrator(
    IStrategyResolver resolver,
    IStorage storage,
    ILogger<EncodingOrchestrator> logger,
    INvencSessionCap? nvencCap = null
) : IEncodingOrchestrator
{
    public async Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress = null,
        CancellationToken ct = default
    )
    {
        // Dispatch-time NVENC session cap enforcement (Phase 3.7).
        // Fires before the strategy is resolved so a saturated GPU returns 409
        // immediately rather than failing mid-encode inside ffmpeg.
        // Only applies when the profile requests hardware encoding.
        bool wantsGpu =
            request.Profile.HardwarePreference
            is HardwarePreference.PreferHardware
                or HardwarePreference.ForceHardware;

        if (nvencCap is not null && wantsGpu)
        {
            string gpuName = request.Profile.Name ?? request.Profile.Format.ToString();

            nvencCap.EnforceForGpuEncode(gpuName, requiresGpu: true);
        }

        IEncodingStrategy? strategy = resolver.Resolve(
            request.Profile.Format,
            request.Profile.EncodeMode
        );

        if (strategy is null)
        {
            EncodingError error = new(
                Kind: EncodingErrorKind.Unknown,
                Message: $"No strategy registered for {request.Profile.Format} / "
                    + $"{request.Profile.EncodeMode}. Register an IEncodingStrategy implementation "
                    + "for this combination.",
                FfmpegStderr: null,
                StageName: "Orchestrator",
                Recoverable: false
            );
            progress?.OnError(error);
            EncoderErrorShape errorShape = new(
                Id: EncoderRuleId.EncoderInitFailed,
                Message: error.Message,
                Suggestion: "Register an IEncodingStrategy for this format + mode combination.",
                Details: null
            );
            return new EncodingResult(
                Success: false,
                OutputPath: string.Empty,
                Duration: TimeSpan.Zero,
                Error: error,
                Metrics: new(0, 0, 0, string.Empty, null)
            )
            {
                Status = "failed",
                EnrichedError = errorShape,
            };
        }

        logger.LogInformation(
            "Dispatching to {Strategy} ({Format}/{Mode}) for {Input}",
            strategy.GetType().Name,
            strategy.Format,
            strategy.EncodeMode,
            request.InputPath
        );

        // Resolve per-request storages. Jobs supply per-folder IStorage instances
        // that enforce path guards and backend routing. Fall back to the DI singleton
        // for callers that do not set per-folder storage (live transcoder, tests, etc.).
        // TODO: cross-backend — when source != destination the orchestrator will need
        // to AcquireLocalPathAsync on sourceStorage, run the encode, then stream the
        // output to destinationStorage. Leave copy semantics for the remote-driver follow-up.
        IStorage sourceStorage = request.SourceStorage ?? storage;
        IStorage destinationStorage =
            request.DestinationStorage ?? request.SourceStorage ?? storage;

        Stopwatch wall = Stopwatch.StartNew();

        try
        {
            EncodingResult result = await strategy.EncodeAsync(request, progress, ct);
            wall.Stop();

            if (!result.Success)
            {
                return result with
                {
                    Status = "failed",
                    EnrichedError =
                        result.EnrichedError
                        ?? new EncoderErrorShape(
                            Id: EncoderRuleId.EncoderInitFailed,
                            Message: result.Error?.Message ?? "Encode failed with no details.",
                            Suggestion: null,
                            Details: null
                        ),
                };
            }

            IReadOnlyList<OutputArtifact> artifacts = await BuildArtifactsAsync(
                request.OutputDirectory,
                destinationStorage,
                ct
            );

            // OutputBytes counts everything on disk (segments + sidecars +
            // thumbnails); artifacts are stream-level only. Walk separately.
            long outputBytes = await SumOutputBytesAsync(
                request.OutputDirectory,
                destinationStorage,
                ct
            );
            long sourceBytes = await GetSourceBytesAsync(request.InputPath, sourceStorage, ct);
            double durationSec = wall.Elapsed.TotalSeconds;
            double avgFps = result.Metrics?.AverageFps ?? 0;
            int bitrateKbps = durationSec > 0 ? (int)(outputBytes * 8L / (durationSec * 1000)) : 0;

            EncodeStats stats = new(
                DurationSeconds: durationSec,
                AvgFps: avgFps,
                OutputBitrateKbps: bitrateKbps,
                SourceBytes: sourceBytes,
                OutputBytes: outputBytes
            );

            return result with
            {
                Status = "success",
                Artifacts = artifacts,
                Stats = stats,
            };
        }
        catch (OperationCanceledException)
        {
            wall.Stop();
            logger.LogWarning(
                "Encode cancelled for {Input} after {Elapsed:F1}s",
                request.InputPath,
                wall.Elapsed.TotalSeconds
            );
            return new EncodingResult(
                Success: false,
                OutputPath: string.Empty,
                Duration: wall.Elapsed,
                Error: null,
                Metrics: null
            )
            {
                Status = "cancelled",
            };
        }
        catch (EncoderRuntimeException rex)
        {
            wall.Stop();
            logger.LogError(
                rex,
                "Encoder runtime failure for {Input}: [{Id}] {Message}",
                request.InputPath,
                rex.Shape.Id,
                rex.Shape.Message
            );
            EncodingError error = new(
                Kind: EncodingErrorKind.Unknown,
                Message: rex.Shape.Message,
                FfmpegStderr: null,
                StageName: "Orchestrator",
                Recoverable: false
            );
            progress?.OnError(error);
            return new EncodingResult(
                Success: false,
                OutputPath: string.Empty,
                Duration: wall.Elapsed,
                Error: error,
                Metrics: null
            )
            {
                Status = "failed",
                EnrichedError = rex.Shape,
            };
        }
        catch (Exception ex)
        {
            wall.Stop();
            logger.LogError(ex, "Unexpected error encoding {Input}", request.InputPath);
            EncodingError error = new(
                Kind: EncodingErrorKind.Unknown,
                Message: ex.Message,
                FfmpegStderr: null,
                StageName: "Orchestrator",
                Recoverable: false
            );
            progress?.OnError(error);
            EncoderErrorShape errorShape = new(
                Id: EncoderRuleId.EncoderInitFailed,
                Message: ex.Message,
                Suggestion: null,
                Details: null
            );
            return new EncodingResult(
                Success: false,
                OutputPath: string.Empty,
                Duration: wall.Elapsed,
                Error: error,
                Metrics: null
            )
            {
                Status = "failed",
                EnrichedError = errorShape,
            };
        }
    }

    // Stream-level artifacts only — the master + variant playlists for HLS,
    // the muxed file itself for MP4 / MKV / audio outputs. Per-segment
    // hashing was costing ~57s on a SMB-mounted output dir for one HLS
    // encode (hundreds of .ts / .m4s / .vtt / sprite files); none of it
    // was consumed downstream. Sizes still come from the recursive walk
    // so EncodeStats.OutputBytes stays accurate.
    private async Task<IReadOnlyList<OutputArtifact>> BuildArtifactsAsync(
        string outputDirectory,
        IStorage stor,
        CancellationToken ct
    )
    {
        List<OutputArtifact> artifacts = [];

        IAsyncEnumerable<StorageEntry>? entries;
        try
        {
            entries = stor.ListAsync(outputDirectory, pattern: null, recursive: true, ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not list output directory {Dir} — no artifacts catalogued",
                outputDirectory
            );
            return artifacts;
        }

        if (entries is null)
        {
            return artifacts;
        }

        await foreach (StorageEntry entry in entries)
        {
            if (entry.IsDirectory)
            {
                continue;
            }

            if (!IsStreamLevelArtifact(entry.Path))
            {
                continue;
            }

            try
            {
                string hash = await stor.HashAsync(entry.Path, "sha256", ct);
                string mime = OutputArtifact.MimeFromPath(entry.Path);
                artifacts.Add(new OutputArtifact(entry.Path, entry.SizeBytes, hash, mime));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not catalogue artifact {Path} — skipping", entry.Path);
            }
        }

        return artifacts;
    }

    private static bool IsStreamLevelArtifact(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        string name = Path.GetFileName(path).ToLowerInvariant();

        // HLS playlists — master + per-variant. Segment files (.ts / .m4s)
        // and segment-wrapping subtitle playlists (subs_<lang>_<variant>.m3u8
        // sit next to .vtt segments) are the SAME structural fingerprint as
        // their variant playlist, so skip the segment files but keep all
        // .m3u8 — playlists are KB-sized text, hashing is free.
        if (ext == ".m3u8")
            return true;

        // Single-file muxed outputs.
        if (ext is ".mp4" or ".mkv" or ".webm" or ".flac" or ".mp3" or ".ogg" or ".opus")
            return true;

        // ASS / SRT sidecars when the profile preserves them as direct files.
        if (ext is ".ass" or ".srt")
            return true;

        // Everything else (segments, fmp4 init, thumbnails, sprite vtt,
        // per-segment .vtt) is auxiliary and skipped.
        return false;
    }

    private async Task<long> GetSourceBytesAsync(
        string inputPath,
        IStorage stor,
        CancellationToken ct
    )
    {
        try
        {
            return await stor.SizeAsync(inputPath, ct);
        }
        catch
        {
            return 0;
        }
    }

    // Plain size walk — no hashing, no extra I/O beyond directory metadata.
    private async Task<long> SumOutputBytesAsync(
        string outputDirectory,
        IStorage stor,
        CancellationToken ct
    )
    {
        long total = 0;
        try
        {
            await foreach (
                StorageEntry entry in stor.ListAsync(
                    outputDirectory,
                    pattern: null,
                    recursive: true,
                    ct: ct
                )
            )
            {
                if (!entry.IsDirectory)
                    total += entry.SizeBytes;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not sum output sizes for {Dir} — OutputBytes will be 0",
                outputDirectory
            );
        }
        return total;
    }
}
