using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.BuildingBlocks.Drm;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Metadata;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.PostProcess;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Subtitles;
using NoMercy.Storage;
using DrmConfig = NoMercy.Encoder.BuildingBlocks.Drm.DrmConfig;

namespace NoMercy.Encoder.Pipeline.Stages;

public record BuildInput(
    ExecutionPlan Plan,
    string InputPath,
    string OutputDirectory,
    string MediaTitle,
    TimeSpan? DurationLimit = null,
    EncodingPass Pass = EncodingPass.Single,
    string? StatsFilePath = null,
    int Pass1VariantIndex = 0,
    DecomposedTask? TaskFilter = null,
    long? ResumeFromMs = null
);

public class BuildStage(
    EncoderOptions options,
    IFontExtractor fontExtractor,
    ISubtitleExtractor subtitleExtractor,
    IOutputStrategyFactory outputStrategyFactory,
    IEnumerable<IDrmProcessor> drmProcessors,
    ILogger<BuildStage> logger,
    IStorage storage,
    AssBurnInFilterBuilder? assBurnInFilterBuilder = null,
    PgsBurnInFilterBuilder? pgsBurnInFilterBuilder = null,
    IMetadataInjector? metadataInjector = null,
    IMetadataMerger? metadataMerger = null
) : IPipelineStage<BuildInput, FfmpegCommand[]>, IBuildStage
{
    public string Name => "Build";

    public async Task<StageResult> ExecuteAsync(
        BuildInput input,
        EncodingContext context,
        CancellationToken ct
    )
    {
        logger.LogInformation("[{CorrelationId}] Building FFmpeg commands", context.CorrelationId);

        try
        {
            // Use the per-folder destination storage from the job context when
            // available; fall back to the DI-injected singleton for default installs.
            IStorage effectiveStorage = context.DestinationStorage ?? storage;

            input = await ApplyDrmPreparationAsync(input, ct).ConfigureAwait(false);
            // Pass 1 of a 2-pass encode: video-only analysis to the stats file,
            // no audio / subtitles / sprite / font-extraction outputs. The
            // Pass1VariantIndex selects which variant to analyze — the strategy
            // loops 0..N-1 for multi-variant profiles.
            if (input.Pass == EncodingPass.One)
            {
                if (string.IsNullOrWhiteSpace(input.StatsFilePath))
                {
                    return new StageFailure(
                        new(
                            EncodingErrorKind.Unknown,
                            "Pass 1 requires StatsFilePath to be set.",
                            null,
                            Name,
                            false
                        )
                    );
                }

                if (input.Plan.OutputPlan.VideoOutputs.Length == 0)
                {
                    return new StageFailure(
                        new(
                            EncodingErrorKind.Unknown,
                            "2-pass requires at least one video output.",
                            null,
                            Name,
                            false
                        )
                    );
                }

                if (
                    input.Pass1VariantIndex < 0
                    || input.Pass1VariantIndex >= input.Plan.OutputPlan.VideoOutputs.Length
                )
                {
                    return new StageFailure(
                        new(
                            EncodingErrorKind.Unknown,
                            $"Pass1VariantIndex {input.Pass1VariantIndex} is out of range for "
                                + $"profile with {input.Plan.OutputPlan.VideoOutputs.Length} variants.",
                            null,
                            Name,
                            false
                        )
                    );
                }

                string variantStats = VariantStatsPath(
                    input.StatsFilePath!,
                    input.Pass1VariantIndex
                );

                FfmpegCommand pass1 = BuildPass1Command(
                    input.Plan.OutputPlan,
                    context.MediaInfo,
                    input.InputPath,
                    input.OutputDirectory,
                    variantStats,
                    options.FfmpegPath,
                    input.Pass1VariantIndex
                );
                return new StageSuccess<FfmpegCommand[]>([pass1]);
            }

            // Chapter-still extraction: one single-frame WebP per chapter at
            // the chapter's exact timestamp. Triggered when a Chapters task
            // filter is present and the plan carries chapter metadata.
            if (
                input.TaskFilter?.Kind == EncodeTaskKind.Chapters
                && input.Plan.OutputPlan.Chapters is not null
            )
            {
                int chapterIndex = input.TaskFilter.OutputIndex;
                if (chapterIndex < 0 || chapterIndex >= input.Plan.OutputPlan.Chapters.Count)
                {
                    return new StageFailure(
                        new(
                            EncodingErrorKind.Unknown,
                            $"Chapter index {chapterIndex} is out of range "
                                + $"(plan has {input.Plan.OutputPlan.Chapters.Count} chapters).",
                            null,
                            Name,
                            false
                        )
                    );
                }

                effectiveStorage.CreateDirectory(input.OutputDirectory);
                effectiveStorage.CreateDirectory(
                    effectiveStorage.CombinePath(input.OutputDirectory, "chapters")
                );

                ChapterInfo chapter = input.Plan.OutputPlan.Chapters[chapterIndex];
                FfmpegCommand chapterCmd = BuildChapterStillCommand(
                    options.FfmpegPath,
                    input.InputPath,
                    input.OutputDirectory,
                    chapterIndex,
                    chapter.Start
                );
                return new StageSuccess<FfmpegCommand[]>([chapterCmd]);
            }

            // Per-task slicing: when a DecomposedTask is supplied, prune the
            // OutputPlan down to the single variant this task is responsible
            // for. Without this, every task spawns an ffmpeg that emits the
            // full ladder — 8 video tasks × 16 outputs = 128 parallel
            // encoder sessions, blowing past hardware concurrency limits and
            // double-writing every output file. Chapters already filters at
            // the dedicated branch above; Pass1 / Pass2 use Pass1VariantIndex
            // for their own slicing. Whole tasks normally keep the full plan,
            // but a dispatch-time bundle with per-kind slice descriptors set
            // narrows the plan to what this batch should emit (resource cap
            // enforcement — see VideoEncodeJob.DispatchDecomposedAsync).
            bool isPerStreamSlice =
                input.TaskFilter is { } perStreamFilter
                && perStreamFilter.Kind
                    is EncodeTaskKind.Video
                        or EncodeTaskKind.Audio
                        or EncodeTaskKind.Subtitle
                        or EncodeTaskKind.Thumbnails
                && input.Pass == EncodingPass.Single;

            bool isBundledWhole =
                input.TaskFilter is { } bundleFilter
                && bundleFilter.Kind == EncodeTaskKind.Whole
                && (
                    bundleFilter.VideoSliceIndexes is not null
                    || bundleFilter.AudioSliceIndexes is not null
                    || bundleFilter.SubtitleSliceIndexes is not null
                    || bundleFilter.IncludeThumbnails is not null
                )
                && input.Pass == EncodingPass.Single;

            if (isPerStreamSlice || isBundledWhole)
            {
                DecomposedTask taskFilter = input.TaskFilter!;
                OutputPlan sliced = BuildStageSlicing.SliceForTask(
                    input.Plan.OutputPlan,
                    taskFilter
                );
                input = input with { Plan = input.Plan with { OutputPlan = sliced } };
                logger.LogInformation(
                    "[{CorrelationId}] Sliced plan for {Kind} task #{Idx}: "
                        + "{V} video / {A} audio / {S} sub / thumbs={T}",
                    context.CorrelationId,
                    taskFilter.Kind,
                    taskFilter.OutputIndex,
                    sliced.VideoOutputs.Length,
                    sliced.AudioOutputs.Length,
                    sliced.SubtitleOutputs.Length,
                    sliced.Thumbnails is not null
                );
            }

            IOutputStrategy strategy = outputStrategyFactory.Resolve(input.Plan.OutputPlan.Format);

            // Ensure output subdirectories exist before FFmpeg runs
            effectiveStorage.CreateDirectory(input.OutputDirectory);
            foreach (string subDir in strategy.GetOutputSubdirectories(input.Plan.OutputPlan))
            {
                effectiveStorage.CreateDirectory(
                    effectiveStorage.CombinePath(input.OutputDirectory, subDir)
                );
            }

            // Ensure subtitles/ directory exists
            if (input.Plan.OutputPlan.SubtitleOutputs.Length > 0)
            {
                effectiveStorage.CreateDirectory(
                    effectiveStorage.CombinePath(input.OutputDirectory, "subtitles")
                );
            }

            FfmpegCommandBuilder builder = new();
            builder.WithGlobalExtraFlags(input.Plan.OutputPlan.GlobalExtraFlags);

            TimeSpan? resumeSeek = ResolveResumeSeek(input.ResumeFromMs);
            bool useGpuResident = UsesGpuResidentPath(input.Plan.OutputPlan);
            builder.AddInput(
                new(
                    input.InputPath,
                    SeekTo: resumeSeek,
                    Duration: input.DurationLimit,
                    HwAccelDevice: useGpuResident
                        ? input.Plan.OutputPlan.GpuAccel!.HwAccelDevice
                        : null,
                    HwAccelOutputFormat: useGpuResident
                        ? input.Plan.OutputPlan.GpuAccel!.HwAccelOutputFormat
                        : null
                )
            );

            // Exact-match acquired subtitles: inject each as an additional -i input
            // so the main command can copy the subtitle stream to the output directory.
            int acquiredInputIndex = 1;
            List<(int InputIndex, AcquiredSubtitle Sub)> exactMatchSubs = [];
            IReadOnlyList<AcquiredSubtitle> acquired =
                input.Plan.OutputPlan.AcquiredSubtitles ?? [];
            foreach (AcquiredSubtitle sub in acquired)
            {
                if (!sub.IsExactMatch)
                    continue;
                builder.AddInput(new(sub.LocalPath));
                exactMatchSubs.Add((acquiredInputIndex, sub));
                acquiredInputIndex++;
            }

            // Resolve burn-in mode first so we can emit the decision log
            // entry and choose between the ASS filter path and the PGS
            // overlay path before building the filter graph.
            SubtitleOutputPlan? burnInPlan = input.Plan.OutputPlan.SubtitleOutputs.FirstOrDefault(
                s => s.Policy == SubtitlePolicy.BurnIn
            );

            if (burnInPlan is not null)
            {
                context.DecisionsOrNoOp.Add(
                    new DecisionLog(
                        Stage: Name,
                        Key: EncoderRuleId.SubtitlesBurnInPermanent,
                        Message: "Subtitle stream will be burned permanently into video frames. "
                            + "The resulting output cannot be toggled off by the client.",
                        Data: new { burnInPlan.SourceIndex }
                    )
                );
            }

            // For PGS burn-in: build a -filter_complex overlay chain that
            // composites the bitmap subtitle stream onto the video. The chain
            // bypasses the normal FilterGraphBuilder path so we set it directly.
            bool isPgsBurnIn =
                burnInPlan is not null
                && context.MediaInfo is not null
                && pgsBurnInFilterBuilder is not null
                && burnInPlan.SourceIndex < context.MediaInfo.SubtitleStreams.Count
                && !context.MediaInfo.SubtitleStreams[burnInPlan.SourceIndex].IsTextBased;

            string? filterGraph;

            if (isPgsBurnIn)
            {
                PgsBurnInFilterChain chain = pgsBurnInFilterBuilder!.Build(
                    videoStreamIndex: 0,
                    subtitleStreamIndex: burnInPlan!.SourceIndex
                );
                filterGraph = chain.FilterComplex;
                // Override the video map label in every video output to [burned].
                OutputPlan pgsRemapped = RemapVideoToBurnedLabel(
                    input.Plan.OutputPlan,
                    chain.MapLabel
                );
                input = input with { Plan = input.Plan with { OutputPlan = pgsRemapped } };
            }
            else
            {
                filterGraph = BuildFilterGraph(
                    input.Plan.OutputPlan,
                    context.MediaInfo,
                    input.InputPath,
                    assBurnInFilterBuilder
                );
            }

            if (filterGraph is not null)
                builder.WithFilterComplex(filterGraph);

            // Pass 2 of a 2-pass encode: inject -pass 2 + -passlogfile into each video
            // output's ExtraFlags before the output strategy emits them.
            OutputPlan effectivePlan = input.Plan.OutputPlan;
            if (input.Pass == EncodingPass.Two && !string.IsNullOrEmpty(input.StatsFilePath))
            {
                effectivePlan = InjectPass2Flags(effectivePlan, input.StatsFilePath);
            }

            // Video + audio outputs via the output strategy (HLS, MKV, etc.)
            strategy.ConfigureOutput(builder, effectivePlan, input.OutputDirectory);

            // Acquired subtitle outputs: copy each exact-match sub to the subtitles dir.
            foreach ((int idx, AcquiredSubtitle sub) in exactMatchSubs)
            {
                string subFile = $"subtitles/{sub.Language}.acquired.{sub.Format}";
                builder.AddOutput(
                    new(FilePath: subFile, SubtitleCodec: "copy", MapStreams: [$"{idx}:s:0"])
                );
            }

            // Thumbnail sprite — the spritevtt muxer generates both the sprite
            // sheet (.webp) and the companion VTT cue file in one pass.
            if (input.Plan.OutputPlan.Thumbnails is not null && context.MediaInfo is not null)
            {
                ThumbnailOutputPlan thumbs = input.Plan.OutputPlan.Thumbnails;

                builder.AddOutput(
                    new(
                        FilePath: $"thumbs_{thumbs.Width}x{thumbs.Height}.webp",
                        MapStreams: ["[thumbs]"],
                        ExtraFlags: new()
                        {
                            ["-f"] = "spritevtt",
                            ["-vtt_filename"] = $"thumbs_{thumbs.Width}x{thumbs.Height}.vtt",
                        }
                    )
                );
            }

            // Text subtitles go in the main command (single-pass).
            // Bitmap subtitles need separate extraction (FFmpeg can't mux dvd_subtitle to .sub+.idx).
            List<FfmpegCommand> bitmapSubCommands = [];
            if (input.Plan.OutputPlan.SubtitleOutputs.Length > 0 && context.MediaInfo is not null)
            {
                AddTextSubtitleOutputs(
                    builder,
                    input.Plan.OutputPlan,
                    context.MediaInfo,
                    input.OutputDirectory,
                    input.MediaTitle,
                    subtitleExtractor,
                    effectiveStorage
                );

                bitmapSubCommands = BuildBitmapSubtitleCommands(
                    options.FfmpegPath,
                    input.InputPath,
                    input.Plan.OutputPlan,
                    context.MediaInfo,
                    input.OutputDirectory,
                    input.MediaTitle,
                    subtitleExtractor,
                    effectiveStorage
                );
            }

            FfmpegCommand mainCommand = builder.Build(options.FfmpegPath, input.OutputDirectory);
            bool copyMode = IsCopyMode(input.Plan.OutputPlan);
            mainCommand = InjectMetadataArgs(mainCommand, context.MediaItem, context, copyMode);

            logger.LogInformation(
                "[{CorrelationId}] FFmpeg command: {Executable} {Args}",
                context.CorrelationId,
                mainCommand.Executable,
                string.Join(" ", mainCommand.Arguments)
            );

            List<FfmpegCommand> allCommands = [mainCommand];
            allCommands.AddRange(bitmapSubCommands);

            // Font extraction — only when the source has embedded attachments (fonts).
            // This requires a separate command because -dump_attachment is incompatible
            // with encoding outputs.
            //
            // Run-once semantics: when tasks are decomposed, fonts are extracted by
            // EXACTLY ONE task to keep the on-disk fonts/ dir stable. We gate on the
            // first video task (OutputIndex=0) or the Thumbnails task when there is
            // no video. Without this gate every per-rung task would race the same
            // -dump_attachment writes, and Whole-plan executions would still extract
            // alongside their normal outputs.
            bool isFontOwner =
                input.TaskFilter is null
                || input.TaskFilter.Kind == EncodeTaskKind.Whole
                || (
                    input.TaskFilter.Kind == EncodeTaskKind.Video
                    && input.TaskFilter.OutputIndex == 0
                )
                || (
                    input.TaskFilter.Kind == EncodeTaskKind.Thumbnails
                    && input.Plan.OutputPlan.VideoOutputs.Length == 0
                );

            if (isFontOwner && context.MediaInfo is not null && context.MediaInfo.HasAttachments)
            {
                string fontDir = effectiveStorage.CombinePath(input.OutputDirectory, "fonts");
                effectiveStorage.CreateDirectory(fontDir);
                FfmpegCommand fontCommand = fontExtractor.BuildExtractionCommand(
                    options.FfmpegPath,
                    input.InputPath,
                    input.OutputDirectory
                );
                allCommands.Add(fontCommand);
            }

            return new StageSuccess<FfmpegCommand[]>(allCommands.ToArray());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new StageFailure(
                new(
                    EncodingErrorKind.Unknown,
                    $"Command build failed: {ex.Message}",
                    null,
                    Name,
                    false
                )
            );
        }
    }

    /// <summary>
    /// When the profile ships a DRM config, resolve the matching processor,
    /// generate key + keyinfo artifacts in the output directory, and splice
    /// <c>-hls_key_info_file</c> into every video output's extra flags so
    /// the output strategy emits it on the ffmpeg command. Returns the input
    /// unchanged when DRM is disabled or no processor handles the method.
    /// </summary>
    private async Task<BuildInput> ApplyDrmPreparationAsync(BuildInput input, CancellationToken ct)
    {
        DrmConfig? drm = input.Plan.OutputPlan.Drm;
        if (drm is null || drm.Method == DrmMethod.None)
            return input;

        IDrmProcessor? processor = drmProcessors.FirstOrDefault(p => p.Method == drm.Method);
        if (processor is null)
        {
            logger.LogWarning(
                "No DRM processor registered for {Method} — encoding without DRM",
                drm.Method
            );
            return input;
        }

        DrmArtifact artifact = await processor
            .PrepareAsync(input.OutputDirectory, drm, ct)
            .ConfigureAwait(false);

        VideoOutputPlan[] encryptedVideos = input
            .Plan.OutputPlan.VideoOutputs.Select(v =>
            {
                Dictionary<string, string> extra = new(v.ExtraFlags)
                {
                    ["-hls_key_info_file"] = artifact.KeyInfoFilePath,
                };
                return v with { ExtraFlags = extra };
            })
            .ToArray();

        OutputPlan newOutputPlan = input.Plan.OutputPlan with { VideoOutputs = encryptedVideos };
        ExecutionPlan newPlan = input.Plan with { OutputPlan = newOutputPlan };
        return input with { Plan = newPlan };
    }

    /// <summary>
    /// Adds text subtitle outputs (WebVTT, ASS) to the main FFmpeg command builder.
    /// Bitmap subtitles are handled separately via BuildBitmapSubtitleCommands.
    /// </summary>
    private static void AddTextSubtitleOutputs(
        FfmpegCommandBuilder builder,
        OutputPlan plan,
        MediaInfo mediaInfo,
        string outputDirectory,
        string mediaTitle,
        ISubtitleExtractor subtitleExtractor,
        IStorage storage
    )
    {
        foreach (SubtitleOutputPlan subPlan in plan.SubtitleOutputs)
        {
            if (subPlan.Policy == SubtitlePolicy.BurnIn)
                continue;

            if (subPlan.Action is not (StreamAction.Extract or StreamAction.Copy))
                continue;

            if (subPlan.SourceIndex >= mediaInfo.SubtitleStreams.Count)
                continue;

            SubtitleStreamInfo stream = mediaInfo.SubtitleStreams[subPlan.SourceIndex];

            // Only text subtitles in the main command
            if (!stream.IsTextBased)
                continue;

            SubtitleOutputInfo info = subtitleExtractor.ResolveOutput(
                subPlan,
                stream,
                outputDirectory,
                mediaTitle
            );

            // Ensure subtitle directory exists (storage-relative parent of OutputPath).
            string? parentDir = storage.GetParent(info.OutputPath);
            if (parentDir is not null)
                storage.CreateDirectory(storage.CombinePath(outputDirectory, parentDir));

            // FFmpeg gets the relative path (CWD = output directory)
            builder.AddOutput(
                new(
                    FilePath: info.OutputPath,
                    SubtitleCodec: info.FfmpegCodec,
                    MapStreams: [$"0:s:{info.SourceIndex}"]
                )
            );
        }
    }

    /// <summary>
    /// Builds separate FFmpeg commands for bitmap subtitle extraction.
    /// Bitmap subs (dvd_subtitle, PGS) can't be muxed to .sub+.idx in a multi-output command.
    /// They're extracted as MKS (Matroska subtitle container) which preserves the original format.
    /// </summary>
    private static List<FfmpegCommand> BuildBitmapSubtitleCommands(
        string ffmpegPath,
        string inputPath,
        OutputPlan plan,
        MediaInfo mediaInfo,
        string outputDirectory,
        string mediaTitle,
        ISubtitleExtractor subtitleExtractor,
        IStorage storage
    )
    {
        List<FfmpegCommand> commands = [];

        foreach (SubtitleOutputPlan subPlan in plan.SubtitleOutputs)
        {
            if (subPlan.Policy == SubtitlePolicy.BurnIn)
                continue;

            if (subPlan.Action is not (StreamAction.Extract or StreamAction.Copy))
                continue;

            if (subPlan.SourceIndex >= mediaInfo.SubtitleStreams.Count)
                continue;

            SubtitleStreamInfo stream = mediaInfo.SubtitleStreams[subPlan.SourceIndex];

            // Only bitmap subtitles here
            if (stream.IsTextBased)
                continue;

            SubtitleOutputInfo info = subtitleExtractor.ResolveOutput(
                subPlan,
                stream,
                outputDirectory,
                mediaTitle
            );

            // Ensure subtitle directory exists (storage-relative parent of OutputPath).
            string? parentDir = storage.GetParent(info.OutputPath);
            if (parentDir is not null)
                storage.CreateDirectory(storage.CombinePath(outputDirectory, parentDir));

            // Use MKS (Matroska) container for bitmap subs.
            // Must specify -f matroska explicitly — FFmpeg doesn't auto-detect .mks.
            string outputPath = Path.ChangeExtension(info.OutputPath, ".mks");

            FfmpegCommand cmd = new FfmpegCommandBuilder()
                .WithGlobalOptions(new(ProgressPipe: false, Overwrite: true))
                .AddInput(new(inputPath))
                .AddOutput(
                    new(
                        FilePath: outputPath,
                        SubtitleCodec: "copy",
                        MapStreams: [$"0:s:{info.SourceIndex}"],
                        ExtraFlags: new() { ["-f"] = "matroska" }
                    )
                )
                .Build(ffmpegPath, outputDirectory);

            commands.Add(cmd);
        }

        return commands;
    }

    /// <summary>
    /// Returns a copy of <paramref name="plan"/> with <c>-pass 2</c> +
    /// <c>-passlogfile</c> injected into every video output's extra flags,
    /// so the shared output strategy emits them on the FFmpeg command.
    /// Each variant gets its own stats file (keyed on index) — the strategy
    /// generates the matching set in pass 1.
    /// </summary>
    private static OutputPlan InjectPass2Flags(OutputPlan plan, string statsFilePath)
    {
        VideoOutputPlan[] updated = plan
            .VideoOutputs.Select(
                (v, index) =>
                {
                    Dictionary<string, string> flags = new(v.ExtraFlags)
                    {
                        ["-pass"] = "2",
                        ["-passlogfile"] = VariantStatsPath(statsFilePath, index),
                    };
                    return v with { ExtraFlags = flags };
                }
            )
            .ToArray();

        return plan with
        {
            VideoOutputs = updated,
        };
    }

    /// <summary>
    /// Per-variant stats path — each variant writes its own <c>-0.log</c>
    /// and <c>-0.log.mbtree</c> so measurements stay independent. Appending
    /// <c>_v{index}</c> to the base path keeps them colocated.
    /// </summary>
    internal static string VariantStatsPath(string basePath, int variantIndex) =>
        $"{basePath}_v{variantIndex}";

    /// <summary>
    /// Builds the pass-1 FFmpeg command: video-only analysis that writes its
    /// stats to <paramref name="statsFilePath"/> and discards actual output.
    /// <paramref name="variantIndex"/> picks which variant to analyze — the
    /// strategy loops 0..N-1 for multi-variant profiles.
    /// </summary>
    private static FfmpegCommand BuildPass1Command(
        OutputPlan plan,
        MediaInfo? mediaInfo,
        string inputPath,
        string outputDirectory,
        string statsFilePath,
        string ffmpegPath,
        int variantIndex = 0
    )
    {
        VideoOutputPlan video = plan.VideoOutputs[variantIndex];

        FfmpegCommandBuilder builder = new();
        builder.AddInput(new(inputPath));

        // Pass 1 analyzes the single target variant — strip the other variants,
        // audio, subtitles, and thumbnails so the filter graph only produces
        // the one video label. Much cheaper than decoding + filtering 4 variants
        // when only one is being measured.
        OutputPlan videoOnly = plan with
        {
            VideoOutputs = [video],
            AudioOutputs = [],
            SubtitleOutputs = [],
            Thumbnails = null,
        };
        // Pass 1 never burns subtitles — no builder needed.
        string? filterGraph = BuildFilterGraph(
            videoOnly,
            mediaInfo,
            inputPath,
            assBurnInFilterBuilder: null
        );
        if (filterGraph is not null)
            builder.WithFilterComplex(filterGraph);

        // Pass 1 output: video encoder settings + -pass 1 + null sink.
        Dictionary<string, string> extraFlags = new(video.ExtraFlags)
        {
            ["-pass"] = "1",
            ["-passlogfile"] = statsFilePath,
            ["-an"] = string.Empty,
            ["-sn"] = string.Empty,
            ["-f"] = "null",
        };

        builder.AddOutput(
            new(
                FilePath: "-",
                VideoCodec: video.EncoderName,
                VideoBitrateKbps: video.BitrateKbps > 0 ? video.BitrateKbps : null,
                Preset: video.Preset,
                Profile: video.Profile,
                Level: video.Level,
                PixelFormat: video.TenBit ? video.PixelFormat : null,
                MapStreams: [video.MapLabel],
                ExtraFlags: extraFlags
            )
        );

        return builder.Build(ffmpegPath, outputDirectory);
    }

    /// <summary>
    /// Reduces a multi-variant <see cref="OutputPlan"/> down to just the
    /// outputs a single <see cref="DecomposedTask"/> needs to emit. Returns
    /// the plan unchanged for <see cref="EncodeTaskKind.Whole"/>, leaves
    /// <see cref="EncodeTaskKind.Chapters"/> to the dedicated chapter branch,
    /// and lets <see cref="EncodeTaskKind.Pass1"/> / <see cref="EncodeTaskKind.Pass2"/>
    /// keep using Pass1VariantIndex for their own slicing.
    ///
    /// When <see cref="DecomposedTask.SourceIndexes"/> is set the slice
    /// includes every entry it lists (smart-batched bucket of rungs sharing
    /// one ffmpeg via filter_complex split); otherwise the legacy single
    /// <see cref="DecomposedTask.OutputIndex"/> slice is used.
    ///
    /// Acquired subtitles are kept on Subtitle tasks (the consumer) and
    /// dropped from Video / Audio / Thumbnails tasks so they don't add
    /// spurious <c>-i</c> inputs to encodes that won't consume them.
    /// Chapters / Drm / Layout are preserved as metadata across all slices.
    /// </summary>
    /// <summary>
    /// GPU-resident when the plan carries a resolved GpuAccel plan, has video
    /// outputs, and requests no thumbnails (sprite generation needs a CPU
    /// download that would break the GPU-memory chain). Drives both the
    /// <c>-hwaccel</c> decode flags and the GPU scale branch.
    /// </summary>
    private static bool UsesGpuResidentPath(OutputPlan plan) =>
        plan.GpuAccel is not null && plan.Thumbnails is null && plan.VideoOutputs.Length > 0;

    private static string? BuildFilterGraph(
        OutputPlan plan,
        MediaInfo? mediaInfo,
        string inputPath,
        AssBurnInFilterBuilder? assBurnInFilterBuilder
    )
    {
        VideoOutputPlan[] videoOutputs = plan.VideoOutputs;

        // No video outputs or no filter-graph labels — nothing to build
        if (videoOutputs.Length == 0 || !videoOutputs.Any(v => v.MapLabel.StartsWith('[')))
            return null;

        // Source dimensions are required to decide copy vs. scale
        if (mediaInfo is null || mediaInfo.VideoStreams.Count == 0)
            return null;

        int sourceWidth = mediaInfo.VideoStreams[0].Width;
        int sourceHeight = mediaInfo.VideoStreams[0].Height;
        bool sourceIs10Bit = mediaInfo.VideoStreams[0].BitDepth > 8;
        bool hasThumbnails = plan.Thumbnails is not null;
        bool sourceIsHdr = mediaInfo.VideoStreams[0].IsHdr;
        string? thumbnailTonemapChain = videoOutputs
            .Select(v => v.TonemapFilterChain)
            .FirstOrDefault(c => !string.IsNullOrEmpty(c));

        // First text-based subtitle output with BurnIn mode (PGS burn-in uses
        // the overlay path handled before this method is called).
        string? burnInExpr = ResolveBurnInExpression(
            plan.SubtitleOutputs,
            mediaInfo,
            inputPath,
            assBurnInFilterBuilder
        );

        FilterGraphBuilder fg = new();

        // GPU-resident path: frames are decoded into GPU memory (-hwaccel set on
        // the input) and scaled on the GPU. Eligibility guarantees no tonemap /
        // crop / burn-in, so every branch is just a GPU scale. Sprites need a CPU
        // download, so this path is skipped when thumbnails are requested.
        if (UsesGpuResidentPath(plan))
        {
            string scaleFilter = plan.GpuAccel!.ScaleFilter;
            if (videoOutputs.Length == 1)
            {
                VideoOutputPlan only = videoOutputs[0];
                fg.AddGpuScale(
                    "0:v:0",
                    scaleFilter,
                    only.Width,
                    only.Height,
                    only.MapLabel.Trim('[', ']')
                );
            }
            else
            {
                List<string> splitLabels = videoOutputs.Select((_, i) => $"gsplit{i}").ToList();
                fg.AddSplit("0:v:0", splitLabels.ToArray());
                for (int i = 0; i < videoOutputs.Length; i++)
                {
                    VideoOutputPlan rung = videoOutputs[i];
                    fg.AddGpuScale(
                        splitLabels[i],
                        scaleFilter,
                        rung.Width,
                        rung.Height,
                        rung.MapLabel.Trim('[', ']')
                    );
                }
            }

            return fg.HasFilters ? fg.Build() : null;
        }

        // Tonemap once: when any SDR consumer (rung or sprite) needs the same
        // HDR→SDR tonemap chain we run it ONCE on the source and route every
        // consumer from the shared [sdr] intermediate. Per-frame the
        // zscale/tonemap chain is the most expensive filter in the graph —
        // running it per branch burns CPU for identical output, and a sprite
        // sampling raw HDR shows crushed colours. Only genuinely mixed tonemap
        // chains (different algorithms per rung) fall through to the per-branch
        // legacy path below.
        bool[] needsTonemapPerBranch = new bool[videoOutputs.Length];
        for (int i = 0; i < videoOutputs.Length; i++)
        {
            needsTonemapPerBranch[i] =
                videoOutputs[i].ConvertHdrToSdr && videoOutputs[i].TonemapFilterChain is not null;
        }

        string? sharedTonemap = null;
        bool dedupeTonemap = false;
        if (needsTonemapPerBranch.Count(needs => needs) >= 1)
        {
            string?[] tonemapChains = videoOutputs
                .Where((_, idx) => needsTonemapPerBranch[idx])
                .Select(video => video.TonemapFilterChain)
                .ToArray();
            if (tonemapChains.Distinct().Count() == 1)
            {
                sharedTonemap = tonemapChains[0];
                dedupeTonemap = true;
            }
        }

        // Total split branches: one per video output + one for thumbnails (if enabled)
        int totalBranches = videoOutputs.Length + (hasThumbnails ? 1 : 0);

        if (totalBranches == 1 && !hasThumbnails)
        {
            // Single video, no thumbnails — no split needed
            VideoOutputPlan single = videoOutputs[0];
            string outputLabel = single.MapLabel.Trim('[', ']');
            BuildBranchFilter(
                fg,
                "0:v:0",
                outputLabel,
                single,
                sourceWidth,
                sourceHeight,
                sourceIs10Bit,
                burnInExpr,
                tonemapAlreadyApplied: false
            );
        }
        else if (dedupeTonemap)
        {
            // Shared HDR→SDR tonemap: run tonemap once, then split.
            //
            // Branches split into two groups:
            //   - HDR-pass branches (needsTonemap=false): use [0:v:0] directly
            //   - SDR-tonemapped branches: use [sdr] intermediate
            // Thumbnails (SDR-friendly format) branch from [sdr] when present.
            int sdrBranchCount = needsTonemapPerBranch.Count(needs => needs);
            int hdrPassBranchCount = videoOutputs.Length - sdrBranchCount;
            int thumbsFromSdr = hasThumbnails ? 1 : 0;

            int sourceSplitCount = hdrPassBranchCount + 1; // +1 for tonemap source

            List<string> sourceSplitLabels = new();
            for (int i = 0; i < hdrPassBranchCount; i++)
                sourceSplitLabels.Add($"hdr{i}");
            sourceSplitLabels.Add("tonemap_in");

            if (sourceSplitCount == 1)
            {
                // Only one downstream consumer of [0:v:0] — the tonemap input.
                // No source split needed; tonemap directly from [0:v:0].
                fg.AddFilter("0:v:0", sharedTonemap!, "sdr");
            }
            else
            {
                fg.AddSplit("0:v:0", sourceSplitLabels.ToArray());
                fg.AddFilter("tonemap_in", sharedTonemap!, "sdr");
            }

            // Split [sdr] into per-SDR-branch labels + thumbnail source.
            int sdrSplitCount = sdrBranchCount + thumbsFromSdr;
            List<string> sdrSplitLabels = new();
            for (int i = 0; i < sdrBranchCount; i++)
                sdrSplitLabels.Add($"sdr{i}");
            if (hasThumbnails)
                sdrSplitLabels.Add("thumbsrc");

            if (sdrSplitCount == 1)
            {
                // One consumer of [sdr] — feed it directly without a split.
                // Rewrite the consumer's input label later.
            }
            else
            {
                fg.AddSplit("sdr", sdrSplitLabels.ToArray());
            }

            int hdrIdx = 0;
            int sdrIdx = 0;
            string sdrInputForSingleConsumer = "sdr";
            for (int i = 0; i < videoOutputs.Length; i++)
            {
                VideoOutputPlan video = videoOutputs[i];
                string outputLabel = video.MapLabel.Trim('[', ']');
                string inputLabel;
                bool tonemapApplied;

                if (needsTonemapPerBranch[i])
                {
                    inputLabel =
                        sdrSplitCount == 1 ? sdrInputForSingleConsumer : sdrSplitLabels[sdrIdx++];
                    tonemapApplied = true;
                }
                else
                {
                    inputLabel = sourceSplitCount == 1 ? "0:v:0" : $"hdr{hdrIdx++}";
                    tonemapApplied = false;
                }

                BuildBranchFilter(
                    fg,
                    inputLabel,
                    outputLabel,
                    video,
                    sourceWidth,
                    sourceHeight,
                    sourceIs10Bit,
                    burnInExpr,
                    tonemapAlreadyApplied: tonemapApplied
                );
            }

            if (hasThumbnails)
            {
                ThumbnailOutputPlan thumbs = plan.Thumbnails!;
                string thumbInput = sdrSplitCount == 1 ? sdrInputForSingleConsumer : "thumbsrc";
                fg.AddFilter(
                    thumbInput,
                    $"format=yuvj420p,fps=1/{thumbs.IntervalSeconds},scale={thumbs.Width}:-2",
                    "thumbs"
                );
            }
        }
        else
        {
            // Legacy path: no dedupe possible (mixed tonemap params, single
            // tonemap branch, or none at all). Split source N+1 ways and let
            // each branch run its own filter chain.
            List<string> splitLabels = videoOutputs.Select((_, i) => $"split{i}").ToList();
            if (hasThumbnails)
                splitLabels.Add("thumbsrc");

            fg.AddSplit("0:v:0", splitLabels.ToArray());

            for (int i = 0; i < videoOutputs.Length; i++)
            {
                VideoOutputPlan video = videoOutputs[i];
                string outputLabel = video.MapLabel.Trim('[', ']');

                BuildBranchFilter(
                    fg,
                    splitLabels[i],
                    outputLabel,
                    video,
                    sourceWidth,
                    sourceHeight,
                    sourceIs10Bit,
                    burnInExpr,
                    tonemapAlreadyApplied: false
                );
            }

            // Thumbnail branch — uses HDR source directly when no dedupe is
            // possible (thumbnails from HDR may show crushed colors but this
            // branch only fires in the rare mixed-tonemap fallback).
            if (hasThumbnails)
            {
                ThumbnailOutputPlan thumbs = plan.Thumbnails!;
                fg.AddFilter(
                    "thumbsrc",
                    ThumbnailFilterResolver.Resolve(
                        thumbs.IntervalSeconds,
                        thumbs.Width,
                        sourceIsHdr,
                        thumbnailTonemapChain
                    ),
                    "thumbs"
                );
            }
        }

        return fg.HasFilters ? fg.Build() : null;
    }

    /// <summary>
    /// Builds the filter chain for a single video output branch.
    /// Pipeline: tonemap (HDR→SDR) → scale (resolution) → format (pixel format) → burn-in subs.
    /// Each step is skipped when not needed.
    ///
    /// <paramref name="tonemapAlreadyApplied"/> is set by the dedupe path
    /// in <see cref="BuildFilterGraph"/> when the source has been
    /// pre-tonemapped once (shared <c>[sdr]</c> intermediate) — this branch
    /// then skips its own tonemap chain and the implicit 8-bit conversion
    /// it carries.
    /// </summary>
    private static void BuildBranchFilter(
        IFilterGraphBuilder fg,
        string inputLabel,
        string outputLabel,
        VideoOutputPlan video,
        int sourceWidth,
        int sourceHeight,
        bool sourceIs10Bit,
        string? burnInExpr,
        bool tonemapAlreadyApplied
    )
    {
        bool needsCrop = !string.IsNullOrWhiteSpace(video.CropFilter);
        bool needsTonemap =
            !tonemapAlreadyApplied && video.ConvertHdrToSdr && video.TonemapFilterChain is not null;
        bool needsScale = video.Width != sourceWidth || video.Height != sourceHeight;
        bool needs8BitConversion = !tonemapAlreadyApplied && sourceIs10Bit && !video.TenBit;
        bool needsBurnIn = burnInExpr is not null;

        if (!needsCrop && !needsTonemap && !needsScale && !needs8BitConversion && !needsBurnIn)
        {
            fg.AddFilter(inputLabel, "copy", outputLabel);
            return;
        }

        // Determine whether we need to terminate the tonemap/scale/format chain on
        // an intermediate label (because burn-in goes after) or on the output label.
        string videoChainEnd = needsBurnIn ? $"{outputLabel}_presub" : outputLabel;

        string currentLabel = inputLabel;

        // Step 0: Crop (remove letterbox / pillarbox). Must run before tonemap /
        // scale so the later filters operate on the real picture region.
        if (needsCrop)
        {
            bool hasDownstreamWork =
                needsTonemap || needsScale || needs8BitConversion || needsBurnIn;
            string cropOut = hasDownstreamWork ? $"{outputLabel}_cropped" : videoChainEnd;
            fg.AddFilter(currentLabel, $"crop={video.CropFilter}", cropOut);
            currentLabel = cropOut;

            if (!hasDownstreamWork)
                return;
        }

        // Step 1: Tonemap (HDR→SDR) — outputs yuv420p, so 8-bit conversion is included
        if (needsTonemap)
        {
            string nextLabel = needsScale ? $"{outputLabel}_tonemapped" : videoChainEnd;
            fg.AddFilter(currentLabel, video.TonemapFilterChain!, nextLabel);
            currentLabel = nextLabel;

            needs8BitConversion = false;

            if (!needsScale && !needsBurnIn)
                return;
        }

        // Step 2: Scale + format
        if (needsScale && needs8BitConversion)
        {
            string intermediate = $"{outputLabel}_scaled";
            fg.AddScaleWidth(currentLabel, video.Width, intermediate);
            fg.AddFilter(intermediate, $"format={video.PixelFormat}", videoChainEnd);
            currentLabel = videoChainEnd;
        }
        else if (needsScale)
        {
            fg.AddScaleWidth(currentLabel, video.Width, videoChainEnd);
            currentLabel = videoChainEnd;
        }
        else if (needs8BitConversion)
        {
            fg.AddFilter(currentLabel, $"format={video.PixelFormat}", videoChainEnd);
            currentLabel = videoChainEnd;
        }
        else if (needsBurnIn && !needsTonemap)
        {
            // No video processing yet — but we still need to land on the intermediate label
            // so the subtitles filter below can read from it.
            fg.AddFilter(currentLabel, "copy", videoChainEnd);
            currentLabel = videoChainEnd;
        }

        // Step 3: Burn-in subtitles — always last (after resolution is final).
        if (needsBurnIn)
        {
            fg.AddFilter(currentLabel, burnInExpr!, outputLabel);
        }
    }

    /// <summary>
    /// Resolves the first text-based burn-in subtitle output into an FFmpeg
    /// filter expression. Returns null when no burn-in subtitle is requested
    /// or when the first burn-in track is bitmap-based (PGS/DVD — those use
    /// the overlay path handled separately before <see cref="BuildFilterGraph"/>
    /// is called).
    ///
    /// <para>For ASS/SSA sources the expression uses libass via the
    /// <c>ass=</c> filter routed through <see cref="AssBurnInFilterBuilder"/>,
    /// which applies correct filter-graph escaping and threads the
    /// <c>fontsdir</c> hint when present. All other text codecs fall back to
    /// the generic <c>subtitles=</c> filter with stream-index selection.</para>
    /// </summary>
    private static string? ResolveBurnInExpression(
        SubtitleOutputPlan[] subs,
        MediaInfo mediaInfo,
        string inputPath,
        AssBurnInFilterBuilder? assBurnInFilterBuilder
    )
    {
        SubtitleOutputPlan? burnIn = subs.FirstOrDefault(s => s.Policy == SubtitlePolicy.BurnIn);
        if (burnIn is null)
            return null;

        // Only text-based codecs here — bitmap (PGS/DVD) uses the overlay path.
        if (burnIn.SourceIndex < mediaInfo.SubtitleStreams.Count)
        {
            SubtitleStreamInfo stream = mediaInfo.SubtitleStreams[burnIn.SourceIndex];
            if (!stream.IsTextBased)
                return null;

            // ASS/SSA: route through dedicated builder for libass + fontsdir.
            if (
                assBurnInFilterBuilder is not null
                && (
                    stream.Codec.Equals("ass", StringComparison.OrdinalIgnoreCase)
                    || stream.Codec.Equals("ssa", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return assBurnInFilterBuilder.Build(inputPath);
            }
        }

        // Generic text subtitle fallback: subtitles= filter with stream-index.
        // Delegate to shared escape logic so both code paths stay in sync.
        string escaped = AssBurnInFilterBuilder.EscapeForFilterGraph(inputPath);

        return $"subtitles='{escaped}':si={burnIn.SourceIndex}";
    }

    /// <summary>
    /// Returns a copy of <paramref name="plan"/> with every video output's
    /// <see cref="VideoOutputPlan.MapLabel"/> replaced by
    /// <paramref name="burnedLabel"/>. Used for PGS burn-in where the
    /// <c>overlay</c> filter emits a single composited output pad that all
    /// video encoders must read from.
    /// </summary>
    private static OutputPlan RemapVideoToBurnedLabel(OutputPlan plan, string burnedLabel)
    {
        VideoOutputPlan[] remapped = plan
            .VideoOutputs.Select(v => v with { MapLabel = burnedLabel })
            .ToArray();

        return plan with
        {
            VideoOutputs = remapped,
        };
    }

    /// <summary>
    /// Splices -metadata / per-stream metadata / disposition / attachment
    /// args from <paramref name="mediaItem"/> into <paramref name="command"/>
    /// just before the last argument (output filename). When no injector is
    /// configured or the media item is null, the command is returned unchanged.
    ///
    /// For copy-mode encodes (<paramref name="isCopyMode"/> true), MetadataMerger
    /// is called first to apply field-level source-vs-DB precedence rules.
    /// For transcode encodes source metadata is discarded and DB tracks are used
    /// directly (streams are re-encoded from scratch, so source tags don't survive).
    /// </summary>
    private FfmpegCommand InjectMetadataArgs(
        FfmpegCommand command,
        MediaItemRef? mediaItem,
        EncodingContext context,
        bool isCopyMode
    )
    {
        if (metadataInjector is null || mediaItem is null)
            return command;

        IReadOnlyList<TrackMetadata> tracks = ResolveTracksForInjection(context, isCopyMode);

        MetadataInjectionContext ctx = new(Media: mediaItem, Tracks: tracks, AttachmentPaths: []);

        IReadOnlyList<string> metaArgs = metadataInjector.BuildArgs(ctx);
        if (metaArgs.Count == 0)
            return command;

        // Insert the metadata flags before the last argument (output filepath).
        string[] original = command.Arguments;
        string[] updated = new string[original.Length + metaArgs.Count];
        int insertAt = original.Length - 1;
        Array.Copy(original, updated, insertAt);
        for (int i = 0; i < metaArgs.Count; i++)
            updated[insertAt + i] = metaArgs[i];
        updated[^1] = original[^1];

        return command with
        {
            Arguments = updated,
        };
    }

    /// <summary>
    /// Resolves the track list to pass to MetadataInjector.
    /// When <paramref name="isCopyMode"/> is true and both SourceTracks and
    /// DbTracks are present on the context, MetadataMerger applies field-level
    /// precedence rules. Otherwise DB tracks (or an empty list) are used.
    /// </summary>
    private IReadOnlyList<TrackMetadata> ResolveTracksForInjection(
        EncodingContext context,
        bool isCopyMode
    )
    {
        IReadOnlyList<TrackMetadata> dbTracks = context.DbTracks ?? [];

        if (
            !isCopyMode
            || metadataMerger is null
            || context.SourceTracks is null
            || context.SourceTracks.Count == 0
        )
            return dbTracks;

        return metadataMerger.Merge(context.SourceTracks, dbTracks);
    }

    /// <summary>
    /// Returns true when any video output uses the "copy" pseudo-encoder or
    /// any audio output uses <see cref="StreamAction.Copy"/>, meaning source
    /// streams are passed byte-for-byte without re-encoding.
    /// </summary>
    internal static bool IsCopyMode(OutputPlan plan)
    {
        foreach (VideoOutputPlan v in plan.VideoOutputs)
        {
            if (string.Equals(v.EncoderName, "copy", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (AudioOutputPlan a in plan.AudioOutputs)
        {
            if (a.Action == StreamAction.Copy)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Converts a crash-checkpoint progress position into an input seek
    /// TimeSpan, backing off by a fixed keyframe window to ensure ffmpeg
    /// can land on a clean keyframe rather than a mid-GOP position.
    /// Returns null when ResumeFromMs is null or zero (no seek required).
    /// </summary>
    internal static TimeSpan? ResolveResumeSeek(long? resumeFromMs)
    {
        if (resumeFromMs is null or <= 0)
            return null;

        const long keyframeBackoffMs = 4000;
        long seekMs = Math.Max(0, resumeFromMs.Value - keyframeBackoffMs);
        return TimeSpan.FromMilliseconds(seekMs);
    }

    /// <summary>
    /// Builds a single-frame WebP extraction command for one chapter still.
    /// Output: <c>chapters/{index:D2}.webp</c> at 240 px wide, aspect-preserved.
    /// <c>-ss</c> before <c>-i</c> (input seek) is used for accuracy on keyframe-
    /// aligned chapter timestamps; the output is a single frame so decode cost
    /// is minimal regardless of seek position.
    /// </summary>
    internal static FfmpegCommand BuildChapterStillCommand(
        string ffmpegPath,
        string inputPath,
        string outputDirectory,
        int chapterIndex,
        TimeSpan timestamp
    )
    {
        // Forward-slash separator — matches HLS / sprite / audio / video output paths
        // across the codebase. Windows ffmpeg accepts forward slashes; using
        // Path.Combine here would emit backslashes that break manifest references.
        string outputFile = $"chapters/{chapterIndex:D2}.webp";

        return new FfmpegCommandBuilder()
            .WithGlobalOptions(new GlobalOptions(ProgressPipe: false, Overwrite: true))
            .AddInput(new InputOptions(FilePath: inputPath, SeekTo: timestamp))
            .AddOutput(
                new OutputOptions(
                    FilePath: outputFile,
                    VideoCodec: "libwebp",
                    ExtraFlags: new Dictionary<string, string>
                    {
                        ["-frames:v"] = "1",
                        ["-vf"] = "scale=240:-2",
                    }
                )
            )
            .Build(ffmpegPath, outputDirectory);
    }
}
