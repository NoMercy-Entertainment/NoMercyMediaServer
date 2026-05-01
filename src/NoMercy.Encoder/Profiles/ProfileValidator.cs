using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;

namespace NoMercy.Encoder.Profiles;

public class ProfileValidator(CodecRegistry codecRegistry) : IProfileValidator
{
    // ──────────────────────────────────────────────────────────────────────────
    // RuleSink — single accumulator; renders to both legacy and envelope shapes.
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Accumulates findings from every Validate* method in a single pass.
    /// Callers project to either <see cref="ValidationResult"/> (legacy) or
    /// <see cref="ValidationEnvelope"/> (new) after the pass completes.
    /// </summary>
    private sealed class RuleSink
    {
        private readonly List<EncoderRule> _rules = [];

        public void Add(
            string id,
            EncoderRuleSeverity severity,
            string field,
            string message,
            string fix
        )
        {
            _rules.Add(new EncoderRule(id, severity, field, message, fix));
        }

        public IReadOnlyList<EncoderRule> Rules => _rules;

        public ValidationResult ToValidationResult()
        {
            ValidationError[] errors = _rules
                .Select(r => new ValidationError(
                    r.Field,
                    r.Message,
                    r.Severity == EncoderRuleSeverity.Error
                        ? ValidationSeverity.Error
                        : ValidationSeverity.Warning
                ))
                .ToArray();
            bool isValid = errors.All(e => e.Severity != ValidationSeverity.Error);
            return new(isValid, errors);
        }

        public ValidationEnvelope ToEnvelope() => ValidationEnvelope.FromRules(_rules);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Legacy API — retained as a compat shim.</summary>
    public ValidationResult Validate(EncodingProfile profile)
    {
        RuleSink sink = new();
        RunAllRules(profile, sink);
        return sink.ToValidationResult();
    }

    /// <summary>New API — emits stable rule IDs + fix strings.</summary>
    public ValidationEnvelope ValidateAsEnvelope(EncodingProfile profile)
    {
        RuleSink sink = new();
        RunAllRules(profile, sink);
        return sink.ToEnvelope();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Orchestrator — all rules in one place, single source of truth
    // ──────────────────────────────────────────────────────────────────────────

    private void RunAllRules(EncodingProfile profile, RuleSink sink)
    {
        ValidateName(profile, sink);
        ValidateOutputsNotEmpty(profile, sink);
        ValidateVideoOutputs(profile, sink);
        ValidateAudioOutputs(profile, sink);
        ValidateFormatCompatibility(profile, sink);
        ValidateSubtitleCompatibility(profile, sink);
        ValidateHdrPathSafety(profile, sink);
        ValidateNoDuplicateVariants(profile, sink);
        ValidateMonotonicLadder(profile, sink);
        ValidateVideoWithoutAudioForStreaming(profile, sink);
        ValidateAudioLanguageFilterOverlap(profile, sink);
        ValidateHlsFmp4CodecCompatibility(profile, sink);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Structural rules
    // ──────────────────────────────────────────────────────────────────────────

    private static void ValidateName(EncodingProfile profile, RuleSink sink)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            sink.Add(
                EncoderRuleId.ProfileNameMissing,
                EncoderRuleSeverity.Error,
                "Name",
                "Profile must have a non-empty name.",
                "Set the `name` field to a non-empty string."
            );
        }
    }

    private static void ValidateOutputsNotEmpty(EncodingProfile profile, RuleSink sink)
    {
        if (profile.VideoOutputs.Length == 0 && profile.AudioOutputs.Length == 0)
        {
            sink.Add(
                EncoderRuleId.ProfileNoOutputs,
                EncoderRuleSeverity.Error,
                "Outputs",
                "Profile must have at least one video or audio output.",
                "Add at least one entry to `VideoOutputs` or `AudioOutputs`."
            );
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Streaming container with video but no audio
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Streaming container (HLS / DASH) with video outputs but zero audio
    /// outputs is almost always a mistake — iOS clients particularly refuse
    /// to play HLS streams with no audio track, and the web client shows
    /// a muted player with no explanation. Legitimate silent-video use
    /// cases exist (CCTV replay, demo loops) but they're rare and the user
    /// should know they're on that path.
    /// </summary>
    private static void ValidateVideoWithoutAudioForStreaming(
        EncodingProfile profile,
        RuleSink sink
    )
    {
        bool isStreamingContainer = profile.Format is OutputFormat.Hls or OutputFormat.Dash;
        if (
            isStreamingContainer
            && profile.VideoOutputs.Length > 0
            && profile.AudioOutputs.Length == 0
        )
        {
            sink.Add(
                EncoderRuleId.AudioCodecContainerMismatch,
                EncoderRuleSeverity.Warning,
                "AudioOutputs",
                $"{profile.Format} profile has video but no audio outputs. iOS and many smart-"
                    + "TV clients refuse to play streams with no audio track. Add at least one "
                    + "audio output, or switch to MKV/MP4 if silent video is intentional.",
                "Add at least one `AudioOutput` (e.g. AAC 192 kbps stereo), or change `Format` to MKV/MP4 for silent video."
            );
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Audio language filter overlap
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Two audio outputs whose AllowedLanguages lists overlap will both
    /// try to encode the same source stream — double the CPU, two copies
    /// of the same file at the same codec. Almost always a misconfiguration
    /// (user meant to separate english and japanese tracks, forgot to set
    /// the language filter). Warn.
    ///
    /// Empty AllowedLanguages means "all languages" — treat overlap with
    /// any other entry as total overlap.
    /// </summary>
    private static void ValidateAudioLanguageFilterOverlap(EncodingProfile profile, RuleSink sink)
    {
        if (profile.AudioOutputs.Length < 2)
            return;

        for (int i = 0; i < profile.AudioOutputs.Length; i++)
        {
            for (int j = i + 1; j < profile.AudioOutputs.Length; j++)
            {
                AudioOutput a = profile.AudioOutputs[i];
                AudioOutput b = profile.AudioOutputs[j];

                // Only flag when the codec+channels pair would produce
                // identical outputs. Different codecs or channel counts is
                // legit intent (e.g., AAC stereo + EAC3 5.1 of the same
                // language for bandwidth tiering).
                if (a.Codec != b.Codec || a.Channels != b.Channels)
                    continue;

                if (!LanguageFiltersOverlap(a.AllowedLanguages, b.AllowedLanguages))
                    continue;

                sink.Add(
                    EncoderRuleId.AudioCodecContainerMismatch,
                    EncoderRuleSeverity.Warning,
                    $"AudioOutput[{j}].AllowedLanguages",
                    $"AudioOutput[{i}] and AudioOutput[{j}] have the same codec "
                        + $"({a.Codec} {a.Channels}ch) and overlapping language filters — "
                        + "they'll both encode the same source streams, wasting CPU and "
                        + "producing duplicate output files. Narrow one of the filters or "
                        + "remove the duplicate output.",
                    $"Set non-overlapping `AllowedLanguages` on AudioOutput[{i}] and AudioOutput[{j}], or remove the duplicate."
                );
            }
        }
    }

    private static bool LanguageFiltersOverlap(string[] first, string[] second)
    {
        // Empty list means "accept all" — overlaps with anything.
        if (first.Length == 0 || second.Length == 0)
            return true;

        HashSet<string> a = new(first, StringComparer.OrdinalIgnoreCase);
        return second.Any(l => a.Contains(l));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // HDR path safety hook (spec-mandated placeholder)
    // ──────────────────────────────────────────────────────────────────────────

    private static void ValidateHdrPathSafety(EncodingProfile profile, RuleSink sink)
    {
        // VideoOutput does not currently carry HdrOptions directly.
        // When HdrOptions are added to VideoOutput, validate CustomLutPath here.
        // This method exists as the spec-mandated hook for that validation.
        for (int i = 0; i < profile.VideoOutputs.Length; i++)
        {
            // Future: if profile.VideoOutputs[i].HdrOptions?.CustomLutPath is set,
            // call ValidatePathSafety($"VideoOutput[{i}].HdrOptions.CustomLutPath", ...)
            _ = i;
        }
    }

    private static void ValidatePathSafety(string field, string? path, RuleSink sink)
    {
        if (string.IsNullOrEmpty(path))
            return;

        if (path.Contains("..", StringComparison.Ordinal))
        {
            sink.Add(
                EncoderRuleId.OutputPathNotAllowed,
                EncoderRuleSeverity.Error,
                field,
                $"{field} must not contain path traversal sequences ('..').",
                $"Remove the `..` segments from `{field}`."
            );
            return;
        }

        if (Path.IsPathRooted(path))
        {
            sink.Add(
                EncoderRuleId.OutputPathNotAllowed,
                EncoderRuleSeverity.Error,
                field,
                $"{field} must not be an absolute path.",
                $"Change `{field}` to a relative path."
            );
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Video output rules
    // ──────────────────────────────────────────────────────────────────────────

    private void ValidateVideoOutputs(EncodingProfile profile, RuleSink sink)
    {
        for (int i = 0; i < profile.VideoOutputs.Length; i++)
        {
            VideoOutput output = profile.VideoOutputs[i];
            string prefix = $"VideoOutput[{i}]";

            if (output.Width <= 0)
            {
                sink.Add(
                    EncoderRuleId.VideoWidthInvalid,
                    EncoderRuleSeverity.Error,
                    $"{prefix}.Width",
                    "Width must be greater than 0.",
                    $"Set `{prefix}.Width` to a positive integer (e.g. 1920)."
                );
            }

            if (output.TenBit)
            {
                ValidateTenBitFeasibility(output, prefix, sink);
            }

            if (output.BitrateKbps <= 0 && output.Crf <= 0)
            {
                sink.Add(
                    EncoderRuleId.VideoRateControlMissing,
                    EncoderRuleSeverity.Error,
                    $"{prefix}.RateControl",
                    "Video output must specify either BitrateKbps > 0 or Crf > 0.",
                    $"Set `{prefix}.BitrateKbps` to a positive value, or set `{prefix}.Crf` to a valid CRF (e.g. 23)."
                );
            }

            if (output.BitrateKbps > 0 && output.Crf > 0)
            {
                // CRF and Bitrate are mutually exclusive rate-control modes.
                // The resolver picks CRF first, silently ignoring the bitrate —
                // warn so the user knows their "target bitrate" is not honored.
                sink.Add(
                    EncoderRuleId.VideoRateControlConflict,
                    EncoderRuleSeverity.Warning,
                    $"{prefix}.RateControl",
                    $"Both Crf={output.Crf} and BitrateKbps={output.BitrateKbps} are set. "
                        + "These are mutually exclusive — CRF wins, BitrateKbps is ignored. "
                        + "Set exactly one.",
                    $"Remove either `{prefix}.Crf` or `{prefix}.BitrateKbps`; they are mutually exclusive."
                );
            }

            if (output.Crf > 0)
            {
                ICodecDefinition definition = codecRegistry.GetVideoDefinition(output.Codec);
                EncoderInfo? softwareEncoder = Array.Find(
                    definition.Encoders,
                    e => e.RequiredVendor == null
                );

                if (softwareEncoder != null)
                {
                    QualityRange range = softwareEncoder.QualityRange;
                    if (output.Crf < range.Min || output.Crf > range.Max)
                    {
                        sink.Add(
                            EncoderRuleId.CrfOutOfTypicalRange,
                            EncoderRuleSeverity.Error,
                            $"{prefix}.Crf",
                            $"Crf value {output.Crf} is outside the valid range [{range.Min}, {range.Max}] for {output.Codec}.",
                            $"Set `{prefix}.Crf` to a value between {range.Min} and {range.Max}."
                        );
                    }
                }
            }

            if (output.KeyframeIntervalSeconds < 0)
            {
                sink.Add(
                    EncoderRuleId.HlsKeyframeSegmentMisalignment,
                    EncoderRuleSeverity.Error,
                    $"{prefix}.KeyframeIntervalSeconds",
                    "KeyframeIntervalSeconds must be >= 0.",
                    $"Set `{prefix}.KeyframeIntervalSeconds` to 0 (auto) or a positive integer."
                );
            }

            ValidateLevelVsDimensions(output, prefix, sink);
            ValidateVp9ProfileBitdepth(output, prefix, sink);
            ValidatePresetKnownToCodec(output, prefix, sink);
            ValidateProfileKnownToCodec(output, prefix, sink);
            ValidateCustomArgumentsReservedFlags(output.CustomArguments, prefix, sink);
            ValidateBitratePerResolution(output, prefix, sink);
            ValidateCrfQualityFloor(output, prefix, sink);
        }
    }

    /// <summary>
    /// Below a codec-specific bitrate floor for a given resolution the output
    /// gets blocky / smeared regardless of preset. Many users set 4K profiles
    /// with 2 Mbps thinking they'll save disk space; the result is unwatchable.
    /// Warn with a rough floor — not a hard block since some encoders
    /// (slow+slower presets) can get away with less.
    ///
    /// Only fires when bitrate rate-control is configured (BitrateKbps > 0).
    /// CRF mode has no target bitrate so the concept doesn't apply.
    /// </summary>
    private static void ValidateBitratePerResolution(
        VideoOutput output,
        string prefix,
        RuleSink sink
    )
    {
        if (output.BitrateKbps <= 0)
            return;

        int height = output.Height ?? output.Width * 9 / 16;

        int floorKbps = MinBitrateFloor(output.Codec, height);
        if (floorKbps <= 0)
            return;

        if (output.BitrateKbps < floorKbps)
        {
            sink.Add(
                EncoderRuleId.BitrateTooLowForResolution,
                EncoderRuleSeverity.Warning,
                $"{prefix}.BitrateKbps",
                $"{output.Codec} at {output.Width}x{height} below {floorKbps} kbps produces "
                    + "visible blocking and smearing regardless of preset. Typical minimum "
                    + $"for this resolution is {floorKbps} kbps; raise it or switch to CRF mode.",
                $"Raise `{prefix}.BitrateKbps` to at least {floorKbps} kbps, or remove `BitrateKbps` and set a `Crf` value instead."
            );
        }
    }

    /// <summary>
    /// CRF in the 35+ range for H.264 / H.265 produces blocky, smeared output
    /// that most viewers reject. Warn so the user knows they've configured
    /// near the bottom of the quality cliff — not a hard error because some
    /// archival / thumbnail-preview use cases legitimately want tiny files.
    ///
    /// Note: in this codebase CRF 0 means "not configured"; the bitrate path
    /// handles that branch. ffmpeg's actual CRF=0 lossless mode would need
    /// a separate opt-in field, not an inferred value.
    /// </summary>
    private static void ValidateCrfQualityFloor(VideoOutput output, string prefix, RuleSink sink)
    {
        if (output.Crf <= 0)
            return;

        // Barely-watchable territory by codec. Thresholds reflect where the
        // quality cliff becomes obvious — H.264/H.265 share the 0-51 scale,
        // AV1 uses 0-63 so its cliff is shifted up proportionally.
        (int ThresholdCrf, string CodecDescription) floor = output.Codec switch
        {
            VideoCodecType.H264 => (35, "H.264"),
            VideoCodecType.H265 => (35, "H.265"),
            VideoCodecType.Vp9 => (45, "VP9"),
            VideoCodecType.Av1 => (50, "AV1"),
            _ => (0, ""),
        };

        if (floor.ThresholdCrf > 0 && output.Crf >= floor.ThresholdCrf)
        {
            sink.Add(
                EncoderRuleId.CrfOutOfTypicalRange,
                EncoderRuleSeverity.Warning,
                $"{prefix}.Crf",
                $"CRF {output.Crf} on {floor.CodecDescription} produces noticeably blocky, "
                    + "smeared output. Typical quality range is 18-28 — higher values cut "
                    + "file size at a sharp quality cliff.",
                $"Lower `{prefix}.Crf` to the 18–28 range for acceptable quality."
            );
        }
    }

    /// <summary>
    /// Rough bitrate floor per codec × resolution. Values reflect "don't go
    /// below this unless you know what you're doing" — they're not Netflix-
    /// quality targets, just the point below which visible artifacts dominate.
    /// HEVC / AV1 / VP9 get lower floors than H.264 (modern codecs are ~30%
    /// more efficient).
    /// </summary>
    private static int MinBitrateFloor(VideoCodecType codec, int height)
    {
        bool modernCodec = codec != VideoCodecType.H264;

        return height switch
        {
            >= 2160 => modernCodec ? 8_000 : 12_000,
            >= 1440 => modernCodec ? 5_000 : 7_500,
            >= 1080 => modernCodec ? 1_500 : 2_500,
            >= 720 => modernCodec ? 800 : 1_500,
            >= 480 => modernCodec ? 500 : 900,
            _ => 0, // Too small to reliably floor — various content types.
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Per-output video guards
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// H.264 / H.265 / VP9 levels carry a maximum frame size in luma samples.
    /// A 4K source at Level 4.1 plays on modern desktop clients but hardware
    /// decoders (iOS, smart TVs, set-top boxes) reject the stream because the
    /// declared level doesn't permit the frame size. Fails late and silently —
    /// validate upfront instead.
    /// </summary>
    private static void ValidateLevelVsDimensions(VideoOutput output, string prefix, RuleSink sink)
    {
        if (string.IsNullOrWhiteSpace(output.Level) || output.Width <= 0)
            return;

        int outputHeight = output.Height ?? output.Width * 9 / 16;
        long pixelsRequested = (long)output.Width * outputHeight;

        long? maxPixels = CodecLevelLimit(output.Codec, output.Level);
        if (maxPixels is null)
            return; // Unknown level — handled by ValidateProfileKnownToCodec.

        if (pixelsRequested > maxPixels.Value)
        {
            sink.Add(
                EncoderRuleId.LevelResolutionMismatch,
                EncoderRuleSeverity.Error,
                $"{prefix}.Level",
                $"{output.Codec} Level {output.Level} caps at {FormatPixels(maxPixels.Value)} "
                    + $"but the output is {output.Width}x{outputHeight} "
                    + $"({FormatPixels(pixelsRequested)}). Hardware decoders (iOS, set-top "
                    + $"boxes, TVs) reject streams above the declared level. Raise the level "
                    + $"or drop the resolution.",
                $"Increase `{prefix}.Level` to one that supports {output.Width}x{outputHeight}, or reduce the output resolution."
            );
        }
    }

    /// <summary>
    /// VP9's bit depth is locked to its profile number. profile0/profile1 are
    /// 8-bit only; profile2/profile3 carry 10-bit (and higher-chroma variants).
    /// Setting TenBit=true with profile0 or profile1 produces either an 8-bit
    /// output (silent downgrade) or a malformed stream depending on encoder.
    /// </summary>
    private static void ValidateVp9ProfileBitdepth(VideoOutput output, string prefix, RuleSink sink)
    {
        if (output.Codec != VideoCodecType.Vp9 || string.IsNullOrWhiteSpace(output.Profile))
            return;

        bool profileIs8BitOnly =
            output.Profile.Equals("profile0", StringComparison.OrdinalIgnoreCase)
            || output.Profile.Equals("profile1", StringComparison.OrdinalIgnoreCase);
        bool profileIs10Bit =
            output.Profile.Equals("profile2", StringComparison.OrdinalIgnoreCase)
            || output.Profile.Equals("profile3", StringComparison.OrdinalIgnoreCase);

        if (output.TenBit && profileIs8BitOnly)
        {
            sink.Add(
                EncoderRuleId.BitDepthVp9ProfileMismatch,
                EncoderRuleSeverity.Error,
                $"{prefix}.Profile",
                $"VP9 {output.Profile} is 8-bit only; TenBit=true produces 8-bit output "
                    + "or a malformed stream. Use profile2 or profile3 for 10-bit VP9.",
                $"Change `{prefix}.Profile` to `profile2` or `profile3` when `TenBit` is true."
            );
        }

        if (!output.TenBit && profileIs10Bit)
        {
            sink.Add(
                EncoderRuleId.BitDepthVp9ProfileMismatch,
                EncoderRuleSeverity.Warning,
                $"{prefix}.Profile",
                $"VP9 {output.Profile} requires 10-bit content; TenBit is false. "
                    + "Either set TenBit=true or drop to profile0/profile1.",
                $"Set `{prefix}.TenBit` to true, or change `{prefix}.Profile` to `profile0` or `profile1`."
            );
        }
    }

    /// <summary>
    /// A preset like "p4" (NVENC-native) set with codec H.264 makes libx264
    /// fall back to its middle preset silently — the user gets a different
    /// encode than configured. Warn when the preset isn't known to any
    /// encoder in the codec family.
    /// </summary>
    private void ValidatePresetKnownToCodec(VideoOutput output, string prefix, RuleSink sink)
    {
        if (string.IsNullOrWhiteSpace(output.Preset))
            return;

        ICodecDefinition definition = codecRegistry.GetVideoDefinition(output.Codec);
        bool knownPreset = definition.Encoders.Any(e => e.Presets.Contains(output.Preset));

        if (!knownPreset)
        {
            sink.Add(
                EncoderRuleId.CodecContainerMismatch,
                EncoderRuleSeverity.Warning,
                $"{prefix}.Preset",
                $"Preset '{output.Preset}' is not recognized by any {output.Codec} encoder. "
                    + "The resolver will substitute a default — your preset is ignored.",
                $"Replace `{prefix}.Preset` with a value supported by {output.Codec} (check the codec definition for valid presets)."
            );
        }
    }

    private void ValidateProfileKnownToCodec(VideoOutput output, string prefix, RuleSink sink)
    {
        if (string.IsNullOrWhiteSpace(output.Profile))
            return;

        ICodecDefinition definition = codecRegistry.GetVideoDefinition(output.Codec);
        bool knownProfile = definition.Encoders.Any(e => e.Profiles.Contains(output.Profile));

        if (!knownProfile)
        {
            sink.Add(
                EncoderRuleId.CodecContainerMismatch,
                EncoderRuleSeverity.Warning,
                $"{prefix}.Profile",
                $"Profile '{output.Profile}' is not recognized by any {output.Codec} encoder. "
                    + "The resolver will fall back to the first declared profile.",
                $"Replace `{prefix}.Profile` with a value supported by {output.Codec} (check the codec definition for valid profiles)."
            );
        }
    }

    // FFmpeg flags the encoder pipeline owns — letting user CustomArguments
    // override them would silently clobber resolver decisions (hw encoder,
    // preset, quality, pixel format). The user's edit looks like it works
    // until the output is wrong in ways they can't debug.
    private static readonly HashSet<string> ReservedFfmpegFlags = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "-c:v",
        "-c:a",
        "-c:s",
        "-vcodec",
        "-acodec",
        "-preset",
        "-profile:v",
        "-level",
        "-crf",
        "-cq",
        "-qp",
        "-global_quality",
        "-q:v",
        "-b:v",
        "-b:a",
        "-maxrate",
        "-minrate",
        "-bufsize",
        "-s",
        "-pix_fmt",
        "-rc",
        "-vf",
        "-map",
        "-f",
        "-tag:v",
        "-hls_time",
        "-hls_segment_filename",
        "-hls_playlist_type",
        "-seg_duration",
        "-init_hw_device",
        "-filter_hw_device",
        "-gpu",
        "-hwaccel",
    };

    private static void ValidateCustomArgumentsReservedFlags(
        Dictionary<string, string>? customArgs,
        string prefix,
        RuleSink sink
    )
    {
        if (customArgs is null || customArgs.Count == 0)
            return;

        foreach (string key in customArgs.Keys)
        {
            if (ReservedFfmpegFlags.Contains(key))
            {
                sink.Add(
                    EncoderRuleId.CustomArgsReservedFlag,
                    EncoderRuleSeverity.Error,
                    $"{prefix}.CustomArguments",
                    $"CustomArguments key '{key}' collides with a flag the encoder pipeline "
                        + "controls (codec, preset, quality, pixel format, filter chain, "
                        + "or hardware init). Letting it through would silently override the "
                        + "resolver's choice. Remove it — the matching profile field is the "
                        + "supported way to set it.",
                    $"Remove `{key}` from `{prefix}.CustomArguments` and use the corresponding profile field instead."
                );
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cross-output guards — duplicates + ladder monotonicity
    // ──────────────────────────────────────────────────────────────────────────

    private static void ValidateNoDuplicateVariants(EncodingProfile profile, RuleSink sink)
    {
        if (profile.VideoOutputs.Length < 2)
            return;

        HashSet<string> seen = [];
        for (int i = 0; i < profile.VideoOutputs.Length; i++)
        {
            VideoOutput v = profile.VideoOutputs[i];
            int h = v.Height ?? v.Width * 9 / 16;
            string key = $"{v.Codec}|{v.Width}x{h}|crf={v.Crf}|br={v.BitrateKbps}|tb={v.TenBit}";
            if (!seen.Add(key))
            {
                sink.Add(
                    EncoderRuleId.LadderDuplicateVariant,
                    EncoderRuleSeverity.Warning,
                    $"VideoOutput[{i}]",
                    $"Duplicate variant — same codec, resolution, bitrate, CRF, and bit depth "
                        + $"as an earlier output. Encoding the same variant twice wastes CPU/GPU "
                        + $"and produces duplicate files. Remove one.",
                    $"Remove `VideoOutput[{i}]` — it is identical to an earlier entry in the ladder."
                );
            }
        }
    }

    private static void ValidateMonotonicLadder(EncodingProfile profile, RuleSink sink)
    {
        // Only meaningful on multi-variant ABR containers.
        if (
            profile.VideoOutputs.Length < 2
            || (profile.Format != OutputFormat.Hls && profile.Format != OutputFormat.Dash)
        )
            return;

        // Rank by resolution; bitrates should be monotonically non-decreasing
        // with resolution. A 480p variant at higher bitrate than 720p means
        // the ABR picker has no reason to ever switch up — broken ladder.
        List<(int Pixels, int Bitrate, int Index)> rungs = [];
        for (int i = 0; i < profile.VideoOutputs.Length; i++)
        {
            VideoOutput v = profile.VideoOutputs[i];
            if (v.BitrateKbps <= 0)
                continue;
            int h = v.Height ?? v.Width * 9 / 16;
            rungs.Add((v.Width * h, v.BitrateKbps, i));
        }

        if (rungs.Count < 2)
            return;

        List<(int Pixels, int Bitrate, int Index)> sorted = rungs.OrderBy(r => r.Pixels).ToList();

        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].Bitrate < sorted[i - 1].Bitrate)
            {
                sink.Add(
                    EncoderRuleId.LadderInverted,
                    EncoderRuleSeverity.Warning,
                    $"VideoOutput[{sorted[i].Index}].BitrateKbps",
                    $"ABR ladder is inverted — a higher-resolution variant has a LOWER "
                        + $"bitrate than a smaller one. Players won't switch up to it. "
                        + $"Ensure bitrate increases with resolution.",
                    $"Raise `VideoOutput[{sorted[i].Index}].BitrateKbps` above the bitrate of the lower-resolution variant below it in the ladder."
                );
                break; // One warning is enough; more would be noise.
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Codec level → max luma-samples-per-frame tables
    // ──────────────────────────────────────────────────────────────────────────

    private static long? CodecLevelLimit(VideoCodecType codec, string level) =>
        codec switch
        {
            VideoCodecType.H264 => H264LevelLimit(level),
            VideoCodecType.H265 => H265LevelLimit(level),
            VideoCodecType.Vp9 => Vp9LevelLimit(level),
            _ => null, // AV1 levels not enforced yet (niche hardware decoders).
        };

    // H.264 MaxFS × 256 (16x16 macroblock → pixel) per Annex A level table.
    // Approximate — skip the frame-rate axis and just pin worst-case pixels.
    private static long? H264LevelLimit(string level) =>
        level switch
        {
            "1" or "1.0" => 99L * 256,
            "1b" or "1.0b" => 99L * 256,
            "1.1" => 396L * 256,
            "1.2" or "1.3" => 396L * 256,
            "2" or "2.0" => 396L * 256,
            "2.1" => 792L * 256,
            "2.2" => 1620L * 256,
            "3" or "3.0" => 1620L * 256,
            "3.1" => 3600L * 256,
            "3.2" => 5120L * 256,
            "4" or "4.0" => 8192L * 256,
            "4.1" => 8192L * 256,
            "4.2" => 8704L * 256,
            "5" or "5.0" => 22080L * 256,
            "5.1" => 36864L * 256,
            "5.2" => 36864L * 256,
            "6" or "6.0" => 139264L * 256,
            "6.1" => 139264L * 256,
            "6.2" => 139264L * 256,
            _ => null,
        };

    // H.265 (HEVC) MaxLumaPs per Table A.1 / A.6 (main/main10 profile).
    private static long? H265LevelLimit(string level) =>
        level switch
        {
            "1" or "1.0" => 36864L,
            "2" or "2.0" => 122880L,
            "2.1" => 245760L,
            "3" or "3.0" => 552960L,
            "3.1" => 983040L,
            "4" or "4.0" => 2228224L,
            "4.1" => 2228224L,
            "5" or "5.0" => 8912896L,
            "5.1" => 8912896L,
            "5.2" => 8912896L,
            "6" or "6.0" => 35651584L,
            "6.1" => 35651584L,
            "6.2" => 35651584L,
            _ => null,
        };

    // VP9 Level table (max luma samples per frame).
    private static long? Vp9LevelLimit(string level) =>
        level switch
        {
            "1" or "1.0" => 36864L,
            "1.1" => 73728L,
            "2" or "2.0" => 122880L,
            "2.1" => 245760L,
            "3" or "3.0" => 552960L,
            "3.1" => 983040L,
            "4" or "4.0" => 2228224L,
            "4.1" => 2228224L,
            "5" or "5.0" => 8912896L,
            "5.1" => 8912896L,
            "5.2" => 8912896L,
            "6" or "6.0" => 35651584L,
            "6.1" => 35651584L,
            "6.2" => 35651584L,
            _ => null,
        };

    private static string FormatPixels(long pixels)
    {
        // Best-guess display — prefer familiar resolution labels when the
        // cap matches a common one; fall back to a pixel count.
        return pixels switch
        {
            >= 33_177_600 => "8K (7680x4320)",
            >= 8_912_000 => "4K (3840x2160)",
            >= 2_228_000 => "1080p (1920x1080)",
            >= 921_000 => "720p (1280x720)",
            >= 414_000 => "480p (720x480)",
            _ => $"{pixels:N0} pixels",
        };
    }

    private void ValidateTenBitFeasibility(VideoOutput output, string prefix, RuleSink sink)
    {
        ICodecDefinition definition = codecRegistry.GetVideoDefinition(output.Codec);

        // Split the codec's encoders into "can do 10-bit" vs "can't". If every
        // encoder supports 10-bit, nothing to warn. If some can and some can't,
        // warn that hardware selection may silently downgrade to 8-bit.
        bool anyHwSupports10Bit = definition
            .Encoders.Where(e => e.RequiredVendor is not null)
            .Any(e => e.Supports10Bit);
        bool allHwLack10Bit = definition
            .Encoders.Where(e => e.RequiredVendor is not null)
            .All(e => !e.Supports10Bit);

        if (allHwLack10Bit)
        {
            // H264 is the canonical case — every H264 hardware encoder
            // (NVENC/AMF/QSV/VAAPI/VideoToolbox) rejects 10-bit.
            sink.Add(
                EncoderRuleId.BitDepthNoHardwareSupport,
                EncoderRuleSeverity.Warning,
                $"{prefix}.TenBit",
                $"10-bit {output.Codec} has no hardware encoder support "
                    + "(every vendor's H.264/HEVC hardware path is 8-bit for this codec). "
                    + "Output will be software-encoded or silently downgraded to 8-bit.",
                $"Set `{prefix}.TenBit` to false, or switch to H.265/AV1 which have hardware 10-bit support on modern GPUs."
            );
        }
        else if (!anyHwSupports10Bit)
        {
            // Codec has no hardware encoders at all (VP9 on non-Intel, etc.)
            // — this is a speed/CPU warning, not a correctness issue.
            sink.Add(
                EncoderRuleId.BitDepthNoHardwareSupport,
                EncoderRuleSeverity.Warning,
                $"{prefix}.TenBit",
                $"10-bit {output.Codec} will be software-encoded (no hardware encoders "
                    + "available for this codec).",
                $"Accept software encoding for `{prefix}.TenBit`, or switch to a codec with hardware 10-bit support."
            );
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // HLS fMP4 codec compatibility
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the profile targets HLS with fMP4 segments, only codecs that map
    /// cleanly into an ISO BMFF (fMP4) box are valid on the video side.
    /// H.264, HEVC, and AV1 all have registered codec boxes; VP9 does not —
    /// ffmpeg will produce a non-compliant or unplayable stream.
    /// </summary>
    private static void ValidateHlsFmp4CodecCompatibility(EncodingProfile profile, RuleSink sink)
    {
        if (profile.Format != OutputFormat.Hls)
            return;

        string? segmentType = profile.HlsOptions?.SegmentType;
        if (segmentType is null || !segmentType.Equals("fmp4", StringComparison.OrdinalIgnoreCase))
            return;

        for (int i = 0; i < profile.VideoOutputs.Length; i++)
        {
            VideoOutput output = profile.VideoOutputs[i];

            bool supported =
                output.Codec == VideoCodecType.H264
                || output.Codec == VideoCodecType.H265
                || output.Codec == VideoCodecType.Av1;

            if (!supported)
            {
                sink.Add(
                    EncoderRuleId.HlsFmp4CodecMismatch,
                    EncoderRuleSeverity.Error,
                    $"video_outputs[{i}].codec",
                    $"{output.Codec} cannot be muxed into fMP4 HLS segments. fMP4 only supports "
                        + "H.264, HEVC, or AV1 — VP9 has no registered ISO BMFF codec box and "
                        + "produces a non-compliant or unplayable stream.",
                    "fMP4 segments only support H.264, HEVC, or AV1 — switch to mpegts segments or pick a supported codec."
                );
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Audio output rules
    // ──────────────────────────────────────────────────────────────────────────

    private void ValidateAudioOutputs(EncodingProfile profile, RuleSink sink)
    {
        for (int i = 0; i < profile.AudioOutputs.Length; i++)
        {
            AudioOutput output = profile.AudioOutputs[i];
            string prefix = $"AudioOutput[{i}]";

            AudioEncoderInfo encoder = codecRegistry.GetAudioEncoder(output.Codec);

            if (!encoder.IsLossless && output.BitrateKbps <= 0)
            {
                sink.Add(
                    EncoderRuleId.AudioCodecContainerMismatch,
                    EncoderRuleSeverity.Error,
                    $"{prefix}.BitrateKbps",
                    $"BitrateKbps must be > 0 for lossy codec {output.Codec}.",
                    $"Set `{prefix}.BitrateKbps` to a positive value (e.g. 192 for AAC stereo)."
                );
            }

            if (!encoder.Channels.Contains(output.Channels))
            {
                sink.Add(
                    EncoderRuleId.AudioCodecContainerMismatch,
                    EncoderRuleSeverity.Error,
                    $"{prefix}.Channels",
                    $"{output.Channels} channels is not supported by {output.Codec}. Supported: [{string.Join(", ", encoder.Channels)}].",
                    $"Set `{prefix}.Channels` to one of: {string.Join(", ", encoder.Channels)}."
                );
            }

            if (!encoder.SampleRates.Contains(output.SampleRateHz))
            {
                sink.Add(
                    EncoderRuleId.AudioCodecContainerMismatch,
                    EncoderRuleSeverity.Error,
                    $"{prefix}.SampleRateHz",
                    $"Sample rate {output.SampleRateHz} Hz is not supported by {output.Codec}. Supported: [{string.Join(", ", encoder.SampleRates)}].",
                    $"Set `{prefix}.SampleRateHz` to one of: {string.Join(", ", encoder.SampleRates)} Hz."
                );
            }

            if (!encoder.IsLossless && output.BitrateKbps > 0)
            {
                if (
                    output.BitrateKbps < encoder.MinBitrateKbps
                    || output.BitrateKbps > encoder.MaxBitrateKbps
                )
                {
                    sink.Add(
                        EncoderRuleId.AudioCodecContainerMismatch,
                        EncoderRuleSeverity.Error,
                        $"{prefix}.BitrateKbps",
                        $"BitrateKbps {output.BitrateKbps} is outside the valid range [{encoder.MinBitrateKbps}, {encoder.MaxBitrateKbps}] for {output.Codec}.",
                        $"Set `{prefix}.BitrateKbps` to a value between {encoder.MinBitrateKbps} and {encoder.MaxBitrateKbps} kbps."
                    );
                }

                ValidateBitrateLadder(output, encoder, prefix, sink);
                ValidateBitrateForChannelCount(output, encoder, prefix, sink);
            }

            ValidateCustomArgumentsReservedFlags(output.CustomArguments, prefix, sink);
        }
    }

    /// <summary>
    /// Codecs with a strict bitrate ladder (AC3 is the canonical case) round
    /// down silently when the user supplies an off-ladder value — the output
    /// file's bitrate ends up lower than configured with no indication. Warn
    /// so the user picks a valid rung intentionally.
    /// </summary>
    private static void ValidateBitrateLadder(
        AudioOutput output,
        AudioEncoderInfo encoder,
        string prefix,
        RuleSink sink
    )
    {
        if (encoder.ValidBitrateLadder is null || encoder.ValidBitrateLadder.Length == 0)
            return;

        if (Array.IndexOf(encoder.ValidBitrateLadder, output.BitrateKbps) >= 0)
            return; // Exact match — nothing to warn about.

        // Find the nearest valid rungs so the message is actionable.
        int nearestBelow = 0;
        int nearestAbove = 0;
        foreach (int rung in encoder.ValidBitrateLadder)
        {
            if (rung <= output.BitrateKbps && rung > nearestBelow)
                nearestBelow = rung;
            if (rung >= output.BitrateKbps && (nearestAbove == 0 || rung < nearestAbove))
                nearestAbove = rung;
        }

        string suggestion =
            nearestAbove > 0 ? $"{nearestBelow} or {nearestAbove}" : $"{nearestBelow}";

        // Use the codec-specific ID: AC3 → AudioAc3OffLadderBitrate, EAC3 → AudioEac3OffLadderBitrate
        string ruleId =
            encoder.CodecType == AudioCodecType.Eac3
                ? EncoderRuleId.AudioEac3OffLadderBitrate
                : EncoderRuleId.AudioAc3OffLadderBitrate;

        sink.Add(
            ruleId,
            EncoderRuleSeverity.Warning,
            $"{prefix}.BitrateKbps",
            $"{output.Codec} requires an exact bitrate from its ladder. {output.BitrateKbps} "
                + $"kbps will be silently rounded down to {nearestBelow} kbps by the encoder. "
                + $"Use {suggestion} kbps to get the bitrate you asked for.",
            $"Change `{prefix}.BitrateKbps` to {suggestion} kbps."
        );
    }

    /// <summary>
    /// Channel count drives the minimum bitrate needed for acceptable quality.
    /// 5.1 at 128 kbps is audibly compressed; stereo at 320 kbps AAC is
    /// wasted bandwidth (plateau is ~192-256). Best-practice guidance, not
    /// hard blocks — users who know what they're doing can ignore.
    /// </summary>
    private static void ValidateBitrateForChannelCount(
        AudioOutput output,
        AudioEncoderInfo encoder,
        string prefix,
        RuleSink sink
    )
    {
        // Dolby's own guidance for AC3/EAC3 5.1: 384 kbps minimum.
        bool isSurroundDolby =
            output.Channels >= 6 && encoder.CodecType is AudioCodecType.Ac3 or AudioCodecType.Eac3;
        if (isSurroundDolby && output.BitrateKbps < 384)
        {
            sink.Add(
                EncoderRuleId.AudioCodecContainerMismatch,
                EncoderRuleSeverity.Warning,
                $"{prefix}.BitrateKbps",
                $"{output.Codec} 5.1/7.1 below 384 kbps produces audibly compressed audio. "
                    + "Dolby-recommended minimum for surround is 384 kbps; use 448 or 640 for "
                    + "reference quality. Lower values work but dialog and transients suffer.",
                $"Raise `{prefix}.BitrateKbps` to at least 384 kbps for surround Dolby audio (448 or 640 recommended)."
            );
        }

        // AAC stereo plateau: bitrate above 256 gives diminishing returns.
        bool isStereoAac = output.Channels <= 2 && encoder.CodecType == AudioCodecType.Aac;
        if (isStereoAac && output.BitrateKbps > 256)
        {
            sink.Add(
                EncoderRuleId.AudioCodecContainerMismatch,
                EncoderRuleSeverity.Warning,
                $"{prefix}.BitrateKbps",
                $"AAC stereo at {output.BitrateKbps} kbps is wasted bandwidth — AAC quality "
                    + "plateaus around 192-256 kbps for stereo. Use FLAC for archival or stay "
                    + "at 256 kbps for streaming.",
                $"Lower `{prefix}.BitrateKbps` to 192 or 256 kbps for AAC stereo."
            );
        }

        // AAC 5.1/7.1 below 256 is compressed; libfdk_aac sweet spot is
        // 256-384 for 5.1.
        bool isSurroundAac = output.Channels >= 6 && encoder.CodecType == AudioCodecType.Aac;
        if (isSurroundAac && output.BitrateKbps < 256)
        {
            sink.Add(
                EncoderRuleId.AudioCodecContainerMismatch,
                EncoderRuleSeverity.Warning,
                $"{prefix}.BitrateKbps",
                $"AAC {output.Channels}ch at {output.BitrateKbps} kbps is under-provisioned — "
                    + "recommended minimum for AAC surround is 256 kbps (sweet spot 256-384).",
                $"Raise `{prefix}.BitrateKbps` to at least 256 kbps for AAC surround."
            );
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Format / container compatibility rules
    // ──────────────────────────────────────────────────────────────────────────

    private static void ValidateFormatCompatibility(EncodingProfile profile, RuleSink sink)
    {
        switch (profile.Format)
        {
            case OutputFormat.Hls:
                ValidateHlsCompatibility(profile, sink);
                ValidateSegmentKeyframeAlignment(profile, sink);
                break;

            case OutputFormat.Mp4:
                ValidateMp4Compatibility(profile, sink);
                break;

            case OutputFormat.Dash:
                ValidateDashCompatibility(profile, sink);
                ValidateSegmentKeyframeAlignment(profile, sink);
                break;

            case OutputFormat.Mkv:
                // MKV allows everything — no restrictions
                break;

            case OutputFormat.Mp3:
            case OutputFormat.Flac:
            case OutputFormat.Ogg:
                ValidateAudioOnlyCompatibility(profile, sink);
                break;
        }
    }

    /// <summary>
    /// HLS / DASH segments should start on a keyframe. If segment duration
    /// is not an integer multiple of the keyframe interval, the encoder
    /// either inserts unexpected keyframes (raising bitrate) or segments
    /// drift in duration — players re-buffer on every drift. Warn so the
    /// profile author knows before shipping a broken ladder.
    /// </summary>
    private static void ValidateSegmentKeyframeAlignment(EncodingProfile profile, RuleSink sink)
    {
        int segmentSeconds = profile.SegmentDurationSeconds;
        if (segmentSeconds <= 0)
            return;

        for (int i = 0; i < profile.VideoOutputs.Length; i++)
        {
            VideoOutput output = profile.VideoOutputs[i];
            int keyInt = output.KeyframeIntervalSeconds;

            if (keyInt <= 0)
                continue;

            if (segmentSeconds % keyInt != 0)
            {
                sink.Add(
                    EncoderRuleId.HlsKeyframeSegmentMisalignment,
                    EncoderRuleSeverity.Warning,
                    $"VideoOutput[{i}].KeyframeIntervalSeconds",
                    $"KeyframeIntervalSeconds ({keyInt}s) does not evenly divide "
                        + $"SegmentDurationSeconds ({segmentSeconds}s). Segments may start "
                        + $"mid-GOP, causing extra keyframes or drift in segment length. "
                        + $"Use a key interval that divides the segment duration (e.g. 2s "
                        + $"key / 6s segment).",
                    $"Set `VideoOutput[{i}].KeyframeIntervalSeconds` to a value that evenly divides {segmentSeconds} (e.g. {segmentSeconds / 2} or {segmentSeconds / 3})."
                );
            }
        }
    }

    private static void ValidateAudioOnlyCompatibility(EncodingProfile profile, RuleSink sink)
    {
        if (profile.VideoOutputs.Length > 0)
        {
            sink.Add(
                EncoderRuleId.CodecContainerMismatch,
                EncoderRuleSeverity.Error,
                "VideoOutputs",
                $"{profile.Format} is an audio-only container; video outputs are not supported.",
                "Remove all entries from `VideoOutputs`, or change `Format` to HLS, DASH, MKV, or MP4."
            );
        }

        if (profile.SubtitleOutputs.Length > 0)
        {
            sink.Add(
                EncoderRuleId.SubtitlesContainerIncompatible,
                EncoderRuleSeverity.Error,
                "SubtitleOutputs",
                $"{profile.Format} has no subtitle support; drop the subtitle outputs.",
                "Remove all entries from `SubtitleOutputs`."
            );
        }

        if (profile.AudioOutputs.Length == 0)
        {
            sink.Add(
                EncoderRuleId.AudioCodecContainerMismatch,
                EncoderRuleSeverity.Error,
                "AudioOutputs",
                $"{profile.Format} requires exactly one audio output.",
                "Add exactly one entry to `AudioOutputs` using the matching codec."
            );
        }
        else if (profile.AudioOutputs.Length > 1)
        {
            sink.Add(
                EncoderRuleId.AudioCodecContainerMismatch,
                EncoderRuleSeverity.Warning,
                "AudioOutputs",
                $"{profile.Format} produces a single file; additional audio outputs are ignored.",
                "Remove extra entries from `AudioOutputs` — only the first is used."
            );
        }

        if (profile.Format == OutputFormat.Mp3 && profile.AudioOutputs.Length > 0)
        {
            AudioCodecType codec = profile.AudioOutputs[0].Codec;
            if (codec != AudioCodecType.Mp3)
            {
                sink.Add(
                    EncoderRuleId.AudioCodecContainerMismatch,
                    EncoderRuleSeverity.Error,
                    "AudioOutputs[0].Codec",
                    $"MP3 container requires the MP3 codec; got {codec}.",
                    "Set `AudioOutputs[0].Codec` to `Mp3`."
                );
            }
        }

        if (profile.Format == OutputFormat.Flac && profile.AudioOutputs.Length > 0)
        {
            AudioCodecType codec = profile.AudioOutputs[0].Codec;
            if (codec != AudioCodecType.Flac)
            {
                sink.Add(
                    EncoderRuleId.AudioCodecContainerMismatch,
                    EncoderRuleSeverity.Error,
                    "AudioOutputs[0].Codec",
                    $"FLAC container requires the FLAC codec; got {codec}.",
                    "Set `AudioOutputs[0].Codec` to `Flac`."
                );
            }
        }

        if (profile.Format == OutputFormat.Ogg && profile.AudioOutputs.Length > 0)
        {
            AudioCodecType codec = profile.AudioOutputs[0].Codec;
            AudioCodecType[] allowed =
            [
                AudioCodecType.Vorbis,
                AudioCodecType.Opus,
                AudioCodecType.Flac,
            ];
            if (Array.IndexOf(allowed, codec) < 0)
            {
                sink.Add(
                    EncoderRuleId.AudioCodecContainerMismatch,
                    EncoderRuleSeverity.Error,
                    "AudioOutputs[0].Codec",
                    $"Ogg container accepts Vorbis, Opus, or FLAC; got {codec}.",
                    "Set `AudioOutputs[0].Codec` to `Vorbis`, `Opus`, or `Flac`."
                );
            }
        }
    }

    private static void ValidateHlsCompatibility(EncodingProfile profile, RuleSink sink)
    {
        VideoCodecType[] allowedVideoCodecs =
        [
            VideoCodecType.H264,
            VideoCodecType.H265,
            VideoCodecType.Av1,
        ];
        AudioCodecType[] allowedAudioCodecs =
        [
            AudioCodecType.Aac,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
            AudioCodecType.Opus,
        ];

        for (int i = 0; i < profile.VideoOutputs.Length; i++)
        {
            VideoOutput output = profile.VideoOutputs[i];
            if (!allowedVideoCodecs.Contains(output.Codec))
            {
                sink.Add(
                    EncoderRuleId.CodecContainerMismatch,
                    EncoderRuleSeverity.Error,
                    $"VideoOutput[{i}].Codec",
                    $"{output.Codec} is not supported in HLS. Allowed: H264, H265, AV1.",
                    $"Change `VideoOutput[{i}].Codec` to H264, H265, or AV1."
                );
            }
        }

        for (int i = 0; i < profile.AudioOutputs.Length; i++)
        {
            AudioOutput output = profile.AudioOutputs[i];
            if (!allowedAudioCodecs.Contains(output.Codec))
            {
                sink.Add(
                    EncoderRuleId.AudioCodecContainerMismatch,
                    EncoderRuleSeverity.Error,
                    $"AudioOutput[{i}].Codec",
                    $"{output.Codec} is not supported in HLS. Allowed: AAC, AC3, EAC3, Opus.",
                    $"Change `AudioOutput[{i}].Codec` to Aac, Ac3, Eac3, or Opus."
                );
            }
        }
    }

    private static void ValidateMp4Compatibility(EncodingProfile profile, RuleSink sink)
    {
        // H264/H265/AV1 are standard; VP9 in MP4 is non-standard → Warning
        for (int i = 0; i < profile.VideoOutputs.Length; i++)
        {
            VideoOutput output = profile.VideoOutputs[i];
            if (output.Codec == VideoCodecType.Vp9)
            {
                sink.Add(
                    EncoderRuleId.CodecContainerMismatch,
                    EncoderRuleSeverity.Warning,
                    $"VideoOutput[{i}].Codec",
                    "VP9 in MP4 container is non-standard and may have limited player support.",
                    $"Change `VideoOutput[{i}].Codec` to H264, H265, or AV1 for broadest MP4 compatibility."
                );
            }
        }

        // MP4 is a single-file format — additional variants are silently
        // dropped by Mp4OutputStrategy (it only mixes the first video output
        // into the container). Use HLS or DASH for adaptive ladders instead.
        if (profile.VideoOutputs.Length > 1)
        {
            sink.Add(
                EncoderRuleId.CodecContainerMismatch,
                EncoderRuleSeverity.Warning,
                "VideoOutputs",
                "MP4 is a single-file format; only the first video output is muxed. Use HLS or DASH for multi-variant ladders.",
                "Remove extra `VideoOutputs` entries, or change `Format` to HLS or DASH."
            );
        }
    }

    private static void ValidateDashCompatibility(EncodingProfile profile, RuleSink sink)
    {
        VideoCodecType[] allowedVideoCodecs =
        [
            VideoCodecType.H264,
            VideoCodecType.H265,
            VideoCodecType.Av1,
            VideoCodecType.Vp9,
        ];
        AudioCodecType[] allowedAudioCodecs =
        [
            AudioCodecType.Aac,
            AudioCodecType.Opus,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
        ];

        for (int i = 0; i < profile.VideoOutputs.Length; i++)
        {
            VideoOutput output = profile.VideoOutputs[i];
            if (!allowedVideoCodecs.Contains(output.Codec))
            {
                sink.Add(
                    EncoderRuleId.CodecContainerMismatch,
                    EncoderRuleSeverity.Error,
                    $"VideoOutput[{i}].Codec",
                    $"{output.Codec} is not supported in DASH. Allowed: H264, H265, AV1, VP9.",
                    $"Change `VideoOutput[{i}].Codec` to H264, H265, AV1, or VP9."
                );
            }
        }

        for (int i = 0; i < profile.AudioOutputs.Length; i++)
        {
            AudioOutput output = profile.AudioOutputs[i];
            if (!allowedAudioCodecs.Contains(output.Codec))
            {
                sink.Add(
                    EncoderRuleId.AudioCodecContainerMismatch,
                    EncoderRuleSeverity.Error,
                    $"AudioOutput[{i}].Codec",
                    $"{output.Codec} is not supported in DASH. Allowed: AAC, Opus, AC3, EAC3.",
                    $"Change `AudioOutput[{i}].Codec` to Aac, Opus, Ac3, or Eac3."
                );
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Subtitle compatibility rules
    // ──────────────────────────────────────────────────────────────────────────

    private static void ValidateSubtitleCompatibility(EncodingProfile profile, RuleSink sink)
    {
        for (int i = 0; i < profile.SubtitleOutputs.Length; i++)
        {
            SubtitleOutput output = profile.SubtitleOutputs[i];
            string prefix = $"SubtitleOutput[{i}]";

            ValidateCustomArgumentsReservedFlags(output.CustomArguments, prefix, sink);

            if (output.Mode != SubtitleMode.Extract)
            {
                // BurnIn and PassThrough are not format-restricted at this level
                continue;
            }

            switch (profile.Format)
            {
                case OutputFormat.Hls:
                    if (
                        output.Codec
                        is SubtitleCodecType.WebVtt
                            or SubtitleCodecType.Ass
                            or SubtitleCodecType.Copy
                    )
                    {
                        // SubtitleExtractor handles each path: WebVTT writes
                        // segmented .vtt + sidecar (HLS-spec), ASS writes
                        // direct .ass that the master URI references, Copy
                        // preserves source format byte-for-byte.
                    }
                    else
                    {
                        sink.Add(
                            EncoderRuleId.SubtitlesContainerIncompatible,
                            EncoderRuleSeverity.Error,
                            $"SubtitleOutput[{i}].Codec",
                            $"{output.Codec} is not supported in HLS Extract mode. Use WebVTT, Ass, or Copy.",
                            $"Change `SubtitleOutput[{i}].Codec` to `WebVtt`, `Ass`, or `Copy`."
                        );
                    }
                    break;

                case OutputFormat.Mp4:
                    if (
                        output.Codec != SubtitleCodecType.WebVtt
                        && output.Codec != SubtitleCodecType.Srt
                    )
                    {
                        sink.Add(
                            EncoderRuleId.SubtitlesContainerIncompatible,
                            EncoderRuleSeverity.Error,
                            $"SubtitleOutput[{i}].Codec",
                            $"{output.Codec} is not supported in MP4 Extract mode. Use WebVTT or SRT.",
                            $"Change `SubtitleOutput[{i}].Codec` to `WebVtt` or `Srt`."
                        );
                    }
                    break;

                case OutputFormat.Mkv:
                case OutputFormat.Dash:
                    // All subtitle codecs allowed
                    break;
            }
        }
    }
}
