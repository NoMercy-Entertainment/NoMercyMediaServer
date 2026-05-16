using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Codecs.Definitions;
using NoMercy.Encoder.ContentAnalysis;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Subtitles;
using LegacyDrmConfig = NoMercy.Encoder.BuildingBlocks.Drm.DrmConfig;
using LegacyDrmMethod = NoMercy.Encoder.BuildingBlocks.Drm.DrmMethod;

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
    IBitDepthPolicyResolver? bitDepthPolicyResolver = null,
    IOutputNamingResolver? outputNamingResolver = null,
    ISubtitleAcquisitionService? subtitleAcquisitionService = null
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

            VideoOutput[] videoOutputs = PlanStageHelpers.EnumerateVideo(profile);

            // EmitHdrAndSdr: when source is HDR, emit the HDR passthrough copy
            // (ConvertHdrToSdr=false) AND a tonemapped SDR companion
            // (ConvertHdrToSdr=true) per resolution. Template label suffix
            // distinguishes the two on disk (video_WxH/ vs video_WxH_SDR/).
            //
            // 10-bit-only gate: HDR PQ / HLG requires 10-bit. A rung that
            // declares 8-bit (e.g. H.264 NVENC, which doesn't support 10-bit
            // reliably) cannot preserve HDR even when we ask it to — both the
            // "passthrough" and "tonemap" copy would end up SDR and collide on
            // the same _SDR filename. So 8-bit rungs only emit the tonemap
            // copy; 10-bit rungs emit both.
            //
            // SDR source: behaves like AlwaysTonemap, no doubling.
            if (
                profile.HdrPolicy == HdrPolicy.EmitHdrAndSdr
                && input.Media.VideoStreams.Count > 0
                && input.Media.VideoStreams[0].IsHdr
            )
            {
                videoOutputs = videoOutputs
                    .SelectMany(v =>
                        v.BitDepth >= 10
                            ? new[]
                            {
                                v with
                                {
                                    ConvertHdrToSdr = false,
                                },
                                v with
                                {
                                    ConvertHdrToSdr = true,
                                },
                            }
                            : new[] { v with { ConvertHdrToSdr = true } }
                    )
                    .ToArray();
            }

            ResolvedCodec[] resolvedCodecs;

            if (hardwarePreferenceResolver is not null)
            {
                SpeedIndex effectiveSpeedIndex =
                    speedIndex ?? new SpeedIndex(new Dictionary<SpeedKey, SpeedMeasurement>());

                List<string> availableEncoderNames =
                    ffmpegCapabilities.AvailableEncoders?.ToList() ?? [];

                List<ResolvedCodec> codecList = [];

                foreach (VideoOutput v in videoOutputs)
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

                    // The HW preference resolver already picked an encoder by name from
                    // the SpeedIndex (which is the authoritative source of "this works
                    // on this host"). Look up the EncoderInfo directly instead of
                    // re-resolving via preference — the legacy CodecResolver path gates
                    // on IHardwareCapabilities.HasGpu, which silently downgrades to
                    // software when GPU detection is stale even though SpeedIndex has
                    // hevc_nvenc benchmarks for it.
                    ResolvedCodec resolved = codecResolver.ResolveByEncoderName(
                        v.Codec,
                        resolution.EncoderHandle!,
                        hardware
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
                resolvedCodecs = videoOutputs
                    .Select(v =>
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

            IReadOnlyList<AcquiredSubtitle> acquiredSubtitles = await AcquireSubtitlesAsync(
                    profile,
                    input.Media,
                    ct
                )
                .ConfigureAwait(false);

            outputPlan = outputPlan with { AcquiredSubtitles = acquiredSubtitles };

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

    private async Task<IReadOnlyList<AcquiredSubtitle>> AcquireSubtitlesAsync(
        EncodingProfile profile,
        MediaInfo media,
        CancellationToken ct
    )
    {
        SubtitleAcquisitionConfig? acq = profile.SubtitleAcquisition;
        if (subtitleAcquisitionService is null || acq is null || !acq.Enabled)
            return [];

        try
        {
            string[] alreadyPresent = media
                .SubtitleStreams.Select(s => s.Language ?? "und")
                .ToArray();

            AcquisitionRequest request = new(
                SourcePath: media.FilePath,
                SourceFileSize: media.FileSizeBytes,
                SourceFilename: System.IO.Path.GetFileName(media.FilePath),
                MediaTitle: System.IO.Path.GetFileNameWithoutExtension(media.FilePath),
                Season: null,
                Episode: null,
                Year: null,
                SourceFps: media.VideoStreams.Count > 0 ? media.VideoStreams[0].FrameRate : null,
                SourceDuration: media.Duration,
                LanguagesAlreadyInSource: alreadyPresent,
                Config: acq
            );

            return await subtitleAcquisitionService.AcquireAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "[{CorrelationId}] Subtitle acquisition failed — continuing without acquired subs",
                "unknown"
            );
            return [];
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
        OutputFormat outputFormat = PlanStageHelpers.ContainerToOutputFormat(profile.Container);
        bool hlsUsesFmp4Segments = profile.Container is Container.HlsFmp4 or Container.AudioHlsFmp4;

        // Resolve tonemap strategy once — shared across all video outputs that need HDR→SDR.
        // When HdrPolicy is AlwaysPreserve, skip tonemapping entirely regardless of source.
        bool sourceIsHdr = media.VideoStreams.Count > 0 && media.VideoStreams[0].IsHdr;
        bool tonemapSuppressed = profile.HdrPolicy == HdrPolicy.AlwaysPreserve;

        TonemapStrategy? tonemap =
            sourceIsHdr && !tonemapSuppressed
                ? tonemapSelector.SelectBest(hardware, ffmpegCapabilities)
                : null;

        // Per-profile plan: resolves algorithm + nits + optional LUT from HdrOptions.
        // V2 profile has no TonemapAlgorithm shorthand — pass null; HdrOptions takes over.
        TonemapPlan tonemapPlan = tonemapSelector.Build(
            profile.HdrOptions,
            null,
            context.DecisionsOrNoOp
        );

        VideoOutput[] videoOutputs = PlanStageHelpers.EnumerateVideo(profile);

        // EmitHdrAndSdr: same expansion as in the resolver block above so
        // VideoOutputPlan count matches resolvedCodecs count. Keep the 10-bit
        // gate identical — 8-bit rungs only emit the tonemap copy.
        if (
            profile.HdrPolicy == HdrPolicy.EmitHdrAndSdr
            && media.VideoStreams.Count > 0
            && media.VideoStreams[0].IsHdr
        )
        {
            videoOutputs = videoOutputs
                .SelectMany(v =>
                    v.BitDepth >= 10
                        ? new[]
                        {
                            v with
                            {
                                ConvertHdrToSdr = false,
                            },
                            v with
                            {
                                ConvertHdrToSdr = true,
                            },
                        }
                        : new[] { v with { ConvertHdrToSdr = true } }
                )
                .ToArray();
        }

        // Audio-only: skip video planning entirely when source has no video streams
        VideoOutputPlan[] videoPlan =
            media.VideoStreams.Count > 0 && videoOutputs.Length > 0
                ? videoOutputs
                    .Select(
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
                            // emit -maxrate / -bufsize so FFmpeg enforces the cap.
                            if (v.Crf > 0 && v.BitrateKbps > 0)
                            {
                                int bufKbps = v.BitrateKbps * 2;
                                extraFlags["-maxrate"] = $"{v.BitrateKbps}k";
                                extraFlags["-bufsize"] = $"{bufKbps}k";
                            }

                            // HDR→HDR passthrough: preserve color metadata when source is HDR
                            // and the output keeps 10-bit without tonemapping.
                            bool preservesHdr =
                                sourceIsHdr && v.BitDepth >= 10 && !v.ConvertHdrToSdr;
                            if (preservesHdr)
                            {
                                VideoStreamInfo src = media.VideoStreams[0];
                                extraFlags["-color_primaries"] = src.ColorPrimaries ?? "bt2020";
                                extraFlags["-color_trc"] = src.ColorTransfer ?? "smpte2084";
                                extraFlags["-colorspace"] = src.ColorSpace ?? "bt2020nc";
                                extraFlags["-color_range"] = "tv";
                            }

                            // 10-bit / bit-depth policy resolution.
                            int requestedDepth = v.BitDepth;
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
                                outputTenBit = requestedDepth >= 10 && encoder.Supports10Bit;
                                if (requestedDepth >= 10 && !encoder.Supports10Bit)
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

                            string? codecProfileString =
                                v.CodecProfile == CodecProfile.Auto
                                    ? null
                                    : v.CodecProfile.ToString().ToLowerInvariant();

                            return new VideoOutputPlan(
                                Width: outputWidth,
                                Height: outputHeight,
                                EncoderName: outputEncoderName,
                                Crf: crf,
                                BitrateKbps: v.BitrateKbps,
                                Preset: EncoderArgumentResolver.ResolvePreset(v.Preset, encoder),
                                Profile: EncoderArgumentResolver.ResolveProfile(
                                    codecProfileString,
                                    encoder
                                ),
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
                                IsHdrOutput: preservesHdr
                            );
                        }
                    )
                    .ToArray()
                : [];

        // Disambiguate any video plans whose templates would resolve to the
        // same on-disk path. The HdrPolicy.EmitHdrAndSdr expansion plus
        // H264FallbackHeights can produce two 1080p rungs (HEVC tonemap + H.264
        // fallback) that both label as "video_1920x1080_SDR" via TemplateResolver.
        // Append a codec-family token to colliding entries so they land in
        // distinct directories (video_1920x1080_SDR_avc/ vs _hevc/).
        videoPlan = DisambiguateVideo(videoPlan);

        // Build one AudioOutputPlan per matching source stream.
        // AllowedLanguages is a FILTER — the actual language comes from the source stream.
        List<AudioOutputPlan> audioPlans = [];
        foreach (AudioOutput audioProfile in profile.Audio)
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

                if (allowed.Count > 0 && !allowed.Contains(streamLang))
                    continue;

                LoudnessMode loudnessMode = audioProfile.Loudness?.Mode ?? LoudnessMode.None;
                DownmixMode downmixMode = audioProfile.Downmix?.Mode ?? DownmixMode.Auto;
                string? customPanMatrix = audioProfile.Downmix?.CustomPanMatrix;

                string? audioFilter = BuildAudioFilter(loudnessMode, downmixMode, customPanMatrix);

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

        // Disambiguate any audio plans whose templates would resolve to the
        // same on-disk path. The default audio template is
        // "audio_{lang}_{codec}/audio_{lang}_{codec}", so three English AAC
        // streams all collapse to the same directory + filename. Append the
        // source stream index to colliding plans only — single-stream-per-
        // language sources keep their stable templates.
        AudioOutputPlan[] audioPlan = DisambiguateAudio(audioPlans).ToArray();

        // Build one SubtitleOutputPlan per matching source stream.
        // Deduplicate by source stream index — first matching profile wins.
        List<SubtitleOutputPlan> subtitlePlans = [];
        HashSet<int> claimedStreams = [];
        foreach (SubtitleOutput subProfile in profile.Subtitles)
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
                        Action: subProfile.Policy == SubtitlePolicy.BurnIn
                            ? StreamAction.Transcode
                            : StreamAction.Extract,
                        Language: streamLang,
                        SourceIndex: si,
                        MapLabel: $"0:s:{si}",
                        PlaylistNameTemplate: subProfile.PlaylistNameTemplate,
                        Policy: subProfile.Policy,
                        Variant: SubtitleClassifier.ResolveVariant(stream)
                    )
                );
            }
        }

        SubtitleOutputPlan[] subtitlePlan = subtitlePlans.ToArray();

        ThumbnailOutputPlan? thumbPlan = null;
        if (media.VideoStreams.Count > 0)
        {
            // Explicit profile.Thumbnails wins. Otherwise fall back to
            // HlsDerivatives: when GenerateSpriteVtt is on (the default for HLS),
            // build a ThumbnailOutput from SpriteVttThumbnailWidth +
            // SpriteVttIntervalSeconds so sprites land for every HLS preset
            // without each author having to also set the legacy Thumbnails field.
            // Null HlsDerivatives (V1-backfilled presets) gets treated as a
            // fresh HlsDerivatives() so the sprite-on defaults still apply.
            ThumbnailOutput? thumbConfig = profile.Thumbnails;
            if (thumbConfig is null)
            {
                HlsDerivatives derivatives = profile.HlsDerivatives ?? new HlsDerivatives();
                if (derivatives.GenerateSpriteVtt)
                {
                    thumbConfig = new ThumbnailOutput(
                        Width: derivatives.SpriteVttThumbnailWidth,
                        IntervalSeconds: derivatives.SpriteVttIntervalSeconds
                    );
                }
            }

            if (thumbConfig is not null)
            {
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
        }

        // Clamp segment duration to input length for very short files.
        int segmentDuration = profile.SegmentDurationSeconds;
        if (media.Duration.TotalSeconds > 0 && media.Duration.TotalSeconds < segmentDuration)
            segmentDuration = Math.Max(1, (int)Math.Ceiling(media.Duration.TotalSeconds));

        // Dolby Vision passthrough gate.
        // Per-output bit-depth: evaluate using the first video output because
        // DV RPU is a stream-level property.
        VideoOutput? primaryVideo = videoOutputs.Length > 0 ? videoOutputs[0] : null;
        int primaryBitDepth = primaryVideo?.BitDepth ?? 8;
        VideoCodecType primaryCodec = primaryVideo?.Codec ?? VideoCodecType.H264;

        DolbyVisionDecision dvDecision = DolbyVisionGate.Resolve(
            media.DolbyVision,
            primaryCodec,
            primaryBitDepth,
            outputFormat,
            profile.HdrPolicy,
            context.DecisionsOrNoOp,
            hlsUsesFmp4Segments
        );

        // Merge DV container flags into the first video output's ExtraFlags.
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

        // 3D stereo_mode preservation: when the source has a stereo_mode tag and
        // the profile stream-copies video, forward the tag so the muxer carries it
        // to the output. Transcode paths cannot preserve it (rejected by validator).
        bool videoIsCopy =
            profile.Video is { Policy: StreamPolicy.Copy } || videoOutputs.Length == 0;

        if (media.StereoMode is not null && videoIsCopy && videoPlan.Length > 0)
        {
            // MKV: -metadata:s:v stereo_mode=<value> tags the video track.
            // MP4: stream-copy keeps the st3d box automatically when -c:v copy is
            //      used; the extra tag does not hurt non-MKV containers.
            videoPlan[0].ExtraFlags["-metadata:s:v stereo_mode"] = media.StereoMode;
        }

        // VR spherical projection preservation: pass-through the sv3d/proj box
        // metadata on stream-copy. -movflags +write_colr ensures the MP4 muxer
        // emits colour information that VR players expect alongside sv3d.
        if (media.SphericalProjection is not null && videoIsCopy && videoPlan.Length > 0)
        {
            if (outputFormat is OutputFormat.Mp4 or OutputFormat.Hls or OutputFormat.Dash)
            {
                videoPlan[0].ExtraFlags["-movflags"] = "+write_colr";
            }

            logger.LogInformation(
                "[{CorrelationId}] VR source (projection={Projection}): stream-copy preserves spherical metadata",
                context.CorrelationId,
                media.SphericalProjection
            );
        }

        // Resolve HLS muxer options from V2 HlsConfig + Container. Strategies
        // need flat strings ("mpegts" | "fmp4", "vod" | "event") so we materialise
        // them here once instead of teaching every strategy how to read V2.
        HlsPlanOptions? hlsOptions = profile.Container
            is Container.HlsTs
                or Container.HlsFmp4
                or Container.AudioHlsTs
                or Container.AudioHlsFmp4
            ? new HlsPlanOptions(
                SegmentType: hlsUsesFmp4Segments ? "fmp4" : "mpegts",
                PlaylistType: profile.Hls?.PlaylistType == HlsPlaylistType.Event ? "event" : "vod",
                IndependentSegments: profile.Hls?.IndependentSegments ?? true
            )
            : null;

        BundleLayout? layout =
            outputNamingResolver is not null && context.MediaItem is not null
                ? outputNamingResolver.Resolve(context.MediaItem, profile)
                : null;

        bool generateChapterThumbs = (
            profile.HlsDerivatives ?? new HlsDerivatives()
        ).GenerateChapterThumbs;

        return new(
            outputFormat,
            videoPlan,
            audioPlan,
            subtitlePlan,
            thumbPlan,
            segmentDuration,
            PreserveDolbyVision: dvDecision.Preserved,
            Drm: ConvertDrmConfig(profile.Drm),
            HlsOptions: hlsOptions,
            Layout: layout,
            GenerateChapterThumbs: generateChapterThumbs
        );
    }

    /// <summary>
    /// When the profile opts into <see cref="LadderMode.Auto"/>, expand the
    /// single reference video output into a multi-tier ABR ladder generated
    /// from the source media's resolution + bitrate density. The generated
    /// outputs are stored as Manual rungs so <see cref="PlanStageHelpers.EnumerateVideo"/>
    /// materialises them correctly on subsequent passes.
    /// Passthrough when auto-ladder is off or when the source has no video.
    /// </summary>
    private EncodingProfile ExpandAutoLadder(EncodingProfile profile, MediaInfo media)
    {
        if (profile.Ladder?.Mode != LadderMode.Auto || media.VideoStreams.Count == 0)
            return profile;

        LadderRung[] existingRungs = profile.Ladder.Rungs ?? [];

        // Auto + multiple rungs → keep rungs as-is, switch to Manual
        if (existingRungs.Length > 1)
        {
            logger.LogWarning(
                "AutoLadder with {Count} rungs: falling back to Manual mode.",
                existingRungs.Length
            );
            return profile with
            {
                Ladder = new LadderConfig { Mode = LadderMode.Manual, Rungs = existingRungs },
            };
        }

        VideoOutput reference =
            profile.Video
            ?? (
                existingRungs.Length == 1
                    ? PlanStageHelpers.BuildSyntheticReference(existingRungs[0])
                    : null
            );

        if (reference is null)
        {
            logger.LogWarning(
                "AutoLadder requires a reference Video output or at least one rung; "
                    + "profile has neither. Falling back to no video outputs."
            );
            return profile;
        }

        LadderRung[] rungs;

        if (profile.Ladder.AutoConfig is not null)
        {
            rungs = abrLadderGenerator.GenerateLadder(
                media,
                reference.Codec,
                profile.Ladder.AutoConfig,
                reference
            );
        }
        else
        {
            VideoOutput[] ladder = abrLadderGenerator.Generate(media, reference);
            if (ladder.Length == 0)
                return profile;

            rungs = ladder
                .Select(v => new LadderRung(
                    Width: v.Width,
                    Height: v.Height ?? 0,
                    Codec: v.Codec,
                    BitrateKbps: v.BitrateKbps,
                    MaxBitrateKbps: v.MaxBitrateKbps ?? 0,
                    BufferSizeKbps: v.BufferSizeKbps ?? 0,
                    Framerate: 0,
                    Preset: v.Preset,
                    CodecProfile: v.CodecProfile,
                    BitDepth: v.BitDepth,
                    PixelFormat: v.PixelFormat
                ))
                .ToArray();
        }

        if (rungs.Length == 0)
            return profile;

        logger.LogInformation(
            "AutoLadder expanded 1 reference profile → {Count} variants for {Source}",
            rungs.Length,
            media.FilePath
        );

        return profile with
        {
            Ladder = new LadderConfig { Mode = LadderMode.Manual, Rungs = rungs },
        };
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

    private static LegacyDrmConfig? ConvertDrmConfig(DrmConfig? v2Drm)
    {
        if (v2Drm is null)
            return null;

        LegacyDrmMethod method = v2Drm.Scheme.ToLowerInvariant() switch
        {
            "aes128" or "aes-128" => LegacyDrmMethod.Aes128,
            "cenc" => LegacyDrmMethod.Cenc,
            _ => LegacyDrmMethod.None,
        };

        string keyUri = v2Drm.Parameters?.GetValueOrDefault("key_uri") ?? string.Empty;

        return new LegacyDrmConfig(Method: method, KeyUri: keyUri);
    }

    /// <summary>
    /// Detects audio plans whose resolved template would land on the same
    /// on-disk path (same language + same codec token) and appends a per-stream
    /// suffix derived from the MapLabel <c>0:a:N</c> source index. Single-
    /// stream-per-language sources see no change. Plans that already differ
    /// (different language, different codec, or already suffixed) are left
    /// alone.
    /// </summary>
    private static IEnumerable<AudioOutputPlan> DisambiguateAudio(
        IReadOnlyList<AudioOutputPlan> plans
    )
    {
        // Group by the natural collision key — same resolved language + codec
        // token + same template lands on the same path.
        var groups = plans
            .Select((plan, idx) => new { plan, idx })
            .GroupBy(entry =>
                (
                    entry.plan.Language ?? "und",
                    AudioCodecTokenFor(entry.plan.EncoderName),
                    entry.plan.SegmentNameTemplate,
                    entry.plan.PlaylistNameTemplate
                )
            )
            .ToList();

        AudioOutputPlan[] result = plans.ToArray();
        foreach (var group in groups)
        {
            if (group.Count() < 2)
                continue;

            foreach (var entry in group)
            {
                int sourceIndex = ParseAudioSourceIndex(entry.plan.MapLabel);
                string suffix = $"_{sourceIndex}";
                result[entry.idx] = entry.plan with
                {
                    SegmentNameTemplate = AppendToTemplate(entry.plan.SegmentNameTemplate, suffix),
                    PlaylistNameTemplate = AppendToTemplate(
                        entry.plan.PlaylistNameTemplate,
                        suffix
                    ),
                };
            }
        }

        return result;
    }

    /// <summary>
    /// Detects video plans whose resolved template would land on the same
    /// on-disk path (same dimensions + same HDR/SDR designation) and appends a
    /// codec-family suffix so two different-codec rungs at the same resolution
    /// (e.g. H.264 1080p fallback + HEVC 1080p tonemap under EmitHdrAndSdr)
    /// don't share <c>video_1920x1080_SDR/</c>.
    /// </summary>
    private static VideoOutputPlan[] DisambiguateVideo(IReadOnlyList<VideoOutputPlan> plans)
    {
        var groups = plans
            .Select((plan, idx) => new { plan, idx })
            .GroupBy(entry =>
                (
                    entry.plan.Width,
                    entry.plan.Height,
                    entry.plan.IsHdrOutput,
                    entry.plan.SegmentNameTemplate,
                    entry.plan.PlaylistNameTemplate
                )
            )
            .ToList();

        VideoOutputPlan[] result = plans.ToArray();
        foreach (var group in groups)
        {
            if (group.Count() < 2)
                continue;

            foreach (var entry in group)
            {
                string suffix = $"_{VideoCodecFamilyToken(entry.plan.EncoderName)}";
                result[entry.idx] = entry.plan with
                {
                    SegmentNameTemplate = AppendToTemplate(entry.plan.SegmentNameTemplate, suffix),
                    PlaylistNameTemplate = AppendToTemplate(
                        entry.plan.PlaylistNameTemplate,
                        suffix
                    ),
                };
            }
        }

        return result;
    }

    /// <summary>
    /// Appends a suffix to every <c>/</c>-separated segment in a template so
    /// both the directory and filename halves get disambiguated.
    /// <c>"audio_eng_aac/audio_eng_aac"</c> + <c>"_0"</c> becomes
    /// <c>"audio_eng_aac_0/audio_eng_aac_0"</c>, not
    /// <c>"audio_eng_aac/audio_eng_aac_0"</c> — the latter would still
    /// collide on the .m4s segment files since they live in the same dir.
    /// </summary>
    private static string AppendToTemplate(string template, string suffix) =>
        string.Join('/', template.Split('/').Select(segment => segment + suffix));

    private static int ParseAudioSourceIndex(string mapLabel)
    {
        // MapLabel is "0:a:N" for audio streams. Pull N; fall back to 0 on
        // unexpected shapes so we still produce a unique-per-call suffix.
        int lastColon = mapLabel.LastIndexOf(':');
        return lastColon >= 0 && int.TryParse(mapLabel[(lastColon + 1)..], out int idx) ? idx : 0;
    }

    private static string AudioCodecTokenFor(string encoderName)
    {
        // Mirrors HlsOutputStrategy's codec-name shortening (lib / libfdk_
        // prefixes stripped) so the collision grouping uses the same key
        // the template would resolve to.
        return encoderName.Replace("libfdk_", "").Replace("lib", "");
    }

    /// <summary>
    /// Codec family token used to disambiguate same-resolution rungs encoded
    /// by different codecs. Matches the codec strings consumers expect in
    /// folder names: <c>avc</c> for H.264, <c>hevc</c> for H.265, <c>av1</c>
    /// for AV1, <c>vp9</c> for VP9. Falls back to the raw encoder name when
    /// the encoder doesn't match a known family.
    /// </summary>
    private static string VideoCodecFamilyToken(string encoderName)
    {
        string lower = encoderName.ToLowerInvariant();
        if (lower.Contains("264") || lower.Contains("h264") || lower == "libx264")
            return "avc";
        if (lower.Contains("265") || lower.Contains("hevc") || lower == "libx265")
            return "hevc";
        if (lower.Contains("av1"))
            return "av1";
        if (lower.Contains("vp9"))
            return "vp9";
        return lower;
    }
}
