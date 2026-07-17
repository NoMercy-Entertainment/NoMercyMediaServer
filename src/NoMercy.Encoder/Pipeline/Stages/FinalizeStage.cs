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
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Bundle;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Reconciliation;
using NoMercy.Storage;

namespace NoMercy.Encoder.Pipeline.Stages;

public record FinalizeInput(
    ExecutionResult[] Results,
    OutputPlan Plan,
    string OutputDirectory,
    string MediaTitle,
    IProgressObserver? Progress = null,
    // Null means "use spec defaults" — not "skip everything".
    HlsDerivatives? HlsDerivatives = null,
    // The RESOLVED profile actually applied to this encode. Used to stamp
    // manifest.json with a profile fingerprint so a later reconciliation
    // pass can tell "preset edited in place, same id" apart from "genuinely
    // unchanged". Null for callers that predate reconciliation (Preview) —
    // the manifest is then written without a fingerprint, same as any
    // pre-reconciliation output.
    EncodingProfile? Profile = null
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
                int fontsWritten = await fontExtractor.WriteFontManifestAsync(
                    input.OutputDirectory,
                    ct
                );
                input.Progress?.OnStageCompleted("Extracting fonts", TimeSpan.Zero);

                // Completeness gate: a source with embedded fonts whose extraction
                // came up short would render subtitles with missing glyphs. Fail
                // the encode instead of publishing broken output — the orchestrator
                // only publishes to the destination when the result is successful.
                int expectedFonts = context.MediaInfo is null
                    ? 0
                    : fontExtractor.CountFontAttachments(context.MediaInfo.Attachments);

                if (expectedFonts > 0 && fontsWritten < expectedFonts)
                    return new StageFailure(
                        new(
                            EncodingErrorKind.Unknown,
                            $"Font extraction incomplete: source has {expectedFonts} embedded font(s) "
                                + $"but only {fontsWritten} were extracted. Subtitle rendering would be "
                                + "missing fonts, so the output is not published.",
                            null,
                            Name,
                            true
                        )
                    );
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

            // SubtitleImsc is reserved for future work — log a warning instead
            // of throwing so existing profiles created before WebVtt/Imsc were
            // wired (when the toggle was a no-op) don't suddenly hard-fail.
            // The dashboard label flags it as "not yet implemented" so users
            // see the same intent on the editor side.
            if (derivatives.SubtitleImsc)
                logger.LogWarning(
                    "[{CorrelationId}] HlsDerivatives.SubtitleImsc is true but IMSC subtitle output is not implemented — flag ignored. Untick it in the encoder profile to silence this warning.",
                    context.CorrelationId
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
                    await WriteManifestAsync(
                        effectiveStorage,
                        input.OutputDirectory,
                        layout,
                        allEntries,
                        context,
                        input.Profile,
                        ct
                    );

                if (reconstructionWriter is not null && context.MediaInfo is not null)
                    await WriteReconstructionAsync(
                        effectiveStorage,
                        input.OutputDirectory,
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
        string outputDirectory,
        BundleLayout layout,
        OutputPlan plan,
        EncodingContext context,
        CancellationToken ct
    )
    {
        // BundleLayout paths are documented as relative to the media item's own
        // folder (see BundleLayout.cs) — that folder IS input.OutputDirectory, the
        // same directory BuildStage already writes video_*/audio_*/chapters.vtt
        // into. Joining here (rather than writing layout.ReconstructionPath as-is
        // against the folder-scoped storage root) keeps every media item's
        // reconstruction.json scoped under its own folder instead of colliding
        // with every other item in the library that shares the same preset slug.
        string reconstructionPath = effectiveStorage.CombinePath(
            outputDirectory,
            layout.ReconstructionPath
        );

        await reconstructionWriter!.WriteAsync(
            effectiveStorage,
            reconstructionPath,
            context.MediaInfo!,
            plan,
            layout,
            ct,
            context.OriginalInputPath
        );

        logger.LogInformation(
            "[{CorrelationId}] Wrote reconstruction.json → {Path}",
            context.CorrelationId,
            reconstructionPath
        );
    }

    private async Task WriteManifestAsync(
        IStorage effectiveStorage,
        string outputDirectory,
        BundleLayout layout,
        IReadOnlyList<StorageEntry> allEntries,
        EncodingContext context,
        EncodingProfile? profile,
        CancellationToken ct
    )
    {
        // allEntries comes from listing input.OutputDirectory, so entry.Path is
        // already relative to the storage root (same domain as outputDirectory
        // itself) — the prefix to strip is the media item's folder joined with
        // the bundle sub-directory, not the bundle sub-directory alone.
        string bundleDirectory = effectiveStorage.CombinePath(
            outputDirectory,
            layout.BundleDirectory
        );
        // Relative to the media folder, which is where the encoder actually writes
        // video_*/, audio_*/ and the rest. Measuring against the bundle directory
        // instead — a subdirectory none of those files are in — meant no entry
        // ever matched the prefix, so every one was recorded as an absolute path
        // into a staging directory that no longer exists once the encode publishes.
        string dirPrefix = outputDirectory.TrimEnd('/') + "/";

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
            // The folder this bundle ends up in, not the staging directory it was
            // assembled in — that one is deleted the moment the encode publishes.
            MediaFolder: context.OriginalOutputDirectory ?? outputDirectory,
            Container: layout.ContainerString,
            CreatedAt: DateTime.UtcNow,
            CompletedAt: DateTime.UtcNow,
            MediaKey: layout.MediaKey,
            Files: relFiles,
            ProfileFingerprint: profile is not null ? ProfileFingerprint.Compute(profile) : null
        );

        string manifestPath = effectiveStorage.CombinePath(outputDirectory, layout.ManifestPath);
        await manifestWriter!.WriteAsync(effectiveStorage, manifestPath, manifest, ct);

        logger.LogInformation(
            "[{CorrelationId}] Wrote manifest.json → {Path} ({FileCount} files)",
            context.CorrelationId,
            manifestPath,
            relFiles.Count
        );
    }
}
