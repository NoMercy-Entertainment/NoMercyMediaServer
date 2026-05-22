using Microsoft.Extensions.Logging;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Bundle;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Progress;
using NoMercy.Storage;

namespace NoMercy.Encoder.Pipeline.Stages;

public record FinalizeInput(
    ExecutionResult[] Results,
    OutputPlan Plan,
    string OutputDirectory,
    string MediaTitle,
    IProgressObserver? Progress = null,
    // Null means "use spec defaults" — not "skip everything".
    HlsDerivatives? HlsDerivatives = null
);

public record FinalizeOutput(string OutputPath, long OutputSizeBytes);

public class FinalizeStage(
    IChapterWriter chapterWriter,
    IFontExtractor fontExtractor,
    IOutputStrategyFactory outputStrategyFactory,
    ILogger<FinalizeStage> logger,
    IStorage storage,
    IBundleManifestWriter? manifestWriter = null,
    IReconstructionWriter? reconstructionWriter = null
) : IPipelineStage<FinalizeInput, FinalizeOutput>, IFinalizeStage
{
    public string Name => "Finalize";

    public async Task<StageResult> ExecuteAsync(
        FinalizeInput input,
        EncodingContext context,
        CancellationToken ct
    )
    {
        logger.LogInformation("[{CorrelationId}] Finalizing output", context.CorrelationId);

        try
        {
            // Use the per-folder destination storage from the job context when
            // available; fall back to the DI-injected singleton for default installs.
            IStorage effectiveStorage = context.DestinationStorage ?? storage;

            // Null → spec defaults. Never skip everything.
            HlsDerivatives derivatives = input.HlsDerivatives ?? new HlsDerivatives();

            effectiveStorage.CreateDirectory(input.OutputDirectory);

            IOutputStrategy strategy = outputStrategyFactory.Resolve(input.Plan.Format);

            // GenerateMasterPlaylist — master playlist written by the output strategy.
            // The flag currently gates the whole FinalizeAsync call because the output
            // strategies pair playlist writing with segment finalization; teasing the
            // two apart is a strategy-level change, not a finalize-stage concern.
            if (derivatives.GenerateMasterPlaylist)
            {
                input.Progress?.OnStageStarted("Building Master Playlist");
                await strategy.FinalizeAsync(
                    input.OutputDirectory,
                    input.Plan,
                    input.MediaTitle,
                    ct
                );
                input.Progress?.OnStageCompleted("Building Master Playlist", TimeSpan.Zero);
            }

            // GenerateChapters — write chapters.vtt from MediaInfo.
            if (
                derivatives.GenerateChapters
                && context.MediaInfo is not null
                && context.MediaInfo.Chapters.Count > 0
            )
            {
                input.Progress?.OnStageStarted("Extracting chapters");
                await chapterWriter.WriteChaptersAsync(
                    input.OutputDirectory,
                    context.MediaInfo.Chapters,
                    ct,
                    includeThumbUris: derivatives.GenerateChapterThumbs
                );
                input.Progress?.OnStageCompleted("Extracting chapters", TimeSpan.Zero);

                logger.LogDebug(
                    "[{CorrelationId}] Wrote {Count} chapters to chapters.vtt",
                    context.CorrelationId,
                    context.MediaInfo.Chapters.Count
                );
            }

            // GenerateFontsJson — write fonts.json manifest from previously extracted fonts.
            if (derivatives.GenerateFontsJson)
            {
                input.Progress?.OnStageStarted("Extracting fonts");
                await fontExtractor.WriteFontManifestAsync(input.OutputDirectory, ct);
                input.Progress?.OnStageCompleted("Extracting fonts", TimeSpan.Zero);
            }
            else
            {
                logger.LogDebug(
                    "[{CorrelationId}] Skipping fonts.json (GenerateFontsJson=false)",
                    context.CorrelationId
                );
            }

            // Sprite VTT, thumbnail track, original-filename tag, and metadata.json
            // sidecar are all produced upstream in BuildStage. No finalize-stage work.
            //
            // The opt-in flags below have no implementation yet. Fail loudly instead
            // of silently ignoring a profile that set them, so callers get a clear
            // error instead of a successful encode missing the requested artifact.
            if (derivatives.GenerateIFramePlaylists)
                throw new NotSupportedException(
                    "HlsDerivatives.GenerateIFramePlaylists is set but no IFramePlaylistGenerator "
                        + "is wired. Leave it false until I-frame playlist support lands."
                );

            if (derivatives.ExtractClosedCaptions)
                throw new NotSupportedException(
                    "HlsDerivatives.ExtractClosedCaptions is set but no CcExtractor is wired. "
                        + "Leave it false until CEA-608/708 extraction lands."
                );

            IReadOnlyList<StorageEntry> allEntries = effectiveStorage.List(
                input.OutputDirectory,
                "*",
                recursive: true
            );

            long totalSize = allEntries.Where(e => !e.IsDirectory).Sum(e => e.SizeBytes);

            // Emit manifest.json + reconstruction.json when the encode has a resolved
            // BundleLayout and writers are wired (DI singletons). Skipped when layout
            // is null (legacy callers that don't set MediaItem on the context).
            if (input.Plan.Layout is BundleLayout layout)
            {
                if (manifestWriter is not null)
                    await WriteManifestAsync(effectiveStorage, layout, allEntries, context, ct);

                if (reconstructionWriter is not null && context.MediaInfo is not null)
                    await WriteReconstructionAsync(
                        effectiveStorage,
                        layout,
                        input.Plan,
                        context,
                        ct
                    );
            }

            return new StageSuccess<FinalizeOutput>(new(input.OutputDirectory, totalSize));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new StageFailure(
                new(
                    EncodingErrorKind.Unknown,
                    $"Finalization failed: {ex.Message}",
                    null,
                    Name,
                    false
                )
            );
        }
    }

    private async Task WriteReconstructionAsync(
        IStorage effectiveStorage,
        BundleLayout layout,
        OutputPlan plan,
        EncodingContext context,
        CancellationToken ct
    )
    {
        await reconstructionWriter!.WriteAsync(
            effectiveStorage,
            layout.ReconstructionPath,
            context.MediaInfo!,
            plan,
            layout,
            ct
        );

        logger.LogInformation(
            "[{CorrelationId}] Wrote reconstruction.json → {Path}",
            context.CorrelationId,
            layout.ReconstructionPath
        );
    }

    private async Task WriteManifestAsync(
        IStorage effectiveStorage,
        BundleLayout layout,
        IReadOnlyList<StorageEntry> allEntries,
        EncodingContext context,
        CancellationToken ct
    )
    {
        string dirPrefix = layout.BundleDirectory.TrimEnd('/') + "/";

        List<string> relFiles = [];
        foreach (StorageEntry entry in allEntries)
        {
            if (entry.IsDirectory)
                continue;
            string rel = entry.Path.StartsWith(dirPrefix, StringComparison.OrdinalIgnoreCase)
                ? entry.Path[dirPrefix.Length..]
                : entry.Path;
            // Exclude the manifest itself from its own file list.
            if (rel.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                continue;
            relFiles.Add(rel);
        }

        string mediaTypeStr = context.MediaItem?.Type switch
        {
            MediaType.Movie => "movie",
            MediaType.Episode => "episode",
            MediaType.Track => "track",
            _ => "unknown",
        };

        string encoderVersion = typeof(FinalizeStage).Assembly.GetName().Version?.ToString() ?? "3";

        BundleManifest manifest = new(
            Version: 1,
            EncoderVersion: encoderVersion,
            PresetId: layout.PresetId,
            PresetName: layout.PresetName,
            PresetSlug: layout.PresetSlug,
            MediaType: mediaTypeStr,
            MediaId: context.MediaItem?.Id ?? 0,
            MediaExternalId: null,
            MediaFolder: layout.BundleDirectory,
            Container: layout.ContainerString,
            CreatedAt: DateTime.UtcNow,
            CompletedAt: DateTime.UtcNow,
            MediaKey: layout.MediaKey,
            Files: relFiles
        );

        await manifestWriter!.WriteAsync(layout.ManifestPath, manifest, ct);

        logger.LogInformation(
            "[{CorrelationId}] Wrote manifest.json → {Path} ({FileCount} files)",
            context.CorrelationId,
            layout.ManifestPath,
            relFiles.Count
        );
    }
}
