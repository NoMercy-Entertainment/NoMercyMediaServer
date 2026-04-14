namespace NoMercy.Encoder.Pipeline.Stages;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.PostProcess;

public record BuildInput(ExecutionPlan Plan, string InputPath, string OutputDirectory);

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

            FfmpegCommandBuilder builder = new();
            builder.AddInput(new InputOptions(input.InputPath));

            string? filterGraph = BuildFilterGraph(input.Plan.OutputPlan, context.MediaInfo);
            if (filterGraph is not null)
                builder.WithFilterComplex(filterGraph);

            strategy.ConfigureOutput(builder, input.Plan.OutputPlan, input.OutputDirectory);

            FfmpegCommand mainCommand = builder.Build(
                options.FfmpegPathOverride,
                input.OutputDirectory
            );

            logger.LogDebug(
                "[{CorrelationId}] Built main command: {Args}",
                context.CorrelationId,
                string.Join(" ", mainCommand.Arguments)
            );

            List<FfmpegCommand> allCommands = [mainCommand];

            // Subtitle extraction commands — one command per subtitle stream
            if (input.Plan.OutputPlan.SubtitleOutputs.Length > 0 && context.MediaInfo is not null)
            {
                SubtitleExtractor subtitleExtractor = new();
                FfmpegCommand[] subCommands = subtitleExtractor.BuildExtractionCommands(
                    options.FfmpegPathOverride,
                    input.InputPath,
                    input.OutputDirectory,
                    context.MediaInfo.SubtitleStreams,
                    input.Plan.OutputPlan.SubtitleOutputs
                );

                allCommands.AddRange(subCommands);

                logger.LogDebug(
                    "[{CorrelationId}] Added {Count} subtitle extraction command(s)",
                    context.CorrelationId,
                    subCommands.Length
                );
            }

            // Font extraction command — always attempt for MKV containers
            FontExtractor fontExtractor = new();
            string fontDir = Path.Combine(input.OutputDirectory, "fonts");
            Directory.CreateDirectory(fontDir);
            FfmpegCommand fontCommand = fontExtractor.BuildExtractionCommand(
                options.FfmpegPathOverride,
                input.InputPath,
                input.OutputDirectory
            );
            allCommands.Add(fontCommand);

            logger.LogDebug(
                "[{CorrelationId}] Added font extraction command",
                context.CorrelationId
            );

            // Thumbnail capture command
            if (input.Plan.OutputPlan.Thumbnails is not null && context.MediaInfo is not null)
            {
                ThumbnailGenerator thumbGen = new();
                string thumbDir = Path.Combine(
                    input.OutputDirectory,
                    $"thumbs_{input.Plan.OutputPlan.Thumbnails.Width}"
                );
                Directory.CreateDirectory(thumbDir);

                FfmpegCommand thumbCommand = thumbGen.BuildCaptureCommand(
                    options.FfmpegPathOverride,
                    input.InputPath,
                    input.OutputDirectory,
                    input.Plan.OutputPlan.Thumbnails,
                    context.MediaInfo.Duration
                );
                allCommands.Add(thumbCommand);

                logger.LogDebug(
                    "[{CorrelationId}] Added thumbnail capture command",
                    context.CorrelationId
                );
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

        FilterGraphBuilder fg = new();

        if (videoOutputs.Length == 1)
        {
            VideoOutputPlan single = videoOutputs[0];
            // Strip the surrounding brackets to get the raw label name
            string outputLabel = single.MapLabel.Trim('[', ']');

            if (single.Width == sourceWidth && single.Height == sourceHeight)
                fg.AddFilter("0:v:0", "copy", outputLabel);
            else
                fg.AddScaleWidth("0:v:0", single.Width, outputLabel);
        }
        else
        {
            // Multi-output: split first, then scale/copy each branch
            string[] splitLabels = videoOutputs.Select((_, i) => $"split{i}").ToArray();

            fg.AddSplit("0:v:0", splitLabels);

            for (int i = 0; i < videoOutputs.Length; i++)
            {
                VideoOutputPlan video = videoOutputs[i];
                string outputLabel = video.MapLabel.Trim('[', ']');

                if (video.Width == sourceWidth && video.Height == sourceHeight)
                    fg.AddFilter(splitLabels[i], "copy", outputLabel);
                else
                    fg.AddScaleWidth(splitLabels[i], video.Width, outputLabel);
            }
        }

        return fg.HasFilters ? fg.Build() : null;
    }
}
