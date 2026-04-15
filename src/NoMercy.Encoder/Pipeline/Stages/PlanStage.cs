namespace NoMercy.Encoder.Pipeline.Stages;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Codecs.Definitions;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
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
    ITonemapSelector tonemapSelector,
    IFfmpegCapabilities ffmpegCapabilities,
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
            // Resolve codecs with hardware session awareness — once we've filled
            // all GPU sessions, overflow outputs fall back to software encoding.
            int maxHwSessions = hardware.HasGpu ? hardware.Gpus.Min(g => g.MaxEncoderSessions) : 0;
            int hwSessionsUsed = 0;

            ResolvedCodec[] resolvedCodecs = input
                .Profile.VideoOutputs.Select(v =>
                {
                    EncoderPreference preference =
                        hwSessionsUsed < maxHwSessions
                            ? EncoderPreference.PreferHardware
                            : EncoderPreference.ForceSoftware;

                    ResolvedCodec resolved = codecResolver.Resolve(v.Codec, hardware, preference);

                    if (resolved.Device is not null)
                        hwSessionsUsed++;

                    return resolved;
                })
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

    private OutputPlan BuildOutputPlan(
        EncodingProfile profile,
        MediaInfo media,
        ResolvedCodec[] resolvedCodecs
    )
    {
        // Resolve tonemap strategy once — shared across all video outputs that need HDR→SDR
        bool sourceIsHdr = media.VideoStreams.Count > 0 && media.VideoStreams[0].IsHdr;
        TonemapStrategy? tonemap = sourceIsHdr
            ? tonemapSelector.SelectBest(hardware, ffmpegCapabilities)
            : null;

        // Audio-only: skip video planning entirely when source has no video streams
        VideoOutputPlan[] videoPlan =
            media.VideoStreams.Count > 0 && profile.VideoOutputs.Length > 0
                ? profile
                    .VideoOutputs.Select(
                        (v, i) =>
                        {
                            ResolvedCodec resolved = resolvedCodecs[i];
                            EncoderInfo encoder = resolved.EncoderInfo;

                            (int outputWidth, int outputHeight) =
                                EncoderArgumentResolver.ResolveDimensions(
                                    v,
                                    media.VideoStreams[0].Width,
                                    media.VideoStreams[0].Height
                                );

                            Dictionary<string, string> extraFlags = new(
                                encoder.VendorSpecificFlags
                            );
                            int crf = EncoderArgumentResolver.ResolveQuality(
                                v.Crf,
                                resolved,
                                extraFlags
                            );

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
                                ExtraFlags: extraFlags,
                                FrameRate: media.VideoStreams[0].FrameRate,
                                SegmentNameTemplate: v.SegmentNameTemplate,
                                PlaylistNameTemplate: v.PlaylistNameTemplate,
                                ConvertHdrToSdr: v.ConvertHdrToSdr && sourceIsHdr,
                                TonemapFilterChain: v.ConvertHdrToSdr && tonemap is not null
                                    ? tonemap.FfmpegFilterChain
                                    : null
                            );
                        }
                    )
                    .ToArray()
                : [];

        // Build one AudioOutputPlan per matching source stream.
        // AllowedLanguages is a FILTER — the actual language comes from the source stream.
        // When AllowedLanguages is empty or contains all languages, include every stream.
        List<AudioOutputPlan> audioPlans = [];
        foreach (AudioOutput audioProfile in profile.AudioOutputs)
        {
            string encoderName = AudioCodecDefinitions.GetEncoder(audioProfile.Codec).FfmpegName;
            HashSet<string> allowed =
                audioProfile.AllowedLanguages.Length > 0
                    ? new HashSet<string>(
                        audioProfile.AllowedLanguages,
                        StringComparer.OrdinalIgnoreCase
                    )
                    : [];

            for (int si = 0; si < media.AudioStreams.Count; si++)
            {
                AudioStreamInfo stream = media.AudioStreams[si];
                string streamLang = stream.Language ?? "und";

                // If AllowedLanguages is empty, include all streams.
                // Otherwise only include streams whose language is in the filter list.
                if (allowed.Count > 0 && !allowed.Contains(streamLang))
                    continue;

                audioPlans.Add(
                    new AudioOutputPlan(
                        EncoderName: encoderName,
                        BitrateKbps: audioProfile.BitrateKbps,
                        Channels: audioProfile.Channels,
                        SampleRate: audioProfile.SampleRateHz,
                        Action: StreamAction.Transcode,
                        Language: streamLang,
                        MapLabel: $"0:a:{si}",
                        SegmentNameTemplate: audioProfile.SegmentNameTemplate,
                        PlaylistNameTemplate: audioProfile.PlaylistNameTemplate
                    )
                );
            }
        }

        AudioOutputPlan[] audioPlan = audioPlans.ToArray();

        // Build one SubtitleOutputPlan per matching source stream.
        // AllowedLanguages is a filter — language comes from the source stream.
        // Deduplicate by source stream index — first matching profile wins.
        List<SubtitleOutputPlan> subtitlePlans = [];
        HashSet<int> claimedStreams = [];
        foreach (SubtitleOutput subProfile in profile.SubtitleOutputs)
        {
            HashSet<string> allowed =
                subProfile.AllowedLanguages.Length > 0
                    ? new HashSet<string>(
                        subProfile.AllowedLanguages,
                        StringComparer.OrdinalIgnoreCase
                    )
                    : [];

            for (int si = 0; si < media.SubtitleStreams.Count; si++)
            {
                if (claimedStreams.Contains(si))
                    continue;

                SubtitleStreamInfo stream = media.SubtitleStreams[si];
                string streamLang = stream.Language ?? "und";

                if (allowed.Count > 0 && !allowed.Contains(streamLang))
                    continue;

                claimedStreams.Add(si);
                subtitlePlans.Add(
                    new SubtitleOutputPlan(
                        OutputCodec: subProfile.Codec,
                        Action: subProfile.Mode == SubtitleMode.BurnIn
                            ? StreamAction.Transcode
                            : StreamAction.Extract,
                        Language: streamLang,
                        SourceIndex: si,
                        MapLabel: $"0:s:{si}",
                        PlaylistNameTemplate: subProfile.PlaylistNameTemplate
                    )
                );
            }
        }

        SubtitleOutputPlan[] subtitlePlan = subtitlePlans.ToArray();

        ThumbnailOutputPlan? thumbPlan = null;
        if (profile.Thumbnails is not null && media.VideoStreams.Count > 0)
        {
            ThumbnailOutput thumbConfig = profile.Thumbnails;
            // Calculate the actual thumbnail height from source aspect ratio.
            // FFmpeg scale=W:-2 produces this height (rounded to even).
            int thumbHeight = (int)(
                2
                * Math.Round(
                    (double)thumbConfig.Width
                        * media.VideoStreams[0].Height
                        / media.VideoStreams[0].Width
                        / 2
                )
            );

            thumbPlan = new ThumbnailOutputPlan(
                thumbConfig.Width,
                thumbHeight,
                thumbConfig.IntervalSeconds
            );
        }

        // Clamp segment duration to input length for very short files.
        // A 2-second clip with 6-second segments produces malformed HLS playlists.
        int segmentDuration = profile.SegmentDurationSeconds;
        if (media.Duration.TotalSeconds > 0 && media.Duration.TotalSeconds < segmentDuration)
            segmentDuration = Math.Max(1, (int)Math.Ceiling(media.Duration.TotalSeconds));

        return new OutputPlan(
            profile.Format,
            videoPlan,
            audioPlan,
            subtitlePlan,
            thumbPlan,
            segmentDuration
        );
    }
}
