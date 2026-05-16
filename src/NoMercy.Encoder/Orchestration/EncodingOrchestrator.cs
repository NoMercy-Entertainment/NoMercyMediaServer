using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Storage.Drivers.S3;
using NoMercy.Storage.Drivers.WebDav;
using EncodeMode = NoMercy.Encoder.Codecs.EncodeMode;

namespace NoMercy.Encoder.Orchestration;

public class EncodingOrchestrator(
    IStrategyResolver resolver,
    IStorage storage,
    IEncoder encoder,
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
            string gpuName = request.Profile.Name ?? request.Profile.Container.ToString();

            nvencCap.EnforceForGpuEncode(gpuName, requiresGpu: true);
        }

        OutputFormat profileFormat = PlanStageHelpers.ContainerToOutputFormat(
            request.Profile.Container
        );

        IEncodingStrategy? strategy = resolver.Resolve(
            profileFormat,
            (NoMercy.Encoder.Codecs.EncodeMode)(int)request.Profile.EncodeMode
        );

        if (strategy is null)
        {
            EncodingError error = new(
                Kind: EncodingErrorKind.Unknown,
                Message: $"No strategy registered for {profileFormat} / "
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
            return new(
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
        // Cross-backend: source staged via AcquireLocalPathAsync; remote destination
        // gets a local temp working dir, then artifacts upload via destinationStorage.
        IStorage sourceStorage = request.SourceStorage ?? storage;
        IStorage destinationStorage =
            request.DestinationStorage ?? request.SourceStorage ?? storage;

        Stopwatch wall = Stopwatch.StartNew();

        try
        {
            await using LocalPathLease lease = await sourceStorage.AcquireLocalPathAsync(
                request.InputPath,
                ct
            );

            EncodingResult result;

            Directory.CreateDirectory(StoragePaths.TranscodeRoot);
            // Mirror the final OutputDirectory structure under the transcode root
            // so working files land at e.g.
            //   cache/encoder/<Show>.(<Year>)/<Show>.SxxExx/
            // instead of an opaque nomercy-enc-<ulid> dir. Easy to inspect, easy
            // to wipe per show. OutputDirectory is a relative path from the
            // destination storage root; treat / and \ as portable separators.
            string relativeOutputPath = (request.OutputDirectory ?? string.Empty)
                .Replace('\\', '/')
                .Trim('/');
            string tempDir = string.IsNullOrEmpty(relativeOutputPath)
                ? Path.Combine(StoragePaths.TranscodeRoot, $"nomercy-enc-{Ulid.NewUlid()}")
                : Path.Combine(
                    StoragePaths.TranscodeRoot,
                    relativeOutputPath.Replace('/', Path.DirectorySeparatorChar)
                );
            // CreateDirectory is idempotent — returns the existing DirectoryInfo
            // when the path already exists. FFmpeg's -y overwrites any stale
            // segments from a prior run, so wiping isn't necessary and a
            // recursive Delete races against Process.Start's working-directory
            // resolution on Windows (intermittently leaves the dir gone when
            // ffmpeg.exe boots a moment later).
            try
            {
                Directory.CreateDirectory(tempDir);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create encoder temp dir {TempDir}", tempDir);
                throw;
            }

            if (!Directory.Exists(tempDir))
            {
                logger.LogWarning(
                    "Encoder temp dir {TempDir} not present after CreateDirectory — recreating",
                    tempDir
                );
                Directory.CreateDirectory(tempDir);
            }

            try
            {
                // Strategy always writes to a local temp dir so ffmpeg has a
                // real filesystem path regardless of backend type. SourceStorage
                // also swaps to the DI LocalStorage so stages can probe the
                // staged input via FileExists/OpenRead. After encoding the
                // publish loop moves or copies artifacts to the real destination.
                EncodingRequest stagedRequest = request with
                {
                    InputPath = lease.Path,
                    OutputDirectory = tempDir,
                    SourceStorage = storage,
                    DestinationStorage = storage,
                };

                result = await strategy.EncodeAsync(stagedRequest, progress, ct);
                wall.Stop();

                // Per-task encodes (TaskFilter set on EncodingOptions) write
                // to a SHARED tempDir derived from the relative OutputDirectory.
                // All decomposed tasks for a single coordinator end up pointing
                // at the same cache path. If each task ran the publish loop it
                // would race every other task in the group: enumerate ALL files
                // (including in-progress writes from other tasks), File.Move
                // them, then Directory.Delete the tempDir — wiping outputs the
                // peers haven't finished writing yet. The coordinator
                // (VideoEncodeJob.HandleFinalizeAsync) publishes once after
                // every task in the group completes, so per-task runs skip
                // both the publish loop and the cleanup finally.
                bool isPerTaskRun = request.Options?.TaskFilter is not null;

                if (result.Success && !isPerTaskRun)
                {
                    await PublishTempDirAsync(
                        tempDir,
                        request.OutputDirectory,
                        destinationStorage,
                        progress,
                        ct
                    );
                }
            }
            finally
            {
                // Per-task runs leave files for the coordinator to publish +
                // sweep — see isPerTaskRun comment above the publish loop.
                bool isPerTaskRun = request.Options?.TaskFilter is not null;
                if (!isPerTaskRun)
                {
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                    catch (Exception cleanEx)
                    {
                        logger.LogWarning(
                            cleanEx,
                            "Could not clean up temp encode dir {TempDir}",
                            tempDir
                        );
                    }
                }
            }

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
            return new(
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
            return new(
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
            return new(
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

    /// <summary>
    /// Run a single decomposed task. Injects a <see cref="DecomposedTask"/>
    /// into the request's <see cref="EncodingOptions.TaskFilter"/> so that
    /// <see cref="BuildStage"/> emits only the commands relevant to this task.
    /// All other pipeline stages run normally — the task still gets analyzed,
    /// validated, and planned, but only its slice is built and executed.
    /// </summary>
    public Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        DecomposedTask task,
        IProgressObserver? progress = null,
        CancellationToken ct = default
    )
    {
        EncodingOptions filteredOptions = (request.Options ?? new EncodingOptions()) with
        {
            TaskFilter = task,
        };
        return EncodeAsync(request with { Options = filteredOptions }, progress, ct);
    }

    public async Task<DecomposedTask[]> DecomposeAsync(
        EncodingRequest request,
        string groupTag,
        CancellationToken ct = default
    )
    {
        OutputFormat profileFormat = PlanStageHelpers.ContainerToOutputFormat(
            request.Profile.Container
        );

        IEncodingStrategy? strategy = resolver.Resolve(
            profileFormat,
            (EncodeMode)(int)request.Profile.EncodeMode
        );

        if (strategy is null)
            return [IEncodingStrategy.WholeTask(groupTag)];

        // Stage the input file locally so PlanAsync can probe it via ffprobe
        // regardless of the source storage backend.
        await using LocalPathLease lease = await (
            request.SourceStorage ?? storage
        ).AcquireLocalPathAsync(request.InputPath, ct);

        EncodingRequest stagedRequest = request with
        {
            InputPath = lease.Path,
            SourceStorage = storage,
            DestinationStorage = storage,
        };

        OutputPlan? plan = await encoder.PlanAsync(stagedRequest, ct);

        if (plan is null)
            return [IEncodingStrategy.WholeTask(groupTag)];

        return strategy.Decompose(plan, groupTag);
    }

    /// <summary>
    /// Moves every file under <paramref name="tempDir"/> to its mirrored path
    /// under <paramref name="outputDirectory"/> on <paramref name="destinationStorage"/>.
    /// Uses an atomic <c>File.Move</c> on same-volume local destinations, falls
    /// back to a copy stream for remote backends or cross-volume cases. Parent
    /// directories are created on demand because NFS <c>creat</c> rejects writes
    /// when the enclosing directory is missing.
    /// </summary>
    private async Task PublishTempDirAsync(
        string tempDir,
        string outputDirectory,
        IStorage destinationStorage,
        IProgressObserver? progress,
        CancellationToken ct
    )
    {
        bool isLocalDest = destinationStorage is LocalStorage;
        bool sameVolume = false;

        if (isLocalDest)
        {
            string? firstFile = Directory
                .EnumerateFiles(tempDir, "*", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (firstFile is not null)
            {
                string probeRel = Path.GetRelativePath(tempDir, firstFile).Replace('\\', '/');
                string probeDest = string.Join(
                    '/',
                    outputDirectory.TrimEnd('/'),
                    probeRel.TrimStart('/')
                );
                try
                {
                    string destAbs = destinationStorage.GetFullPath(probeDest);
                    sameVolume = string.Equals(
                        Path.GetPathRoot(firstFile),
                        Path.GetPathRoot(destAbs),
                        StringComparison.OrdinalIgnoreCase
                    );
                }
                catch (NotSupportedException) { }
            }
        }

        string stageName = ResolvePublishStageName(destinationStorage, sameVolume);
        progress?.OnStageStarted(stageName);
        Stopwatch stageWatch = Stopwatch.StartNew();
        try
        {
            foreach (
                string localFile in Directory.EnumerateFiles(
                    tempDir,
                    "*",
                    SearchOption.AllDirectories
                )
            )
            {
                string rel = Path.GetRelativePath(tempDir, localFile).Replace('\\', '/');
                string remoteDest = string.Join(
                    '/',
                    outputDirectory.TrimEnd('/'),
                    rel.TrimStart('/')
                );

                if (sameVolume)
                {
                    bool moved = false;
                    try
                    {
                        string destAbs = destinationStorage.GetFullPath(remoteDest);
                        Directory.CreateDirectory(Path.GetDirectoryName(destAbs)!);
                        File.Move(localFile, destAbs, overwrite: true);
                        moved = true;
                    }
                    catch (IOException)
                    {
                        // Cross-volume race or permission edge-case —
                        // fall through to the copy path below.
                    }

                    if (moved)
                        continue;
                }

                string? remoteParent = destinationStorage.GetParent(remoteDest);
                if (!string.IsNullOrEmpty(remoteParent))
                    destinationStorage.CreateDirectory(remoteParent);

                await using FileStream src = File.OpenRead(localFile);
                await using Stream dst = await destinationStorage.OpenWriteAsync(
                    remoteDest,
                    overwrite: true,
                    ct
                );
                await src.CopyToAsync(dst, ct);
            }
            stageWatch.Stop();
            progress?.OnStageCompleted(stageName, stageWatch.Elapsed);
        }
        catch
        {
            stageWatch.Stop();
            throw;
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
                artifacts.Add(new(entry.Path, entry.SizeBytes, hash, mime));
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

    private static string ResolvePublishStageName(IStorage dest, bool sameVolume)
    {
        if (dest is LocalStorage)
        {
            return sameVolume
                ? "Publishing artifacts (same-volume rename)"
                : "Publishing artifacts to local";
        }

        string backendLabel = dest.Driver switch
        {
            NfsStorageDriver => "NFS",
            S3StorageDriver => "S3",
            WebDavStorageDriver => "WebDAV",
            _ => dest.Driver.GetType().Name,
        };

        return $"Publishing artifacts to {backendLabel}";
    }
}
