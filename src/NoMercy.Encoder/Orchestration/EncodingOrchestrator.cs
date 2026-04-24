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
                ct
            );

            long outputBytes = artifacts.Sum(a => a.SizeBytes);
            long sourceBytes = await GetSourceBytesAsync(request.InputPath, ct);
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

    private async Task<IReadOnlyList<OutputArtifact>> BuildArtifactsAsync(
        string outputDirectory,
        CancellationToken ct
    )
    {
        List<OutputArtifact> artifacts = [];

        IAsyncEnumerable<StorageEntry>? entries;
        try
        {
            entries = storage.ListAsync(outputDirectory, pattern: null, recursive: true, ct: ct);
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

            try
            {
                string hash = await storage.HashAsync(entry.Path, "sha256", ct);
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

    private async Task<long> GetSourceBytesAsync(string inputPath, CancellationToken ct)
    {
        try
        {
            return await storage.SizeAsync(inputPath, ct);
        }
        catch
        {
            return 0;
        }
    }
}
