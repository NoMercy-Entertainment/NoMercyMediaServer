namespace NoMercy.Encoder.Pipeline.Stages;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Codecs.Definitions;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Profiles;

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
                    ResolvedCodec resolved = resolvedCodecs[i];
                    EncoderInfo encoder = resolved.EncoderInfo;

                    (int outputWidth, int outputHeight) = EncoderArgumentResolver.ResolveDimensions(
                        v,
                        media.VideoStreams[0].Width,
                        media.VideoStreams[0].Height
                    );

                    Dictionary<string, string> extraFlags = new(encoder.VendorSpecificFlags);
                    int crf = EncoderArgumentResolver.ResolveQuality(v.Crf, resolved, extraFlags);

                    return new VideoOutputPlan(
                        Width: outputWidth,
                        Height: outputHeight,
                        EncoderName: resolved.FfmpegEncoderName,
                        Crf: crf,
                        BitrateKbps: v.BitrateKbps,
                        Preset: EncoderArgumentResolver.ResolvePreset(v.Preset, encoder),
                        Profile: EncoderArgumentResolver.ResolveProfile(v.Profile, encoder),
                        Level: v.Level,
                        TenBit: v.TenBit,
                        PixelFormat: v.TenBit ? encoder.PixelFormat10Bit : "yuv420p",
                        MapLabel: $"[v{i}]",
                        ExtraFlags: extraFlags
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
