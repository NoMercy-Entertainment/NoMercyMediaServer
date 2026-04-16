namespace NoMercy.Encoder.Pipeline.Stages;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.PostProcess;
using NoMercy.Encoder.Profiles;

public record BuildInput(
    ExecutionPlan Plan,
    string InputPath,
    string OutputDirectory,
    string MediaTitle,
    TimeSpan? DurationLimit = null,
    EncodingPass Pass = EncodingPass.Single,
    string? StatsFilePath = null,
    int Pass1VariantIndex = 0
);

public class BuildStage(
    EncoderOptions options,
    IFontExtractor fontExtractor,
    ISubtitleExtractor subtitleExtractor,
    ILogger<BuildStage> logger
) : IPipelineStage<BuildInput, FfmpegCommand[]>
{
    public string Name => "Build";

    public Task<StageResult> ExecuteAsync(
        BuildInput input,
        EncodingContext context,
        CancellationToken ct
    )
    {
        logger.LogInformation("[{CorrelationId}] Building FFmpeg commands", context.CorrelationId);

        try
        {
            // Pass 1 of a 2-pass encode: video-only analysis to the stats file,
            // no audio / subtitles / sprite / font-extraction outputs. The
            // Pass1VariantIndex selects which variant to analyze — the strategy
            // loops 0..N-1 for multi-variant profiles.
            if (input.Pass == EncodingPass.One)
            {
                if (string.IsNullOrWhiteSpace(input.StatsFilePath))
                {
                    return Task.FromResult<StageResult>(
                        new StageFailure(
                            new EncodingError(
                                EncodingErrorKind.Unknown,
                                "Pass 1 requires StatsFilePath to be set.",
                                null,
                                Name,
                                false
                            )
                        )
                    );
                }

                if (input.Plan.OutputPlan.VideoOutputs.Length == 0)
                {
                    return Task.FromResult<StageResult>(
                        new StageFailure(
                            new EncodingError(
                                EncodingErrorKind.Unknown,
                                "2-pass requires at least one video output.",
                                null,
                                Name,
                                false
                            )
                        )
                    );
                }

                if (
                    input.Pass1VariantIndex < 0
                    || input.Pass1VariantIndex >= input.Plan.OutputPlan.VideoOutputs.Length
                )
                {
                    return Task.FromResult<StageResult>(
                        new StageFailure(
                            new EncodingError(
                                EncodingErrorKind.Unknown,
                                $"Pass1VariantIndex {input.Pass1VariantIndex} is out of range for "
                                    + $"profile with {input.Plan.OutputPlan.VideoOutputs.Length} variants.",
                                null,
                                Name,
                                false
                            )
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
                return Task.FromResult<StageResult>(new StageSuccess<FfmpegCommand[]>([pass1]));
            }

            IOutputStrategy strategy = GetStrategy(input.Plan.OutputPlan.Format);

            // Ensure output subdirectories exist before FFmpeg runs
            Directory.CreateDirectory(input.OutputDirectory);
            foreach (string subDir in strategy.GetOutputSubdirectories(input.Plan.OutputPlan))
            {
                Directory.CreateDirectory(Path.Combine(input.OutputDirectory, subDir));
            }

            // Ensure subtitles/ directory exists
            if (input.Plan.OutputPlan.SubtitleOutputs.Length > 0)
            {
                Directory.CreateDirectory(Path.Combine(input.OutputDirectory, "subtitles"));
            }

            FfmpegCommandBuilder builder = new();
            builder.AddInput(new InputOptions(input.InputPath, Duration: input.DurationLimit));

            string? filterGraph = BuildFilterGraph(
                input.Plan.OutputPlan,
                context.MediaInfo,
                input.InputPath
            );
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

            // Thumbnail sprite — the spritevtt muxer generates both the sprite
            // sheet (.webp) and the companion VTT cue file in one pass.
            if (input.Plan.OutputPlan.Thumbnails is not null && context.MediaInfo is not null)
            {
                ThumbnailOutputPlan thumbs = input.Plan.OutputPlan.Thumbnails;

                builder.AddOutput(
                    new OutputOptions(
                        FilePath: $"thumbs_{thumbs.Width}x{thumbs.Height}.webp",
                        MapStreams: ["[thumbs]"],
                        ExtraFlags: new Dictionary<string, string>
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
                    subtitleExtractor
                );

                bitmapSubCommands = BuildBitmapSubtitleCommands(
                    options.FfmpegPath,
                    input.InputPath,
                    input.Plan.OutputPlan,
                    context.MediaInfo,
                    input.OutputDirectory,
                    input.MediaTitle,
                    subtitleExtractor
                );
            }

            FfmpegCommand mainCommand = builder.Build(options.FfmpegPath, input.OutputDirectory);

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
            if (context.MediaInfo is not null && context.MediaInfo.HasAttachments)
            {
                string fontDir = Path.Combine(input.OutputDirectory, "fonts");
                Directory.CreateDirectory(fontDir);
                FfmpegCommand fontCommand = fontExtractor.BuildExtractionCommand(
                    options.FfmpegPath,
                    input.InputPath,
                    input.OutputDirectory
                );
                allCommands.Add(fontCommand);
            }

            return Task.FromResult<StageResult>(
                new StageSuccess<FfmpegCommand[]>(allCommands.ToArray())
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult<StageResult>(
                new StageFailure(
                    new EncodingError(
                        EncodingErrorKind.Unknown,
                        $"Command build failed: {ex.Message}",
                        null,
                        Name,
                        false
                    )
                )
            );
        }
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
        ISubtitleExtractor subtitleExtractor
    )
    {
        foreach (SubtitleOutputPlan subPlan in plan.SubtitleOutputs)
        {
            if (subPlan.Mode == SubtitleMode.BurnIn)
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

            // Ensure subtitle directory exists (absolute path for .NET IO)
            string absolutePath = Path.Combine(outputDirectory, info.OutputPath);
            string? parentDir = Path.GetDirectoryName(absolutePath);
            if (parentDir is not null)
                Directory.CreateDirectory(parentDir);

            // FFmpeg gets the relative path (CWD = output directory)
            builder.AddOutput(
                new OutputOptions(
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
        ISubtitleExtractor subtitleExtractor
    )
    {
        List<FfmpegCommand> commands = [];

        foreach (SubtitleOutputPlan subPlan in plan.SubtitleOutputs)
        {
            if (subPlan.Mode == SubtitleMode.BurnIn)
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

            // Ensure subtitle directory exists (absolute path for .NET IO)
            string absolutePath = Path.Combine(outputDirectory, info.OutputPath);
            string? parentDir = Path.GetDirectoryName(absolutePath);
            if (parentDir is not null)
                Directory.CreateDirectory(parentDir);

            // Use MKS (Matroska) container for bitmap subs.
            // Must specify -f matroska explicitly — FFmpeg doesn't auto-detect .mks.
            string outputPath = Path.ChangeExtension(info.OutputPath, ".mks");

            FfmpegCommand cmd = new FfmpegCommandBuilder()
                .WithGlobalOptions(new GlobalOptions(ProgressPipe: false, Overwrite: true))
                .AddInput(new InputOptions(inputPath))
                .AddOutput(
                    new OutputOptions(
                        FilePath: outputPath,
                        SubtitleCodec: "copy",
                        MapStreams: [$"0:s:{info.SourceIndex}"],
                        ExtraFlags: new Dictionary<string, string> { ["-f"] = "matroska" }
                    )
                )
                .Build(ffmpegPath, outputDirectory);

            commands.Add(cmd);
        }

        return commands;
    }

    private static IOutputStrategy GetStrategy(OutputFormat format) =>
        format switch
        {
            OutputFormat.Hls => new HlsOutputStrategy(),
            OutputFormat.Mkv => new MkvOutputStrategy(),
            OutputFormat.Mp4 => new Mp4OutputStrategy(),
            OutputFormat.Dash => new DashOutputStrategy(),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

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
        builder.AddInput(new InputOptions(inputPath));

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
        string? filterGraph = BuildFilterGraph(videoOnly, mediaInfo, inputPath);
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
            new OutputOptions(
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

    private static string? BuildFilterGraph(OutputPlan plan, MediaInfo? mediaInfo, string inputPath)
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

        // First subtitle output with BurnIn mode (at most one burns per encode).
        string? burnInExpr = ResolveBurnInExpression(plan.SubtitleOutputs, inputPath);

        FilterGraphBuilder fg = new();

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
                burnInExpr
            );
        }
        else
        {
            // Split source into N video branches + optional thumbnail branch
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
                    burnInExpr
                );
            }

            // Thumbnail branch: format=yuv420p (force 8-bit) → fps → scale → [thumbs]
            // The spritevtt muxer handles tiling and VTT generation — no tile filter needed.
            // format=yuv420p is required because the split receives raw source pixel format
            // (e.g. yuv420p10le for 10-bit content) and libwebp can't encode 10-bit.
            if (hasThumbnails)
            {
                ThumbnailOutputPlan thumbs = plan.Thumbnails!;

                fg.AddFilter(
                    "thumbsrc",
                    $"format=yuv420p,fps=1/{thumbs.IntervalSeconds},scale={thumbs.Width}:-2",
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
    /// </summary>
    private static void BuildBranchFilter(
        IFilterGraphBuilder fg,
        string inputLabel,
        string outputLabel,
        VideoOutputPlan video,
        int sourceWidth,
        int sourceHeight,
        bool sourceIs10Bit,
        string? burnInExpr
    )
    {
        bool needsTonemap = video.ConvertHdrToSdr && video.TonemapFilterChain is not null;
        bool needsScale = video.Width != sourceWidth || video.Height != sourceHeight;
        bool needs8BitConversion = sourceIs10Bit && !video.TenBit;
        bool needsBurnIn = burnInExpr is not null;

        if (!needsTonemap && !needsScale && !needs8BitConversion && !needsBurnIn)
        {
            fg.AddFilter(inputLabel, "copy", outputLabel);
            return;
        }

        // Determine whether we need to terminate the tonemap/scale/format chain on
        // an intermediate label (because burn-in goes after) or on the output label.
        string videoChainEnd = needsBurnIn ? $"{outputLabel}_presub" : outputLabel;

        string currentLabel = inputLabel;

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
    /// Resolves the first burn-in subtitle output into an FFmpeg subtitles filter
    /// expression that reads from the input file + stream index. Returns null when
    /// no burn-in subtitle is requested.
    /// </summary>
    private static string? ResolveBurnInExpression(SubtitleOutputPlan[] subs, string inputPath)
    {
        SubtitleOutputPlan? burnIn = subs.FirstOrDefault(s => s.Mode == SubtitleMode.BurnIn);
        if (burnIn is null)
            return null;

        // FFmpeg subtitle filter path escaping: single-quote the path, escape
        // colons and backslashes. Forward slashes are accepted on all platforms.
        string normalized = inputPath.Replace('\\', '/');
        string escaped = normalized.Replace(":", "\\:");

        return $"subtitles='{escaped}':si={burnIn.SourceIndex}";
    }
}
