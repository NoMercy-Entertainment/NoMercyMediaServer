using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Audio;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Codecs.Definitions;
using NoMercy.Encoder.ContentAnalysis;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Subtitles;

namespace NoMercy.Encoder.Pipeline.Stages;

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
    IAbrLadderGenerator abrLadderGenerator,
    ICropDetector cropDetector,
    ILogger<PlanStage> logger,
    IQualityScalerResolver? qualityScalerResolver = null,
    IHardwarePreferenceResolver? hardwarePreferenceResolver = null,
    SpeedIndex? speedIndex = null,
    IBitDepthPolicyResolver? bitDepthPolicyResolver = null
) : IPipelineStage<ValidateInput, ExecutionPlan>, IPlanStage
{
    public string Name => "Plan";

    public async Task<StageResult> ExecuteAsync(
        ValidateInput input,
        EncodingContext context,
        CancellationToken ct
    )
    {
        logger.LogInformation("[{CorrelationId}] Planning execution", context.CorrelationId);

        try
        {
            EncodingProfile profile = ExpandAutoLadder(input.Profile, input.Media);
            string? cropFilter = await ResolveCropFilterAsync(
                profile,
                input.Media,
                context.CorrelationId,
                ct
            );

            // Resolve codecs honouring profile.HardwarePreference.
            // When HardwarePreferenceResolver is available, it is the single source of
            // truth for encoder selection and emits a decision log entry per output.
            // The GPU session cap (maxHwSessions) is still enforced: outputs that would
            // exceed it are demoted to ForceSoftware regardless of the profile setting.
            int maxHwSessions = hardware.HasGpu ? hardware.Gpus.Min(g => g.MaxEncoderSessions) : 0;
            int hwSessionsUsed = 0;

            ResolvedCodec[] resolvedCodecs;

            if (hardwarePreferenceResolver is not null)
            {
                SpeedIndex effectiveSpeedIndex =
                    speedIndex ?? new SpeedIndex(new Dictionary<SpeedKey, SpeedMeasurement>());

                List<string> availableEncoderNames =
                    ffmpegCapabilities.AvailableEncoders?.ToList() ?? [];

                List<ResolvedCodec> codecList = [];

                foreach (VideoOutput v in profile.VideoOutputs)
                {
                    // Clamp to ForceSoftware when the GPU session cap is exhausted.
                    HardwarePreference effectivePreference =
                        hwSessionsUsed >= maxHwSessions && maxHwSessions > 0
                            ? HardwarePreference.ForceSoftware
                            : profile.HardwarePreference;

                    HardwareResolutionResult resolution = hardwarePreferenceResolver.Resolve(
                        v.Codec,
                        effectivePreference,
                        availableEncoderNames,
                        effectiveSpeedIndex,
                        context.DecisionsOrNoOp
                    );

                    if (resolution.Failure is not null)
                    {
                        return new StageFailure(
                            new(
                                EncodingErrorKind.HardwareUnavailable,
                                resolution.Failure.Shape.Message,
                                null,
                                Name,
                                false
                            )
                        );
                    }

                    // Fall through to ICodecResolver using the preference the resolver chose —
                    // it owns the EncoderInfo/GpuDevice lookup that the rest of the pipeline needs.
                    EncoderPreference legacyPreference = CodecRegistry.IsHardware(
                        resolution.EncoderHandle!
                    )
                        ? EncoderPreference.PreferHardware
                        : EncoderPreference.ForceSoftware;

                    ResolvedCodec resolved = codecResolver.Resolve(
                        v.Codec,
                        hardware,
                        legacyPreference
                    );

                    if (resolved.Device is not null)
                        hwSessionsUsed++;

                    codecList.Add(resolved);
                }

                resolvedCodecs = codecList.ToArray();
            }
            else
            {
                // Legacy path — no resolver injected (e.g. older test suites).
                resolvedCodecs = profile
                    .VideoOutputs.Select(v =>
                    {
                        EncoderPreference preference =
                            hwSessionsUsed < maxHwSessions
                                ? EncoderPreference.PreferHardware
                                : EncoderPreference.ForceSoftware;

                        ResolvedCodec resolved = codecResolver.Resolve(
                            v.Codec,
                            hardware,
                            preference
                        );

                        if (resolved.Device is not null)
                            hwSessionsUsed++;

                        return resolved;
                    })
                    .ToArray();
            }

            List<ExecutionNode> nodes = graphBuilder.BuildGraph(
                input.Media,
                profile,
                resolvedCodecs
            );

            List<ExecutionGroup> groups = groupingStrategy.GroupNodes(nodes, hardware);

            TimeSpan totalEstimate = costEstimator.EstimateTotal(groups, input.Media.Duration);

            OutputPlan outputPlan = BuildOutputPlan(
                profile,
                input.Media,
                resolvedCodecs,
                cropFilter,
                context
            );

            logger.LogInformation(
                "[{CorrelationId}] Plan: {Groups} groups, estimated {Duration}",
                context.CorrelationId,
                groups.Count,
                totalEstimate
            );

            ExecutionPlan plan = new(groups.ToArray(), totalEstimate, outputPlan);
            return new StageSuccess<ExecutionPlan>(plan);
        }
        catch (EncoderRuntimeException rte)
        {
            return new StageFailure(
                new(EncodingErrorKind.HardwareUnavailable, rte.Shape.Message, null, Name, false)
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new StageFailure(
                new(EncodingErrorKind.Unknown, $"Planning failed: {ex.Message}", null, Name, false)
            );
        }
    }

    /// <summary>
    /// When the profile opts into <see cref="EncodingProfile.AutoDetectCrop"/>
    /// and the source has a video stream, run the crop detector and return
    /// a <c>W:H:X:Y</c> string suitable for the <c>crop=</c> filter. Returns
    /// null when the profile disables auto-crop, the source has no video,
    /// or detection concludes the frame is already letterbox-free.
    /// </summary>
    private async Task<string?> ResolveCropFilterAsync(
        EncodingProfile profile,
        MediaInfo media,
        string correlationId,
        CancellationToken ct
    )
    {
        if (!profile.AutoDetectCrop || media.VideoStreams.Count == 0)
            return null;

        try
        {
            CropResult crop = await cropDetector
                .DetectAsync(media.FilePath, ct)
                .ConfigureAwait(false);

            if (!crop.ShouldCrop)
                return null;

            logger.LogInformation(
                "[{CorrelationId}] Crop detected: {W}x{H}+{X}+{Y}",
                correlationId,
                crop.Width,
                crop.Height,
                crop.X,
                crop.Y
            );

            return $"{crop.Width}:{crop.Height}:{crop.X}:{crop.Y}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort: a crop detection failure shouldn't fail the encode.
            // The log surfaces the issue so users can disable AutoDetectCrop if
            // the source consistently trips the detector.
            logger.LogWarning(
                ex,
                "[{CorrelationId}] Crop detection failed — continuing without crop",
                correlationId
            );
            return null;
        }
    }

    private OutputPlan BuildOutputPlan(
        EncodingProfile profile,
        MediaInfo media,
        ResolvedCodec[] resolvedCodecs,
        string? cropFilter,
        EncodingContext context
    )
    {
        // Resolve tonemap strategy once — shared across all video outputs that need HDR→SDR.
        // When HdrPolicy is AlwaysPreserve, skip tonemapping entirely regardless of source.
        bool sourceIsHdr = media.VideoStreams.Count > 0 && media.VideoStreams[0].IsHdr;
        bool tonemapSuppressed = profile.HdrPolicy == HdrPolicy.AlwaysPreserve;

        TonemapStrategy? tonemap =
            sourceIsHdr && !tonemapSuppressed
                ? tonemapSelector.SelectBest(hardware, ffmpegCapabilities)
                : null;

        // Per-profile plan: resolves algorithm + nits + optional LUT from HdrOptions /
        // profile.TonemapAlgorithm with a clear precedence chain.
        TonemapPlan tonemapPlan = tonemapSelector.Build(
            profile.HdrOptions,
            profile.TonemapAlgorithm,
            context.DecisionsOrNoOp
        );

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

                            // Translate the profile CRF to the encoder-native quality value.
                            // The scaler knows each encoder's quirks (CQ range, inverted q:v,
                            // QSV's zero-disables-CRF bug, etc.) so PlanStage stays generic.
                            string encoderHandle = resolved.FfmpegEncoderName;
                            IQualityScaler scaler =
                                qualityScalerResolver?.For(encoderHandle)
                                ?? new LinearQualityScaler();

                            QualityRange nativeRange = encoder.QualityRange;
                            int referenceMax = encoderHandle.Contains(
                                "av1",
                                StringComparison.OrdinalIgnoreCase
                            )
                                ? 63
                                : 51;

                            CodecHint hint = new(encoderHandle, v.Codec);
                            int translatedCrf = scaler.Translate(
                                v.Crf,
                                referenceMax,
                                nativeRange.Max,
                                hint
                            );

                            context.DecisionsOrNoOp.Add(
                                new DecisionLog(
                                    "plan",
                                    "plan.crf_translated",
                                    $"CRF {v.Crf} → {encoderHandle} quality {translatedCrf}",
                                    new
                                    {
                                        handle = encoderHandle,
                                        source = v.Crf,
                                        translated = translatedCrf,
                                    }
                                )
                            );

                            int crf = EncoderArgumentResolver.ResolveQuality(
                                translatedCrf,
                                resolved,
                                extraFlags
                            );

                            // Capped-CRF: when both a CRF target and a bitrate ceiling are set,
                            // emit -maxrate / -bufsize so FFmpeg enforces the cap. Without these
                            // the bitrate value is silently ignored and the encode is pure CRF.
                            // Fix: Phase 3.10 — RateControl.CrfCapped was dashboard-display-only.
                            if (v.Crf > 0 && v.BitrateKbps > 0)
                            {
                                int bufKbps = v.BitrateKbps * 2;
                                extraFlags["-maxrate"] = $"{v.BitrateKbps}k";
                                extraFlags["-bufsize"] = $"{bufKbps}k";
                            }

                            // HDR→HDR passthrough: when source is HDR and the output profile
                            // keeps 10-bit without tonemapping to SDR, preserve color metadata
                            // so players treat the file as HDR. Without these flags the output
                            // is 10-bit bt709 (muddy colors on HDR displays).
                            bool preservesHdr = sourceIsHdr && v.TenBit && !v.ConvertHdrToSdr;
                            if (preservesHdr)
                            {
                                VideoStreamInfo src = media.VideoStreams[0];
                                extraFlags["-color_primaries"] = src.ColorPrimaries ?? "bt2020";
                                extraFlags["-color_trc"] = src.ColorTransfer ?? "smpte2084";
                                extraFlags["-colorspace"] = src.ColorSpace ?? "bt2020nc";
                                extraFlags["-color_range"] = "tv";
                            }

                            // 10-bit / bit-depth policy resolution.
                            // When IBitDepthPolicyResolver is injected (Phase 3.3) it is the
                            // single source of truth and honours profile.BitDepthPolicy.
                            // Without the resolver the legacy inline guard is preserved so
                            // existing tests (TenBitRequested_EncoderLacks10Bit_DowngradedTo8Bit)
                            // continue to pass unchanged.
                            int requestedDepth = v.BitDepth ?? (v.TenBit ? 10 : 8);
                            bool outputTenBit;
                            string outputPixelFormat;
                            string outputEncoderName = resolved.FfmpegEncoderName;

                            if (bitDepthPolicyResolver is not null)
                            {
                                BitDepthResolutionResult bdResult = bitDepthPolicyResolver.Resolve(
                                    requestedDepth,
                                    profile.BitDepthPolicy,
                                    v.Codec,
                                    resolved,
                                    context.DecisionsOrNoOp,
                                    codecType =>
                                        codecResolver.Resolve(
                                            codecType,
                                            hardware,
                                            EncoderPreference.ForceSoftware
                                        )
                                );

                                if (bdResult.Failure is not null)
                                {
                                    // Surface Strict violation as a StageFailure — throw so
                                    // the outer catch re-wraps it, or return early by throwing.
                                    throw bdResult.Failure;
                                }

                                if (bdResult.SwitchedToEncoder is not null)
                                {
                                    // PreferSoftware swapped the encoder — adopt the SW codec.
                                    outputEncoderName = bdResult.SwitchedToEncoder;
                                    resolved = codecResolver.Resolve(
                                        v.Codec,
                                        hardware,
                                        EncoderPreference.ForceSoftware
                                    );
                                    encoder = resolved.EncoderInfo;
                                }

                                if (bdResult.Warning is not null)
                                {
                                    logger.LogWarning(
                                        "Bit-depth policy [{Id}]: {Message}",
                                        bdResult.Warning.Id,
                                        bdResult.Warning.Message
                                    );
                                }

                                outputTenBit = bdResult.FinalBitDepth == 10;
                                outputPixelFormat = bdResult.PixelFormat ?? "yuv420p";
                            }
                            else
                            {
                                // Legacy path — preserve existing behaviour exactly.
                                outputTenBit = v.TenBit && encoder.Supports10Bit;
                                if (v.TenBit && !encoder.Supports10Bit)
                                {
                                    logger.LogWarning(
                                        "Profile requests 10-bit video_{Index} but encoder {Encoder} "
                                            + "does not support 10-bit. Downgrading to 8-bit output.",
                                        i,
                                        encoder.FfmpegName
                                    );
                                }

                                outputPixelFormat = outputTenBit
                                    ? encoder.PixelFormat10Bit
                                    : "yuv420p";
                            }

                            return new VideoOutputPlan(
                                Width: outputWidth,
                                Height: outputHeight,
                                EncoderName: outputEncoderName,
                                Crf: crf,
                                BitrateKbps: v.BitrateKbps,
                                Preset: EncoderArgumentResolver.ResolvePreset(v.Preset, encoder),
                                Profile: EncoderArgumentResolver.ResolveProfile(v.Profile, encoder),
                                Level: v.Level,
                                TenBit: outputTenBit,
                                PixelFormat: outputPixelFormat,
                                MapLabel: $"[v{i}]",
                                ExtraFlags: extraFlags,
                                FrameRate: media.VideoStreams[0].FrameRate,
                                SegmentNameTemplate: v.SegmentNameTemplate,
                                PlaylistNameTemplate: v.PlaylistNameTemplate,
                                ConvertHdrToSdr: v.ConvertHdrToSdr && sourceIsHdr,
                                TonemapFilterChain: v.ConvertHdrToSdr
                                && sourceIsHdr
                                && !tonemapSuppressed
                                    ? tonemapPlan.FilterStringFragment
                                    : null,
                                CropFilter: cropFilter,
                                // Reuses the same predicate as the HDR-passthrough flag
                                // emission above (preservesHdr). Keeping them in sync
                                // means the folder/playlist names match the actual color
                                // metadata embedded in the segments.
                                IsHdrOutput: preservesHdr
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

                string? audioFilter = BuildAudioFilter(
                    audioProfile.Loudness,
                    audioProfile.Downmix,
                    audioProfile.CustomPanMatrix
                );

                audioPlans.Add(
                    new(
                        EncoderName: encoderName,
                        BitrateKbps: audioProfile.BitrateKbps,
                        Channels: audioProfile.Channels,
                        SampleRate: audioProfile.SampleRateHz,
                        Action: StreamAction.Transcode,
                        Language: streamLang,
                        MapLabel: $"0:a:{si}",
                        SegmentNameTemplate: audioProfile.SegmentNameTemplate,
                        PlaylistNameTemplate: audioProfile.PlaylistNameTemplate,
                        AudioFilter: audioFilter
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
                    new(
                        OutputCodec: subProfile.Codec,
                        Action: subProfile.Mode == SubtitleMode.BurnIn
                            ? StreamAction.Transcode
                            : StreamAction.Extract,
                        Language: streamLang,
                        SourceIndex: si,
                        MapLabel: $"0:s:{si}",
                        PlaylistNameTemplate: subProfile.PlaylistNameTemplate,
                        Mode: subProfile.Mode,
                        Variant: SubtitleClassifier.ResolveVariant(stream)
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

            thumbPlan = new(thumbConfig.Width, thumbHeight, thumbConfig.IntervalSeconds);
        }

        // Clamp segment duration to input length for very short files.
        // A 2-second clip with 6-second segments produces malformed HLS playlists.
        int segmentDuration = profile.SegmentDurationSeconds;
        if (media.Duration.TotalSeconds > 0 && media.Duration.TotalSeconds < segmentDuration)
            segmentDuration = Math.Max(1, (int)Math.Ceiling(media.Duration.TotalSeconds));

        // Dolby Vision passthrough gate (Phase 3.5).
        // DolbyVisionGate is the single source of truth for whether the RPU
        // survives. It evaluates codec, bit-depth, container, and HdrPolicy in
        // one place and merges any container-specific extra flags into the first
        // video output's ExtraFlags dictionary.
        //
        // Per-output bit-depth: we evaluate using the first video output because
        // DV RPU is a stream-level property — all outputs either preserve or strip.
        // The gate checks the most-capable output to avoid false strips when a
        // ladder has mixed bit-depths.
        VideoOutput? primaryVideo =
            profile.VideoOutputs.Length > 0 ? profile.VideoOutputs[0] : null;

        int primaryBitDepth = primaryVideo?.BitDepth ?? (primaryVideo?.TenBit == true ? 10 : 8);

        // Codec type lives on the profile VideoOutput — ResolvedCodec only
        // carries the ffmpeg encoder name, not the VideoCodecType enum.
        VideoCodecType primaryCodec = primaryVideo?.Codec ?? VideoCodecType.H264;

        DolbyVisionDecision dvDecision = DolbyVisionGate.Resolve(
            media.DolbyVision,
            primaryCodec,
            primaryBitDepth,
            profile.Format,
            profile.HdrPolicy,
            context.DecisionsOrNoOp,
            profile.HlsOptions
        );

        // Merge DV container flags into the first video output's ExtraFlags.
        // videoPlan is already built at this point so we patch via LINQ index.
        if (dvDecision.Preserved && dvDecision.ExtraFlags.Count > 0 && videoPlan.Length > 0)
        {
            foreach ((string key, string value) in dvDecision.ExtraFlags)
                videoPlan[0].ExtraFlags[key] = value;
        }

        if (media.DolbyVision is not null && !dvDecision.Preserved)
        {
            logger.LogWarning(
                "Source has Dolby Vision (profile {Profile}.{Level}) but DV will be stripped — {Reason}",
                media.DolbyVision.Profile,
                media.DolbyVision.Level,
                dvDecision.Reason
            );
        }

        return new(
            profile.Format,
            videoPlan,
            audioPlan,
            subtitlePlan,
            thumbPlan,
            segmentDuration,
            PreserveDolbyVision: dvDecision.Preserved,
            Drm: profile.Drm
        );
    }

    /// <summary>
    /// When the profile opts into <see cref="EncodingProfile.AutoLadder"/>,
    /// expand the single reference video output into a multi-tier ABR ladder
    /// generated from the source media's resolution + bitrate density.
    /// Passthrough when auto-ladder is off or when the source has no video.
    /// </summary>
    private EncodingProfile ExpandAutoLadder(EncodingProfile profile, MediaInfo media)
    {
        if (!profile.AutoLadder || media.VideoStreams.Count == 0)
            return profile;

        if (profile.VideoOutputs.Length != 1)
        {
            logger.LogWarning(
                "AutoLadder requires exactly one reference VideoOutput; profile has {Count}. "
                    + "Falling back to manual variants.",
                profile.VideoOutputs.Length
            );
            return profile;
        }

        VideoOutput[] ladder = abrLadderGenerator.Generate(media, profile.VideoOutputs[0]);
        if (ladder.Length == 0)
            return profile;

        logger.LogInformation(
            "AutoLadder expanded 1 reference profile → {Count} variants for {Source}",
            ladder.Length,
            media.FilePath
        );
        return profile with { VideoOutputs = ladder };
    }

    /// <summary>
    /// Builds a single FFmpeg audio-filter chain: <c>pan=</c> (when an explicit
    /// downmix matrix is requested) chained with <c>loudnorm</c> (when a
    /// loudness target is requested). Pan runs first because loudnorm expects
    /// the final channel layout. Returns null when neither filter is needed.
    /// </summary>
    internal static string? BuildAudioFilter(
        LoudnessMode loudness,
        DownmixMode downmix,
        string? customPanMatrix
    )
    {
        string? pan = BuildPanFilter(downmix, customPanMatrix);
        string? loudnorm = BuildLoudnormFilter(loudness);

        return (pan, loudnorm) switch
        {
            (null, null) => null,
            (not null, null) => pan,
            (null, not null) => loudnorm,
            _ => $"{pan},{loudnorm}",
        };
    }

    private static string? BuildPanFilter(DownmixMode mode, string? customPanMatrix) =>
        mode switch
        {
            // ITU-R BS.775 5.1 → stereo. Center folded at -3 dB, surrounds at -3 dB.
            DownmixMode.StereoItuR128 =>
                "pan=stereo|FL<FL+0.707*FC+0.707*BL+0.707*SL|FR<FR+0.707*FC+0.707*BR+0.707*SR",
            // Simple equal-weight sum; safe for any input channel layout.
            DownmixMode.Mono => "pan=mono|c0<0.5*FL+0.5*FR+0.5*FC+0.25*BL+0.25*BR+0.25*SL+0.25*SR",
            DownmixMode.Custom => string.IsNullOrWhiteSpace(customPanMatrix)
                ? null
                : $"pan={customPanMatrix}",
            _ => null,
        };

    private static string? BuildLoudnormFilter(LoudnessMode loudness) =>
        loudness switch
        {
            // EBU R128 streaming target: -16 LUFS integrated, -1.5 dBTP true peak, 11 LU LRA.
            LoudnessMode.EbuR128 => "loudnorm=I=-16:TP=-1.5:LRA=11",
            // ReplayGain target: -18 LUFS integrated, same peak + range as R128.
            LoudnessMode.ReplayGain => "loudnorm=I=-18:TP=-1.5:LRA=11",
            // Custom loudnorm left to CustomArguments on the profile; no auto filter here.
            LoudnessMode.Custom => null,
            _ => null,
        };
}
