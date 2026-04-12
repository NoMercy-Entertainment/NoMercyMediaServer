namespace NoMercy.Encoder.V3.Pipeline.Stages;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.V3.Analysis;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Codecs.Definitions;
using NoMercy.Encoder.V3.Errors;
using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.Output;
using NoMercy.Encoder.V3.Pipeline.Optimizer;
using NoMercy.Encoder.V3.Profiles;

public record ExecutionPlan(
    ExecutionGroup[] Groups,
    TimeSpan EstimatedTotalDuration,
    OutputPlan OutputPlan
);

public class PlanStage(
    ExecutionGraphBuilder graphBuilder,
    GroupingStrategy groupingStrategy,
    CostEstimator costEstimator,
    ICodecResolver codecResolver,
    IHardwareCapabilities hardware,
    ILogger<PlanStage> logger
) : IPipelineStage<ValidateInput, ExecutionPlan>
{
    public string Name => "Plan";

    public Task<StageResult> ExecuteAsync(
        ValidateInput input,
        EncodingContext context,
        CancellationToken ct
    )
    {
        logger.LogInformation("[{CorrelationId}] Planning execution", context.CorrelationId);

        try
        {
            ResolvedCodec[] resolvedCodecs = input
                .Profile.VideoOutputs.Select(v => codecResolver.Resolve(v.Codec, hardware))
                .ToArray();

            List<ExecutionNode> nodes = graphBuilder.BuildGraph(
                input.Media,
                input.Profile,
                resolvedCodecs
            );

            List<ExecutionGroup> groups = groupingStrategy.GroupNodes(nodes, hardware);

            TimeSpan totalEstimate = costEstimator.EstimateTotal(groups, input.Media.Duration);

            OutputPlan outputPlan = BuildOutputPlan(input.Profile, input.Media, resolvedCodecs);

            logger.LogInformation(
                "[{CorrelationId}] Plan: {Groups} groups, estimated {Duration}",
                context.CorrelationId,
                groups.Count,
                totalEstimate
            );

            ExecutionPlan plan = new(groups.ToArray(), totalEstimate, outputPlan);
            return Task.FromResult<StageResult>(new StageSuccess<ExecutionPlan>(plan));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult<StageResult>(
                new StageFailure(
                    new EncodingError(
                        EncodingErrorKind.Unknown,
                        $"Planning failed: {ex.Message}",
                        null,
                        Name,
                        false
                    )
                )
            );
        }
    }

    private static OutputPlan BuildOutputPlan(
        EncodingProfile profile,
        MediaInfo media,
        ResolvedCodec[] resolvedCodecs
    )
    {
        VideoOutputPlan[] videoPlan = profile
            .VideoOutputs.Select(
                (v, i) =>
                {
                    int height =
                        v.Height
                        ?? (v.Width * media.VideoStreams[0].Height / media.VideoStreams[0].Width);
                    ResolvedCodec resolved = resolvedCodecs[i];
                    return new VideoOutputPlan(
                        Width: v.Width,
                        Height: height,
                        EncoderName: resolved.FfmpegEncoderName,
                        Crf: v.Crf,
                        BitrateKbps: v.BitrateKbps,
                        Preset: v.Preset,
                        Profile: v.Profile,
                        Level: v.Level,
                        TenBit: v.TenBit,
                        PixelFormat: v.TenBit ? resolved.EncoderInfo.PixelFormat10Bit : "yuv420p",
                        MapLabel: $"[v{i}]",
                        ExtraFlags: new Dictionary<string, string>(
                            resolved.EncoderInfo.VendorSpecificFlags
                        )
                    );
                }
            )
            .ToArray();

        AudioOutputPlan[] audioPlan = profile
            .AudioOutputs.Select(
                (a, i) =>
                {
                    string encoderName = AudioCodecDefinitions.GetEncoder(a.Codec).FfmpegName;
                    return new AudioOutputPlan(
                        EncoderName: encoderName,
                        BitrateKbps: a.BitrateKbps,
                        Channels: a.Channels,
                        SampleRate: a.SampleRateHz,
                        Action: StreamAction.Transcode,
                        Language: a.AllowedLanguages.Length > 0 ? a.AllowedLanguages[0] : null,
                        MapLabel: $"0:a:{i}"
                    );
                }
            )
            .ToArray();

        SubtitleOutputPlan[] subtitlePlan = profile
            .SubtitleOutputs.Select(
                (s, i) =>
                    new SubtitleOutputPlan(
                        OutputCodec: s.Codec,
                        Action: s.Mode == SubtitleMode.BurnIn
                            ? StreamAction.Transcode
                            : StreamAction.Extract,
                        Language: s.AllowedLanguages.Length > 0 ? s.AllowedLanguages[0] : null,
                        SourceIndex: i,
                        MapLabel: $"0:s:{i}"
                    )
            )
            .ToArray();

        ThumbnailOutputPlan? thumbPlan = profile.Thumbnails is not null
            ? new ThumbnailOutputPlan(profile.Thumbnails.Width, profile.Thumbnails.IntervalSeconds)
            : null;

        return new OutputPlan(profile.Format, videoPlan, audioPlan, subtitlePlan, thumbPlan);
    }
}
