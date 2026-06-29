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
    ISubtitleAcquisitionService? subtitleAcquisitionService = null,
    Composition.EncoderOptions? options = null
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
            EncodingProfile profile = AutoLadderExpander.Expand(
                abrLadderGenerator,
                logger,
                input.Profile,
                input.Media
            );
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

            // EmitHdrAndSdr (HDR source): split coverage along the bit-depth /
            // codec role. 10-bit rungs (HEVC Main10) preserve HDR via
            // passthrough; 8-bit rungs (H.264) carry the tonemapped SDR copy.
            // Each 10-bit rung emits ONE output (HDR), each 8-bit rung emits
            // ONE output (SDR) — no per-rung doubling. Net coverage for an
            // HDR source therefore relies on the ladder containing both 10-bit
            // and 8-bit rungs at the heights you want available in both ranges
            // (e.g. YouTube's H264FallbackHeights = [1080]). SDR-only clients
            // at heights without an 8-bit rung simply step down to the next
            // height that has one.
            //
            // Rationale: an SDR-tonemapped HEVC copy at a height that already
            // has an AVC SDR fallback is redundant — the AVC sibling already
            // covers HEVC-blocked / SDR-only clients. Doubling the 10-bit
            // rung wastes encode time + storage for a niche middle case
            // (HEVC-capable but cannot tonemap HDR client-side) that almost
            // no real-world client falls into.
            //
            // SDR source: this block is skipped (IsHdr gate); rungs emit
            // SDR as-is from EnumerateVideo regardless of bit depth.
            if (
                profile.HdrPolicy == HdrPolicy.EmitHdrAndSdr
                && input.Media.VideoStreams.Count > 0
                && input.Media.VideoStreams[0].IsHdr
            )
            {
                videoOutputs = videoOutputs
                    .Select(v =>
                        v.BitDepth >= 10
                            ? v with
                            {
                                ConvertHdrToSdr = false,
                            }
                            : v with
                            {
                                ConvertHdrToSdr = true,
                            }
                    )
                    .ToArray();
            }

            ResolvedCodec[] resolvedCodecs;

            if (hardwarePreferenceResolver is not null)
            {
                SpeedIndex effectiveSpeedIndex =
                    speedIndex ?? new SpeedIndex(new Dictionary<SpeedKey, SpeedMeasurement>());

                List<string> availableEncoderNames =
                    ffmpegCapabilities.AvailableEncoders.ToList() ?? [];

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

            OutputPlan outputPlan = await BuildOutputPlanAsync(
                    profile,
                    input.Media,
                    resolvedCodecs,
                    cropFilter,
                    context,
                    ct
                )
                .ConfigureAwait(false);

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

            GpuAccelPlan? gpuAccel = ResolveGpuAccel(outputPlan);
            if (gpuAccel is not null)
                outputPlan = outputPlan with { GpuAccel = gpuAccel };

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
    /// Resolves the GPU-resident decode+scale plan for this output, or null for
    /// the CPU path. Dark by default: only when <c>EncoderOptions.EnableGpuResident</c>
    /// is opted in, the host has a GPU, the plan is eligible (no tonemap / crop /
    /// burn-in), has no thumbnails (sprites need a CPU download), and the vendor's
    /// GPU scaler is present in the running ffmpeg build.
    /// </summary>
    private GpuAccelPlan? ResolveGpuAccel(OutputPlan outputPlan)
    {
        bool hasGpu = hardware is { HasGpu: true, Gpus.Count: > 0 };
        return GpuResidentActivation.Resolve(
            options?.EnableGpuResident == true,
            hasGpu,
            hasGpu ? hardware.Gpus[0].Vendor : null,
            outputPlan,
            ffmpegCapabilities.HasFilter
        );
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
                SourceFilename: Path.GetFileName(media.FilePath),
                MediaTitle: Path.GetFileNameWithoutExtension(media.FilePath),
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

    private async Task<OutputPlan> BuildOutputPlanAsync(
        EncodingProfile profile,
        MediaInfo media,
        ResolvedCodec[] resolvedCodecs,
        string? cropFilter,
        EncodingContext context,
        CancellationToken ct
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
        TonemapPlan tonemapPlan = await tonemapSelector
            .BuildAsync(profile.HdrOptions, null, context.DecisionsOrNoOp, cancellationToken: ct)
            .ConfigureAwait(false);

        VideoOutput[] videoOutputs = PlanStageHelpers.EnumerateVideo(profile);

        // EmitHdrAndSdr: same role split as in the resolver block above. Must
        // stay in lockstep so VideoOutputPlan count matches resolvedCodecs
        // count — 10-bit rung → HDR-only, 8-bit rung → SDR-only.
        if (
            profile.HdrPolicy == HdrPolicy.EmitHdrAndSdr
            && media.VideoStreams.Count > 0
            && media.VideoStreams[0].IsHdr
        )
        {
            videoOutputs = videoOutputs
                .Select(v =>
                    v.BitDepth >= 10
                        ? v with
                        {
                            ConvertHdrToSdr = false,
                        }
                        : v with
                        {
                            ConvertHdrToSdr = true,
                        }
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
                                        "Profile requests 10-bit video_{Index} but encoder {Encoder} does not support 10-bit. Downgrading to 8-bit output.",
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

                            // Profile/plugin CustomArguments escape hatch: merge the
                            // per-video custom flags last so user/plugin intent wins.
                            // ProfileValidator already blocks codec/format-overriding
                            // keys, so what reaches here is safe to apply verbatim.
                            if (v.CustomArguments is not null)
                            {
                                foreach ((string argKey, string argValue) in v.CustomArguments)
                                    extraFlags[argKey] = argValue;
                            }

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
        videoPlan = PlanStageDisambiguation.DisambiguateVideo(videoPlan);

        // Build one AudioOutputPlan per matching source stream.
        // AllowedLanguages is a FILTER — the actual language comes from the source stream.
        AudioOutputPlan[] audioPlan = AudioPlanBuilder.Build(profile, media);

        SubtitleOutputPlan[] subtitlePlan = SubtitlePlanBuilder.Build(profile, media);

        ThumbnailOutputPlan? thumbPlan = ThumbnailPlanBuilder.Build(profile, media);

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

        HlsDerivatives effectiveDerivatives = profile.HlsDerivatives ?? new HlsDerivatives();
        bool generateChapterThumbs = effectiveDerivatives.GenerateChapterThumbs;
        bool emitSubtitleChunks = effectiveDerivatives.SubtitleWebVtt;

        return new(
            outputFormat,
            videoPlan,
            audioPlan,
            subtitlePlan,
            thumbPlan,
            segmentDuration,
            PreserveDolbyVision: dvDecision.Preserved,
            Drm: DrmConfigConverter.Convert(profile.Drm),
            HlsOptions: hlsOptions,
            Layout: layout,
            GenerateChapterThumbs: generateChapterThumbs,
            EmitSubtitleWebVttChunks: emitSubtitleChunks,
            GlobalExtraFlags: profile.CustomArguments is { Count: > 0 }
                ? new Dictionary<string, string>(profile.CustomArguments)
                : null
        );
    }
}
