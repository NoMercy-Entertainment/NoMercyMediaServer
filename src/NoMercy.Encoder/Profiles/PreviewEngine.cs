namespace NoMercy.Encoder.Profiles;

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Pipeline;

/// <summary>
/// Pure, synchronous preview engine.
/// Given an <see cref="EncodingProfile"/> and a <see cref="MediaInfo"/> from ffprobe,
/// produces a <see cref="PreviewResult"/> containing the per-stream action plan,
/// source-level warning rules, and placeholder encode estimates.
///
/// <para>No DI, no IO — safe to call from tests without any infrastructure.</para>
/// </summary>
public static class PreviewEngine
{
    private static readonly StreamActionResolver Resolver = new();

    /// <summary>
    /// VFR threshold: real-vs-average frame rate divergence must exceed this
    /// fraction before the <c>source.variable_frame_rate</c> warning fires.
    /// 1 % catches typical container-vs-decode discrepancies while ignoring
    /// trivial rounding from ffprobe.
    /// </summary>
    private const double VfrThresholdFraction = 0.01;

    // Codecs that can carry Dolby Vision without stripping the RPU layer.
    private static readonly HashSet<string> DvCompatibleCodecs = ["hevc", "h265", "av1"];

    /// <summary>
    /// Analyse <paramref name="source"/> against <paramref name="profile"/> and return
    /// the per-stream plan, source warnings, and placeholder estimates.
    /// </summary>
    public static PreviewResult Analyze(EncodingProfile profile, MediaInfo source)
    {
        List<EncoderRule> warnings = [];

        EmitVfrWarning(source, warnings);
        EmitDolbyVisionWarning(profile, source, warnings);
        EmitUpscalingWarning(profile, source, warnings);

        PerStreamPlan plan = BuildPlan(profile, source);
        SourceAnalysisDto dto = BuildSourceAnalysis(source);

        return new PreviewResult(
            Plan: plan,
            SourceWarnings: warnings,
            SourceAnalysis: dto,
            EstimatedFps: 0,
            EstimatedDurationSeconds: (int)Math.Round(source.Duration.TotalSeconds),
            EncoderHandle: "auto"
        );
    }

    // -------------------------------------------------------------------------
    // Warning emitters
    // -------------------------------------------------------------------------

    private static void EmitVfrWarning(MediaInfo source, List<EncoderRule> warnings)
    {
        foreach (VideoStreamInfo video in source.VideoStreams)
        {
            if (
                video.AverageFrameRate is null
                || video.RealFrameRate is null
                || video.AverageFrameRate.Value <= 0
            )
            {
                continue;
            }

            double avg = video.AverageFrameRate.Value;
            double real = video.RealFrameRate.Value;
            double divergence = Math.Abs(avg - real) / avg;

            if (divergence > VfrThresholdFraction)
            {
                warnings.Add(
                    new EncoderRule(
                        EncoderRuleId.SourceVariableFrameRate,
                        EncoderRuleSeverity.Warning,
                        $"source.video_streams[{video.Index}].frame_rate",
                        $"Variable frame rate detected: average {avg:F3} fps vs real {real:F3} fps ({divergence:P1} divergence). "
                            + "FFmpeg may produce A/V drift or broken keyframe alignment.",
                        "Add -vsync vfr or -fps_mode vfr to your custom arguments, or set a fixed output frame rate in the video output."
                    )
                );
                break; // one warning is enough — VFR is a container-level property
            }
        }
    }

    private static void EmitDolbyVisionWarning(
        EncodingProfile profile,
        MediaInfo source,
        List<EncoderRule> warnings
    )
    {
        if (source.DolbyVision is null)
        {
            return;
        }

        VideoOutput? firstVideo = profile.VideoOutputs.FirstOrDefault();
        if (firstVideo is null)
        {
            return;
        }

        string targetCodecName = firstVideo.Codec switch
        {
            VideoCodecType.H265 => "hevc",
            VideoCodecType.Av1 => "av1",
            VideoCodecType.H264 => "h264",
            VideoCodecType.Vp9 => "vp9",
            _ => string.Empty,
        };

        bool codecPreservesDv = DvCompatibleCodecs.Contains(targetCodecName);
        bool bitDepthPreservesDv = firstVideo.TenBit; // 10-bit required for DV

        if (!codecPreservesDv || !bitDepthPreservesDv)
        {
            warnings.Add(
                new EncoderRule(
                    EncoderRuleId.SourceDolbyVisionWillBeStripped,
                    EncoderRuleSeverity.Warning,
                    "source.video_streams[0].dolby_vision",
                    "Dolby Vision RPU metadata will be stripped during transcode. "
                        + $"Target codec '{targetCodecName}' or bit depth ({(firstVideo.TenBit ? 10 : 8)}-bit) cannot carry DV.",
                    "Switch the video output codec to H.265 or AV1 with ten_bit: true to preserve Dolby Vision metadata."
                )
            );
        }
    }

    private static void EmitUpscalingWarning(
        EncodingProfile profile,
        MediaInfo source,
        List<EncoderRule> warnings
    )
    {
        VideoStreamInfo? primary = source.VideoStreams.FirstOrDefault();
        if (primary is null)
        {
            return;
        }

        for (int i = 0; i < profile.VideoOutputs.Length; i++)
        {
            VideoOutput output = profile.VideoOutputs[i];

            bool widthUpscales = output.Width > primary.Width;
            bool heightUpscales = output.Height.HasValue && output.Height.Value > primary.Height;

            if (widthUpscales || heightUpscales)
            {
                string dimension = widthUpscales
                    ? $"width {primary.Width} → {output.Width}"
                    : $"height {primary.Height} → {output.Height}";

                warnings.Add(
                    new EncoderRule(
                        EncoderRuleId.SourceUpscalingDetected,
                        EncoderRuleSeverity.Warning,
                        $"video_outputs[{i}].width",
                        $"Output {i} upscales the source ({dimension}). Upscaling wastes bitrate without improving quality.",
                        "Lower the output resolution to match or be smaller than the source, or set width/height to 0 for auto-scaling."
                    )
                );
            }
        }
    }

    // -------------------------------------------------------------------------
    // Per-stream plan builder
    // -------------------------------------------------------------------------

    private static PerStreamPlan BuildPlan(EncodingProfile profile, MediaInfo source)
    {
        List<VideoStreamAction> videoActions = BuildVideoActions(profile, source);
        List<AudioStreamAction> audioActions = BuildAudioActions(profile, source);
        List<SubtitleStreamAction> subtitleActions = BuildSubtitleActions(profile, source);

        return new PerStreamPlan(videoActions, audioActions, subtitleActions);
    }

    private static List<VideoStreamAction> BuildVideoActions(
        EncodingProfile profile,
        MediaInfo source
    )
    {
        List<VideoStreamAction> actions = [];

        foreach (VideoStreamInfo video in source.VideoStreams)
        {
            VideoOutput? matched = profile.VideoOutputs.FirstOrDefault();

            if (matched is null)
            {
                actions.Add(
                    new VideoStreamAction(
                        SourceIndex: video.Index,
                        Action: "skip",
                        Reason: "No video output defined in profile.",
                        Target: null,
                        FilterChainSummary: null,
                        BitDepthDowngrade: false
                    )
                );
                continue;
            }

            StreamAction streamAction = Resolver.ResolveVideo(video, matched);
            string actionStr = MapStreamAction(streamAction);

            bool bitDepthDowngrade = video.BitDepth > 8 && !matched.TenBit;

            string? filterSummary =
                actionStr == "transcode"
                    ? BuildVideoFilterSummary(video, matched, bitDepthDowngrade)
                    : null;

            actions.Add(
                new VideoStreamAction(
                    SourceIndex: video.Index,
                    Action: actionStr,
                    Reason: BuildVideoReason(streamAction, video, matched),
                    Target: matched,
                    FilterChainSummary: filterSummary,
                    BitDepthDowngrade: bitDepthDowngrade
                )
            );
        }

        return actions;
    }

    private static List<AudioStreamAction> BuildAudioActions(
        EncodingProfile profile,
        MediaInfo source
    )
    {
        List<AudioStreamAction> actions = [];

        foreach (AudioStreamInfo audio in source.AudioStreams)
        {
            AudioOutput? matched = FindMatchingAudioOutput(profile, audio);

            if (matched is null)
            {
                actions.Add(
                    new AudioStreamAction(
                        SourceIndex: audio.Index,
                        Action: "skip",
                        Reason: "No audio output matches this stream's language or codec.",
                        Target: null
                    )
                );
                continue;
            }

            StreamAction streamAction = Resolver.ResolveAudio(audio, matched, profile.Format);
            string actionStr = MapStreamAction(streamAction);

            actions.Add(
                new AudioStreamAction(
                    SourceIndex: audio.Index,
                    Action: actionStr,
                    Reason: BuildAudioReason(streamAction, audio, matched),
                    Target: matched
                )
            );
        }

        return actions;
    }

    private static List<SubtitleStreamAction> BuildSubtitleActions(
        EncodingProfile profile,
        MediaInfo source
    )
    {
        List<SubtitleStreamAction> actions = [];

        foreach (SubtitleStreamInfo subtitle in source.SubtitleStreams)
        {
            SubtitleOutput? matched = FindMatchingSubtitleOutput(profile, subtitle);

            if (matched is null)
            {
                actions.Add(
                    new SubtitleStreamAction(
                        SourceIndex: subtitle.Index,
                        Action: "skip",
                        Reason: "No subtitle output matches this stream's language.",
                        Target: null
                    )
                );
                continue;
            }

            StreamAction streamAction = Resolver.ResolveSubtitle(subtitle, matched, profile.Format);
            string actionStr = MapSubtitleStreamAction(streamAction, subtitle, matched);

            actions.Add(
                new SubtitleStreamAction(
                    SourceIndex: subtitle.Index,
                    Action: actionStr,
                    Reason: BuildSubtitleReason(streamAction, subtitle, matched),
                    Target: matched
                )
            );
        }

        return actions;
    }

    // -------------------------------------------------------------------------
    // Matching helpers
    // -------------------------------------------------------------------------

    private static AudioOutput? FindMatchingAudioOutput(
        EncodingProfile profile,
        AudioStreamInfo audio
    )
    {
        string? lang = audio.Language?.ToLowerInvariant();

        // Prefer an output that explicitly lists this language.
        foreach (AudioOutput output in profile.AudioOutputs)
        {
            if (
                output.AllowedLanguages.Length == 0
                || output.AllowedLanguages.Any(l =>
                    l.Equals(lang, StringComparison.OrdinalIgnoreCase)
                    || l.Equals("*", StringComparison.Ordinal)
                )
            )
            {
                return output;
            }
        }

        return null;
    }

    private static SubtitleOutput? FindMatchingSubtitleOutput(
        EncodingProfile profile,
        SubtitleStreamInfo subtitle
    )
    {
        string? lang = subtitle.Language?.ToLowerInvariant();

        foreach (SubtitleOutput output in profile.SubtitleOutputs)
        {
            if (
                output.AllowedLanguages.Length == 0
                || output.AllowedLanguages.Any(l =>
                    l.Equals(lang, StringComparison.OrdinalIgnoreCase)
                    || l.Equals("*", StringComparison.Ordinal)
                )
            )
            {
                return output;
            }
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // Action string mapping
    // -------------------------------------------------------------------------

    private static string MapStreamAction(StreamAction action) =>
        action switch
        {
            StreamAction.Copy => "copy",
            StreamAction.Transcode => "transcode",
            StreamAction.Drop => "skip",
            StreamAction.Extract => "extract",
            _ => "transcode",
        };

    private static string MapSubtitleStreamAction(
        StreamAction action,
        SubtitleStreamInfo subtitle,
        SubtitleOutput output
    )
    {
        if (output.Mode == SubtitleMode.BurnIn)
        {
            return "burn_in";
        }

        return action switch
        {
            StreamAction.Copy => "copy",
            StreamAction.Extract when !subtitle.IsTextBased => "extract_ocr",
            StreamAction.Extract => "extract",
            StreamAction.Transcode => "transcode",
            StreamAction.Drop => "skip",
            _ => "extract",
        };
    }

    // -------------------------------------------------------------------------
    // Reason builders
    // -------------------------------------------------------------------------

    private static string BuildVideoReason(
        StreamAction action,
        VideoStreamInfo source,
        VideoOutput target
    ) =>
        action switch
        {
            StreamAction.Copy =>
                $"Source codec, resolution, and bitrate all satisfy the output profile.",
            StreamAction.Transcode =>
                $"Source {source.Codec} {source.Width}x{source.Height} @ {source.BitRateKbps} kbps → "
                    + $"target {target.Codec} {target.Width}x{target.Height ?? source.Height} @ {target.BitrateKbps} kbps.",
            _ => "Stream will be skipped — no matching video output.",
        };

    private static string BuildAudioReason(
        StreamAction action,
        AudioStreamInfo source,
        AudioOutput target
    ) =>
        action switch
        {
            StreamAction.Copy =>
                $"Source codec, bitrate, and channel count satisfy the output profile.",
            StreamAction.Transcode =>
                $"Source {source.Codec} {source.Channels}ch @ {source.BitRateKbps} kbps → "
                    + $"target {target.Codec} {target.Channels}ch @ {target.BitrateKbps} kbps.",
            _ => "Stream will be skipped — no matching audio output.",
        };

    private static string BuildSubtitleReason(
        StreamAction action,
        SubtitleStreamInfo source,
        SubtitleOutput target
    ) =>
        action switch
        {
            StreamAction.Copy => "Text subtitle copied into the output container.",
            StreamAction.Extract => source.IsTextBased
                ? "Text subtitle extracted to WebVTT sidecar."
                : "Bitmap subtitle extracted via OCR.",
            StreamAction.Transcode when target.Mode == SubtitleMode.BurnIn =>
                "Subtitle burned into the video — permanently visible, not selectable.",
            StreamAction.Transcode => "Subtitle will be transcoded.",
            StreamAction.Drop => "Stream will be dropped.",
            _ => "Stream will be skipped — no matching subtitle output.",
        };

    // -------------------------------------------------------------------------
    // Filter chain summary
    // -------------------------------------------------------------------------

    private static string BuildVideoFilterSummary(
        VideoStreamInfo source,
        VideoOutput target,
        bool bitDepthDowngrade
    )
    {
        List<string> filters = [];

        int targetW = target.Width;
        int targetH = target.Height ?? source.Height;

        if (source.Width != targetW || source.Height != targetH)
        {
            filters.Add($"scale={targetW}:{targetH}");
        }

        if (bitDepthDowngrade)
        {
            filters.Add("format=yuv420p");
        }
        else if (target.TenBit)
        {
            filters.Add("format=yuv420p10le");
        }

        if (target.ConvertHdrToSdr)
        {
            filters.Add("zscale=t=linear,tonemap=hable,zscale=t=bt709");
        }

        return filters.Count > 0 ? string.Join(",", filters) : "null";
    }

    // -------------------------------------------------------------------------
    // SourceAnalysisDto builder
    // -------------------------------------------------------------------------

    private static SourceAnalysisDto BuildSourceAnalysis(MediaInfo source)
    {
        VideoStreamInfo? primary = source.VideoStreams.FirstOrDefault();

        return new SourceAnalysisDto(
            Format: source.Format,
            DurationSeconds: source.Duration.TotalSeconds,
            FileSizeBytes: source.FileSizeBytes,
            OverallBitRateKbps: source.OverallBitRateKbps,
            VideoStreamCount: source.VideoStreams.Count,
            AudioStreamCount: source.AudioStreams.Count,
            SubtitleStreamCount: source.SubtitleStreams.Count,
            ChapterCount: source.Chapters.Count,
            AttachmentCount: source.Attachments.Count,
            HasDolbyVision: source.DolbyVision is not null,
            PrimaryVideoCodec: primary?.Codec,
            PrimaryVideoWidth: primary?.Width,
            PrimaryVideoHeight: primary?.Height
        );
    }
}

/// <summary>
/// Internal result type returned by <see cref="PreviewEngine.Analyze"/>.
/// The controller maps this to <see cref="PreviewResponse"/>.
/// </summary>
public sealed record PreviewResult(
    PerStreamPlan Plan,
    IReadOnlyList<EncoderRule> SourceWarnings,
    SourceAnalysisDto SourceAnalysis,
    int EstimatedFps,
    int EstimatedDurationSeconds,
    string EncoderHandle
);
