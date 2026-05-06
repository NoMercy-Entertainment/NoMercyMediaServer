using Microsoft.Extensions.Logging;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Bundle;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Progress;
using NoMercy.Storage;

namespace NoMercy.Encoder.Pipeline.Stages;

public record FinalizeInput(
    ExecutionResult[] Results,
    OutputPlan Plan,
    string OutputDirectory,
    string MediaTitle,
    IProgressObserver? Progress = null
);

public record FinalizeOutput(string OutputPath, long OutputSizeBytes);

public class FinalizeStage(
    IChapterWriter chapterWriter,
    IFontExtractor fontExtractor,
    IOutputStrategyFactory outputStrategyFactory,
    ILogger<FinalizeStage> logger,
    IStorage storage,
    IBundleManifestWriter? manifestWriter = null
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

            effectiveStorage.CreateDirectory(input.OutputDirectory);

            IOutputStrategy strategy = outputStrategyFactory.Resolve(input.Plan.Format);

            input.Progress?.OnStageStarted("Building Master Playlist");
            await strategy.FinalizeAsync(input.OutputDirectory, input.Plan, input.MediaTitle, ct);
            input.Progress?.OnStageCompleted("Building Master Playlist", TimeSpan.Zero);

            // Write chapters.vtt from MediaInfo
            if (context.MediaInfo is not null && context.MediaInfo.Chapters.Count > 0)
            {
                input.Progress?.OnStageStarted("Extracting chapters");
                await chapterWriter.WriteChaptersAsync(
                    input.OutputDirectory,
                    context.MediaInfo.Chapters,
                    ct
                );
                input.Progress?.OnStageCompleted("Extracting chapters", TimeSpan.Zero);

                logger.LogDebug(
                    "[{CorrelationId}] Wrote {Count} chapters to chapters.vtt",
                    context.CorrelationId,
                    context.MediaInfo.Chapters.Count
                );
            }

            // Write fonts.json manifest from previously extracted fonts
            input.Progress?.OnStageStarted("Extracting fonts");
            await fontExtractor.WriteFontManifestAsync(input.OutputDirectory, ct);
            input.Progress?.OnStageCompleted("Extracting fonts", TimeSpan.Zero);

            // Thumbnail sprite + VTT are produced by the spritevtt muxer
            // in the main FFmpeg command — no post-processing needed.

            IReadOnlyList<StorageEntry> allEntries = effectiveStorage.List(
                input.OutputDirectory,
                "*",
                recursive: true
            );

            long totalSize = allEntries.Where(e => !e.IsDirectory).Sum(e => e.SizeBytes);

            // Emit manifest.json when the encode has a resolved BundleLayout
            // and a writer is wired (DI singleton). Skipped when layout is null
            // (legacy callers that don't set MediaItem on the context).
            if (manifestWriter is not null && input.Plan.Layout is BundleLayout layout)
            {
                await WriteManifestAsync(effectiveStorage, layout, allEntries, context, ct);
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
