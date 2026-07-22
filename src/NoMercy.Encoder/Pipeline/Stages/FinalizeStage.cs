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
    IMediaBlueprintWriter? blueprintWriter = null
) : IPipelineStage<FinalizeInput, FinalizeOutput>, IFinalizeStage
{
    public string Name => "Finalize";

    public async Task<StageResult> ExecuteAsync(
        FinalizeInput input,
        EncodingContext context,
        CancellationToken ct
    )
    {
        logger.LogInformation(message: "[{CorrelationId}] Finalizing output", args: context.CorrelationId);

        try
        {
            // Use the per-folder destination storage from the job context when
            // available; fall back to the DI-injected singleton for default installs.
            IStorage effectiveStorage = context.DestinationStorage ?? storage;

            // Null → spec defaults. Never skip everything.
            HlsDerivatives derivatives = input.HlsDerivatives ?? new HlsDerivatives();

            effectiveStorage.CreateDirectory(path: input.OutputDirectory);

            IOutputStrategy strategy = outputStrategyFactory.Resolve(format: input.Plan.Format);

            // GenerateMasterPlaylist — master playlist written by the output strategy.
            // The flag currently gates the whole FinalizeAsync call because the output
            // strategies pair playlist writing with segment finalization; teasing the
            // two apart is a strategy-level change, not a finalize-stage concern.
            if (derivatives.GenerateMasterPlaylist)
            {
                input.Progress?.OnStageStarted(stageName: "Building Master Playlist");
                await strategy.FinalizeAsync(
                    outputDirectory: input.OutputDirectory,
                    plan: input.Plan,
                    mediaTitle: input.MediaTitle,
                    ct: ct
                );
                input.Progress?.OnStageCompleted(stageName: "Building Master Playlist", duration: TimeSpan.Zero);
            }

            // GenerateChapters — write chapters.vtt from MediaInfo.
            if (
                derivatives.GenerateChapters
                && context.MediaInfo is not null
                && context.MediaInfo.Chapters.Count > 0
            )
            {
                input.Progress?.OnStageStarted(stageName: "Extracting chapters");
                await chapterWriter.WriteChaptersAsync(
                    outputDirectory: input.OutputDirectory,
                    chapters: context.MediaInfo.Chapters,
                    ct: ct,
                    includeThumbUris: derivatives.GenerateChapterThumbs
                );
                input.Progress?.OnStageCompleted(stageName: "Extracting chapters", duration: TimeSpan.Zero);

                logger.LogDebug(
                    message: "[{CorrelationId}] Wrote {Count} chapters to chapters.vtt", args: [context.CorrelationId, context.MediaInfo.Chapters.Count]
                );
            }

            // GenerateFontsJson — write fonts.json manifest from previously extracted fonts.
            if (derivatives.GenerateFontsJson)
            {
                input.Progress?.OnStageStarted(stageName: "Extracting fonts");
                int fontsWritten = await fontExtractor.WriteFontManifestAsync(
                    outputDirectory: input.OutputDirectory,
                    ct: ct
                );
                input.Progress?.OnStageCompleted(stageName: "Extracting fonts", duration: TimeSpan.Zero);

                // Completeness gate: a source with embedded fonts whose extraction
                // came up short would render subtitles with missing glyphs. Fail
                // the encode instead of publishing broken output — the orchestrator
                // only publishes to the destination when the result is successful.
                int expectedFonts = context.MediaInfo is null
                    ? 0
                    : fontExtractor.CountFontAttachments(attachments: context.MediaInfo.Attachments);

                if (expectedFonts > 0 && fontsWritten < expectedFonts)
                    return new StageFailure(
                        Error: new(
                            Kind: EncodingErrorKind.Unknown,
                            Message: $"Font extraction incomplete: source has {expectedFonts} embedded font(s) "
                                     + $"but only {fontsWritten} were extracted. Subtitle rendering would be "
                                     + "missing fonts, so the output is not published.",
                            FfmpegStderr: null,
                            StageName: Name,
                            Recoverable: true
                        )
                    );
            }
            else
            {
                logger.LogDebug(
                    message: "[{CorrelationId}] Skipping fonts.json (GenerateFontsJson=false)",
                    args: context.CorrelationId
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
                    message: "HlsDerivatives.GenerateIFramePlaylists is set but no IFramePlaylistGenerator "
                             + "is wired. Leave it false until I-frame playlist support lands."
                );

            if (derivatives.ExtractClosedCaptions)
                throw new NotSupportedException(
                    message: "HlsDerivatives.ExtractClosedCaptions is set but no CcExtractor is wired. "
                             + "Leave it false until CEA-608/708 extraction lands."
                );

            // SubtitleImsc is reserved for future work — log a warning instead
            // of throwing so existing profiles created before WebVtt/Imsc were
            // wired (when the toggle was a no-op) don't suddenly hard-fail.
            // The dashboard label flags it as "not yet implemented" so users
            // see the same intent on the editor side.
            if (derivatives.SubtitleImsc)
                logger.LogWarning(
                    message: "[{CorrelationId}] HlsDerivatives.SubtitleImsc is true but IMSC subtitle output is not implemented — flag ignored. Untick it in the encoder profile to silence this warning.",
                    args: context.CorrelationId
                );

            IReadOnlyList<StorageEntry> allEntries = effectiveStorage.List(
                path: input.OutputDirectory,
                pattern: "*",
                recursive: true
            );

            long totalSize = allEntries.Where(predicate: e => !e.IsDirectory).Sum(selector: e => e.SizeBytes);

            // Emit .nomercy.json when the encode has a resolved BundleLayout and
            // the writer is wired (DI singleton). Skipped when layout is null
            // (legacy callers that don't set MediaItem on the context).
            if (
                input.Plan.Layout is BundleLayout layout
                && blueprintWriter is not null
                && context.MediaInfo is not null
            )
                await WriteBlueprintAsync(
                    effectiveStorage: effectiveStorage,
                    outputDirectory: input.OutputDirectory,
                    layout: layout,
                    plan: input.Plan,
                    allEntries: allEntries,
                    context: context,
                    profile: input.Profile,
                    ct: ct
                );

            return new StageSuccess<FinalizeOutput>(Value: new(OutputPath: input.OutputDirectory, OutputSizeBytes: totalSize));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new StageFailure(
                Error: new(
                    Kind: EncodingErrorKind.Unknown,
                    Message: $"Finalization failed: {ex.Message}",
                    FfmpegStderr: null,
                    StageName: Name,
                    Recoverable: false
                )
            );
        }
    }

    private async Task WriteBlueprintAsync(
        IStorage effectiveStorage,
        string outputDirectory,
        BundleLayout layout,
        OutputPlan plan,
        IReadOnlyList<StorageEntry> allEntries,
        EncodingContext context,
        EncodingProfile? profile,
        CancellationToken ct
    )
    {
        // Relative to the media folder, which is where the encoder actually writes
        // video_*/, audio_*/ and the rest — allEntries comes from listing
        // input.OutputDirectory, so entry.Path is already in that domain.
        string dirPrefix = outputDirectory.TrimEnd(trimChar: '/') + "/";

        List<string> relFiles = [];
        foreach (StorageEntry entry in allEntries)
        {
            if (entry.IsDirectory)
                continue;
            string rel = entry.Path.StartsWith(value: dirPrefix, comparisonType: StringComparison.OrdinalIgnoreCase)
                ? entry.Path[dirPrefix.Length..]
                : entry.Path;
            // Exclude the blueprint itself from its own file list.
            if (rel.Equals(value: MediaBlueprintWriter.FileName, comparisonType: StringComparison.OrdinalIgnoreCase))
                continue;
            relFiles.Add(item: rel);
        }

        string encoderVersion = typeof(FinalizeStage).Assembly.GetName().Version?.ToString() ?? "3";
        // The real media folder this encode ends up in — recorded INSIDE the file
        // as output_location. NOT where the file is written: every encode runs in a
        // staging temp dir and PublishTempDirAsync sweeps that dir's contents to the
        // real folder afterward. Writing to OriginalOutputDirectory here put the
        // blueprint outside the temp dir, so publish never carried it and it never
        // reached the media folder.
        string mediaRoot = context.OriginalOutputDirectory ?? outputDirectory;

        // Layout is only ever resolved (PlanStage) when context.MediaItem is set —
        // see OutputNamingResolver.Resolve's caller — so MediaItem is guaranteed
        // non-null here.
        //
        // Write to outputDirectory (the staging temp dir where video_*/, audio_*/
        // etc. are assembled) so the blueprint publishes to the media root exactly
        // like every rendition does. originalSourcePath carries the real source, not
        // the staging lease MediaInfo.FilePath points at.
        await blueprintWriter!.WriteAsync(
            storage: effectiveStorage,
            mediaRootPath: outputDirectory,
            source: context.MediaInfo!,
            identity: BlueprintIdentityFactory.From(media: context.MediaItem!),
            plan: plan,
            layout: layout,
            outputFiles: relFiles,
            outputLocation: mediaRoot,
            encoderVersion: encoderVersion,
            profileFingerprint: profile is not null ? ProfileFingerprint.Compute(profile: profile) : null,
            createdAt: DateTime.UtcNow,
            completedAt: DateTime.UtcNow,
            ct: ct,
            originalSourcePath: context.OriginalInputPath
        );

        logger.LogInformation(
            message: "[{CorrelationId}] Wrote {FileName} into staging {StagingDir} ({FileCount} files); publishes to media root {MediaRoot}", args: [context.CorrelationId, MediaBlueprintWriter.FileName, outputDirectory, relFiles.Count, mediaRoot]
        );
    }
}
