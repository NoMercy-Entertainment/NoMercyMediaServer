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

public record BuildInput(
    ExecutionPlan Plan,
    string InputPath,
    string OutputDirectory,
    string MediaTitle,
    TimeSpan? DurationLimit = null
);

public class BuildStage(EncoderOptions options, ILogger<BuildStage> logger)
    : IPipelineStage<BuildInput, FfmpegCommand[]>
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

            string? filterGraph = BuildFilterGraph(input.Plan.OutputPlan, context.MediaInfo);
            if (filterGraph is not null)
                builder.WithFilterComplex(filterGraph);

            // Video + audio outputs via the output strategy (HLS, MKV, etc.)
            strategy.ConfigureOutput(builder, input.Plan.OutputPlan, input.OutputDirectory);

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
                    input.MediaTitle
                );

                bitmapSubCommands = BuildBitmapSubtitleCommands(
                    options.FfmpegPath,
                    input.InputPath,
                    input.Plan.OutputPlan,
                    context.MediaInfo,
                    input.OutputDirectory,
                    input.MediaTitle
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
                FontExtractor fontExtractor = new();
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
        string mediaTitle
    )
    {
        SubtitleExtractor subtitleExtractor = new();

        foreach (SubtitleOutputPlan subPlan in plan.SubtitleOutputs)
        {
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
        string mediaTitle
    )
    {
        List<FfmpegCommand> commands = [];
        SubtitleExtractor subtitleExtractor = new();

        foreach (SubtitleOutputPlan subPlan in plan.SubtitleOutputs)
        {
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

    private static string? BuildFilterGraph(OutputPlan plan, MediaInfo? mediaInfo)
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
                sourceIs10Bit
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
                    sourceIs10Bit
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
    /// Pipeline: tonemap (HDR→SDR) → scale (resolution) → format (pixel format).
    /// Each step is skipped when not needed.
    /// </summary>
    private static void BuildBranchFilter(
        IFilterGraphBuilder fg,
        string inputLabel,
        string outputLabel,
        VideoOutputPlan video,
        int sourceWidth,
        int sourceHeight,
        bool sourceIs10Bit
    )
    {
        bool needsTonemap = video.ConvertHdrToSdr && video.TonemapFilterChain is not null;
        bool needsScale = video.Width != sourceWidth || video.Height != sourceHeight;
        bool needs8BitConversion = sourceIs10Bit && !video.TenBit;

        if (!needsTonemap && !needsScale && !needs8BitConversion)
        {
            fg.AddFilter(inputLabel, "copy", outputLabel);
            return;
        }

        // Chain: tonemap → scale → format, using intermediate labels between steps.
        string currentLabel = inputLabel;

        // Step 1: Tonemap (HDR→SDR) — outputs yuv420p, so 8-bit conversion is included
        if (needsTonemap)
        {
            string nextLabel = needsScale ? $"{outputLabel}_tonemapped" : outputLabel;
            fg.AddFilter(currentLabel, video.TonemapFilterChain!, nextLabel);
            currentLabel = nextLabel;

            // Tonemap filter chains (zscale, libplacebo) already output 8-bit yuv420p,
            // so we don't need a separate format conversion after tonemap.
            needs8BitConversion = false;

            if (!needsScale)
                return;
        }

        // Step 2: Scale (resolution change)
        if (needsScale && needs8BitConversion)
        {
            string intermediate = $"{outputLabel}_scaled";
            fg.AddScaleWidth(currentLabel, video.Width, intermediate);
            fg.AddFilter(intermediate, $"format={video.PixelFormat}", outputLabel);
        }
        else if (needsScale)
        {
            fg.AddScaleWidth(currentLabel, video.Width, outputLabel);
        }
        else if (needs8BitConversion)
        {
            fg.AddFilter(currentLabel, $"format={video.PixelFormat}", outputLabel);
        }
    }
}
