using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;

namespace NoMercy.Encoder.Profiles;

/// <summary>
///     Structured rule emitter for encoding profiles. Returns an
///     <see cref="ValidationEnvelope"/> whose entries are typed
///     <see cref="EncoderRule"/> records with stable IDs from
///     <see cref="EncoderRuleId"/>, the spec field path, a human-readable message,
///     and a concrete fix.
///     <para>
///         Replaces the string-based <see cref="ProfileValidator.Validate"/> for new callers.
///         The legacy method stays for backward compatibility while consumers migrate.
///     </para>
/// </summary>
public static class ProfileRuleValidator
{
    /// <summary>
    ///     Run every static rule against the profile alone (no source media). Use
    ///     <see cref="ValidateWithSource"/> to layer source-dependent rules on top
    ///     (HFR level cap, stereoscopic, spherical).
    /// </summary>
    public static ValidationEnvelope Validate(EncodingProfile profile)
    {
        List<EncoderRule> rules = [];

        EmitProfileNameMissing(profile, rules);
        EmitProfileNoOutputs(profile, rules);
        EmitVideoWidthInvalid(profile, rules);
        EmitVideoHeightInvalid(profile, rules);
        EmitVideoRateControlMissing(profile, rules);
        EmitVideoRateControlConflict(profile, rules);
        EmitCodecContainerMismatch(profile, rules);
        EmitAudioCodecContainerMismatch(profile, rules);
        EmitHlsFmp4CodecMismatch(profile, rules);
        EmitLadderDuplicateVariant(profile, rules);
        EmitProfileLevelResolutionMismatch(profile, rules);
        EmitBitrateTooLowForResolution(profile, rules);
        EmitCrfOutOfTypicalRange(profile, rules);
        EmitHlsKeyframeSegmentMisalignment(profile, rules);
        EmitLadderInverted(profile, rules);
        EmitAudioAc3OffLadderBitrate(profile, rules);
        EmitSubtitlesContainerIncompatible(profile, rules);
        EmitSubtitlesBurnInPermanent(profile, rules);
        EmitSubtitlesAssNeedsCapableClient(profile, rules);
        EmitHdrInverseTonemapUnsupported(profile, rules);
        EmitCustomArgsReservedFlag(profile, rules);
        EmitDrmHttpNotHttps(profile, rules);
        EmitDrmKeyMissing(profile, rules);

        return ValidationEnvelope.FromRules(rules);
    }

    /// <summary>
    ///     The full set of ffmpeg flags whose values are derived from the profile's typed fields
    ///     and must not be hand-overridden via <see cref="EncodingProfile.CustomArguments"/>.
    ///     Letting users smuggle these through silently desyncs the validator from what ffmpeg
    ///     actually runs (the profile says one thing, the encode does another). Spec part 04
    ///     §"reserved flags".
    /// </summary>
    public static readonly IReadOnlySet<string> ReservedFlags = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        // Codec selection — driven by VideoOutput.Codec / AudioOutput.Codec / SubtitleOutput.Codec
        "-c:v",
        "-c:a",
        "-c:s",
        "-vcodec",
        "-acodec",
        "-scodec",
        // Container muxer — driven by EncodingProfile.Container
        "-f",
        // Encoder tuning — driven by VideoOutput.Preset
        "-preset",
        // Hardware pipeline — chosen by HardwarePreferenceResolver based on the profile policy
        "-init_hw_device",
        "-filter_hw_device",
        "-hwaccel",
        "-hwaccel_output_format",
        // Stream mapping — derived from the policy + ladder + audio/subtitle selection
        "-map",
        "-map_metadata",
        // Filter graph — built by FilterGraphBuilder from crop / scale / tonemap settings
        "-vf",
        "-af",
        "-filter_complex",
        // HLS muxer arguments — driven by HlsConfig + SegmentDurationSeconds
        "-hls_time",
        "-hls_segment_filename",
        "-hls_playlist_type",
        "-hls_segment_type",
    };

    /// <summary>
    ///     Layer source-dependent rules on top of <see cref="Validate"/>. Returns a combined envelope.
    /// </summary>
    public static ValidationEnvelope ValidateWithSource(EncodingProfile profile, MediaInfo source)
    {
        ValidationEnvelope baseEnvelope = Validate(profile);
        List<EncoderRule> sourceRules = [];

        EmitLevelFrameRateCapExceeded(profile, source, sourceRules);
        EmitSourceVariableFrameRate(profile, source, sourceRules);
        EmitSourceDolbyVisionWillBeStripped(profile, source, sourceRules);
        EmitSourceUpscalingDetected(profile, source, sourceRules);
        EmitStereoscopicSourceUnsupported(profile, source, sourceRules);
        EmitSphericalMetadataWillBeStripped(profile, source, sourceRules);

        return ValidationEnvelope.FromRules(
            baseEnvelope.Errors.Concat(baseEnvelope.Warnings).Concat(sourceRules)
        );
    }

    // ----------------------------------------------------------------------
    // Structural profile rules
    // ----------------------------------------------------------------------

    private static void EmitProfileNameMissing(EncodingProfile profile, List<EncoderRule> rules)
    {
        if (!string.IsNullOrWhiteSpace(profile.Name))
            return;

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.ProfileNameMissing,
                Severity: EncoderRuleSeverity.Error,
                Field: "name",
                Message: "Profile name is required so operators can identify the preset in the dashboard.",
                Fix: "Set a non-empty profile.name (e.g. \"General 1080p Fast\")."
            )
        );
    }

    private static void EmitProfileNoOutputs(EncodingProfile profile, List<EncoderRule> rules)
    {
        // A profile must produce at least one output stream — every encode call would be a no-op.
        bool hasVideo = profile.Video is { Policy: not StreamPolicy.Omit };
        bool hasAudio = profile.Audio.Any(a => a.Policy != StreamPolicy.Omit);
        bool hasSubtitles = profile.Subtitles.Any(s => s.Policy != SubtitlePolicy.Omit);

        if (hasVideo || hasAudio || hasSubtitles)
            return;

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.ProfileNoOutputs,
                Severity: EncoderRuleSeverity.Error,
                Field: "outputs",
                Message: "Profile declares no video, audio, or subtitle outputs; an encode run "
                    + "would produce no streams.",
                Fix: "Add at least one VideoOutput, AudioOutput, or SubtitleOutput with policy != Skip / Omit."
            )
        );
    }

    private static void EmitVideoWidthInvalid(EncodingProfile profile, List<EncoderRule> rules)
    {
        if (profile.Video is not { Policy: StreamPolicy.Transcode } video || video.Width > 0)
            return;

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.VideoWidthInvalid,
                Severity: EncoderRuleSeverity.Error,
                Field: "video.width",
                Message: $"Video width must be a positive integer (got {video.Width}); the encoder "
                    + "cannot produce a 0-pixel-wide output.",
                Fix: "Set video.width to a positive value (typical values: 854, 1280, 1920, 2560, 3840)."
            )
        );
    }

    private static void EmitVideoHeightInvalid(EncodingProfile profile, List<EncoderRule> rules)
    {
        if (profile.Video is not { Policy: StreamPolicy.Transcode } video || video.Height is null)
            return;

        if (video.Height.Value > 0)
            return;

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.VideoHeightInvalid,
                Severity: EncoderRuleSeverity.Error,
                Field: "video.height",
                Message: $"Video height must be positive when set (got {video.Height}); "
                    + "leave it null to derive from source aspect ratio.",
                Fix: "Set video.height to a positive integer or null."
            )
        );
    }

    private static void EmitVideoRateControlMissing(
        EncodingProfile profile,
        List<EncoderRule> rules
    )
    {
        // Every transcode needs a quality target — either a CRF value or a bitrate. A profile that
        // pinned RateControlMode.Crf but left Crf at 0 (default) silently encodes near-lossless and
        // produces enormous files. A profile that pinned Vbr / Cbr but left BitrateKbps at 0 has
        // no target at all and the muxer falls back to whatever the encoder default is.
        if (profile.Video is not { Policy: StreamPolicy.Transcode } video)
            return;

        bool isCrfMode = video.RateControl == RateControlMode.Crf;
        bool isBitrateMode =
            video.RateControl == RateControlMode.Vbr || video.RateControl == RateControlMode.Cbr;

        bool crfMissing = isCrfMode && video.Crf <= 0;
        bool bitrateMissing = isBitrateMode && video.BitrateKbps <= 0;

        if (!crfMissing && !bitrateMissing)
            return;

        string field = isCrfMode ? "video.crf" : "video.bitrate_kbps";
        string mode = isCrfMode ? "Crf" : video.RateControl.ToString();

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.VideoRateControlMissing,
                Severity: EncoderRuleSeverity.Error,
                Field: field,
                Message: $"Rate control is {mode} but {field} is unset; the encoder has no quality "
                    + "target and would emit a stream nobody asked for.",
                Fix: isCrfMode
                    ? "Set video.crf to a value in 17..28 (23 is the typical default)."
                    : "Set video.bitrate_kbps to a positive value matched to the resolution."
            )
        );
    }

    private static void EmitVideoRateControlConflict(
        EncodingProfile profile,
        List<EncoderRule> rules
    )
    {
        if (profile.Video is not { Policy: StreamPolicy.Transcode } video)
            return;

        // ABR / VBR / CBR want a positive bitrate; pure CRF wants the CRF value populated.
        bool isBitrateMode =
            video.RateControl == RateControlMode.Vbr || video.RateControl == RateControlMode.Cbr;
        bool hasBitrate = video.BitrateKbps > 0;
        bool hasCrf = video.Crf > 0;

        if (isBitrateMode && hasCrf && !hasBitrate)
        {
            rules.Add(
                new EncoderRule(
                    Id: EncoderRuleId.VideoRateControlConflict,
                    Severity: EncoderRuleSeverity.Error,
                    Field: "video.rate_control",
                    Message: $"Rate control is {video.RateControl} (bitrate-targeted) but bitrate_kbps "
                        + $"is unset and crf is {video.Crf}; the encoder cannot decide which value to honour.",
                    Fix: "Either set bitrate_kbps and keep rate_control at VBR/CBR, "
                        + "or change rate_control to Crf and clear bitrate_kbps."
                )
            );
        }
    }

    private static void EmitCodecContainerMismatch(EncodingProfile profile, List<EncoderRule> rules)
    {
        if (profile.Video is not { Policy: StreamPolicy.Transcode } video)
            return;

        if (ContainerCompatibility.SupportsVideo(profile.Container, video.Codec))
            return;

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.CodecContainerMismatch,
                Severity: EncoderRuleSeverity.Error,
                Field: "video.codec",
                Message: $"Container {profile.Container} does not support video codec {video.Codec}; "
                    + "the muxer will refuse the stream.",
                Fix: $"Change video.codec or pick a container that supports {video.Codec} "
                    + "(see the codec/container matrix in The Effortless Encoder part 02)."
            )
        );
    }

    private static void EmitAudioCodecContainerMismatch(
        EncodingProfile profile,
        List<EncoderRule> rules
    )
    {
        foreach (AudioOutput audio in profile.Audio.Where(a => a.Policy == StreamPolicy.Transcode))
        {
            if (ContainerCompatibility.SupportsAudio(profile.Container, audio.Codec))
                continue;

            rules.Add(
                new EncoderRule(
                    Id: EncoderRuleId.AudioCodecContainerMismatch,
                    Severity: EncoderRuleSeverity.Error,
                    Field: "audio.codec",
                    Message: $"Container {profile.Container} does not support audio codec {audio.Codec}.",
                    Fix: $"Change audio.codec or pick a container that supports {audio.Codec}."
                )
            );
        }
    }

    private static void EmitHlsFmp4CodecMismatch(EncodingProfile profile, List<EncoderRule> rules)
    {
        // HlsTs only carries H.264 per Apple HLS Authoring Specification §1.5. The codec/container
        // matrix already enforces this at SupportsVideo, but we want a dedicated rule with a stable
        // ID so the dashboard can deep-link the HLS-fmp4 explanation.
        if (
            profile.Container != Container.HlsTs
            || profile.Video is not { Policy: StreamPolicy.Transcode } video
            || video.Codec == VideoCodecType.H264
        )
            return;

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.HlsFmp4CodecMismatch,
                Severity: EncoderRuleSeverity.Error,
                Field: "container",
                Message: $"HLS MPEG-TS only carries H.264 (Apple HLS Authoring §1.5); "
                    + $"video.codec is {video.Codec}.",
                Fix: "Switch container to HlsFmp4 for HEVC / AV1, or change video.codec to H264."
            )
        );
    }

    private static void EmitLadderDuplicateVariant(EncodingProfile profile, List<EncoderRule> rules)
    {
        if (profile.Ladder is not { Mode: LadderMode.Manual, Rungs: { Length: > 1 } rungs })
            return;

        HashSet<string> seen = [];
        for (int i = 0; i < rungs.Length; i++)
        {
            string key =
                $"{rungs[i].Codec}|{rungs[i].Width}x{rungs[i].Height}|{rungs[i].BitrateKbps}";
            if (seen.Add(key))
                continue;

            rules.Add(
                new EncoderRule(
                    Id: EncoderRuleId.LadderDuplicateVariant,
                    Severity: EncoderRuleSeverity.Warning,
                    Field: $"ladder.rungs[{i}]",
                    Message: $"Ladder rung {i} duplicates an earlier rung ({rungs[i].Codec} "
                        + $"{rungs[i].Width}x{rungs[i].Height} @ {rungs[i].BitrateKbps} kbps); "
                        + "the second copy is wasted CPU.",
                    Fix: $"Remove or differentiate rung {i} (change bitrate, resolution, or codec)."
                )
            );
            return;
        }
    }

    private static void EmitSubtitlesAssNeedsCapableClient(
        EncodingProfile profile,
        List<EncoderRule> rules
    )
    {
        // ASS / SSA typesetting renders correctly only on players that ship a libass-backed
        // engine. Most browsers don't, so an HLS extract-mode ASS track is silently fallback-
        // rendered as plain text on Safari / Edge / Firefox without a player extension.
        bool isHlsExtract =
            profile.Container
            is Container.HlsTs
                or Container.HlsFmp4
                or Container.AudioHlsTs
                or Container.AudioHlsFmp4
                or Container.Dash;

        foreach (
            SubtitleOutput subtitle in profile.Subtitles.Where(s =>
                s.Codec == SubtitleCodecType.Ass && s.Policy == SubtitlePolicy.Extract
            )
        )
        {
            if (!isHlsExtract)
                continue;

            rules.Add(
                new EncoderRule(
                    Id: EncoderRuleId.SubtitlesAssNeedsCapableClient,
                    Severity: EncoderRuleSeverity.Info,
                    Field: "subtitles.codec",
                    Message: $"ASS subtitle extracted to {profile.Container} is rendered with typesetting "
                        + "only on players that bundle a libass-compatible engine; most browsers fall "
                        + "back to plain text without positioning, fonts, or effects.",
                    Fix: "Use the SubtitlesOctopus plugin in the web player, ship ASS rendering at the client, "
                        + "or set subtitles.policy to BurnIn for guaranteed-fidelity playback."
                )
            );
            return;
        }
    }

    private static void EmitDrmKeyMissing(EncodingProfile profile, List<EncoderRule> rules)
    {
        // DRM enabled but no key delivery configured: HLS AES-128 needs a key_uri the client can
        // fetch the raw key from, CENC needs an inline key id + value or a license server URL.
        // Without either, packaging fails late.
        if (profile.Drm is null)
            return;

        string scheme = (profile.Drm.Scheme ?? string.Empty).ToLowerInvariant();
        if (scheme is "" or "none")
            return;

        if (profile.Drm.Parameters is null || profile.Drm.Parameters.Count == 0)
        {
            rules.Add(BuildDrmKeyMissingRule(scheme, "drm.parameters"));
            return;
        }

        // Accept conventional key field names — schemes vary.
        bool hasKeyUri = profile.Drm.Parameters.Keys.Any(k =>
            k.Equals("key_uri", StringComparison.OrdinalIgnoreCase)
            || k.Equals("key_url", StringComparison.OrdinalIgnoreCase)
            || k.Equals("license_url", StringComparison.OrdinalIgnoreCase)
            || k.Equals("keyfile", StringComparison.OrdinalIgnoreCase)
            || k.Equals("key_file", StringComparison.OrdinalIgnoreCase)
        );

        if (!hasKeyUri)
            rules.Add(BuildDrmKeyMissingRule(scheme, "drm.parameters"));
    }

    private static EncoderRule BuildDrmKeyMissingRule(string scheme, string field) =>
        new(
            Id: EncoderRuleId.DrmKeyMissing,
            Severity: EncoderRuleSeverity.Error,
            Field: field,
            Message: $"DRM scheme '{scheme}' is enabled but no key delivery URI is configured; "
                + "packaging will fail at encode time.",
            Fix: "Add a key_uri / license_url entry to drm.parameters pointing to "
                + "the key server, or set drm.scheme to 'none' to disable DRM."
        );

    private static void EmitDrmHttpNotHttps(EncodingProfile profile, List<EncoderRule> rules)
    {
        // DRM key URLs must travel over HTTPS — plain HTTP exposes the decryption key to any
        // on-path observer. Spec part 07 §"DRM" + part 10 §"HTTPS enforcement".
        if (profile.Drm?.Parameters is null)
            return;

        foreach ((string key, string value) in profile.Drm.Parameters)
        {
            string normalisedKey = key.ToLowerInvariant();
            bool looksLikeKeyUri =
                normalisedKey is "key_uri" or "key_url" or "keyuri" or "license_url";
            if (!looksLikeKeyUri || string.IsNullOrWhiteSpace(value))
                continue;

            if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                continue;
            if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                rules.Add(
                    new EncoderRule(
                        Id: EncoderRuleId.DrmHttpNotHttps,
                        Severity: EncoderRuleSeverity.Error,
                        Field: $"drm.parameters[{key}]",
                        Message: $"DRM key URI uses plain HTTP ({value}); the decryption key would "
                            + "travel in cleartext to every on-path observer.",
                        Fix: "Switch the key URI scheme to https:// or terminate TLS at a "
                            + "reverse proxy in front of the key endpoint."
                    )
                );
            }
        }
    }

    // ----------------------------------------------------------------------
    // Source-dependent rule emitters
    // ----------------------------------------------------------------------

    private static void EmitSourceVariableFrameRate(
        EncodingProfile profile,
        MediaInfo source,
        List<EncoderRule> rules
    )
    {
        if (!source.IsVariableFrameRate)
            return;
        if (profile.Video is not { Policy: StreamPolicy.Transcode })
            return;

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.SourceVariableFrameRate,
                Severity: EncoderRuleSeverity.Warning,
                Field: "source.frame_rate",
                Message: "Source is variable frame rate (VFR); the encoder will resample to a "
                    + "constant frame rate. Long-duration content can drift up to ~1 second per hour.",
                Fix: "If drift is unacceptable, set video.policy to Copy to preserve the source "
                    + "frame-rate timing, or convert via -fps_mode passthrough manually."
            )
        );
    }

    private static void EmitSourceUpscalingDetected(
        EncodingProfile profile,
        MediaInfo source,
        List<EncoderRule> rules
    )
    {
        // Upscaling a low-res source to a higher target resolution wastes bytes without adding
        // detail. The encoder will produce the larger output but it carries the same information
        // as the source — and a poorly-tuned bitrate ladder can hide quality regressions behind
        // the resolution change.
        if (profile.Video is not { Policy: StreamPolicy.Transcode } video || video.Width <= 0)
            return;
        if (source.VideoStreams.Count == 0)
            return;

        VideoStreamInfo primary = source.VideoStreams[0];
        if (primary.Width <= 0 || video.Width <= primary.Width)
            return;

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.SourceUpscalingDetected,
                Severity: EncoderRuleSeverity.Warning,
                Field: "video.width",
                Message: $"Target width {video.Width} exceeds source width {primary.Width}; "
                    + "the encoder will upscale, adding bytes without adding detail.",
                Fix: $"Lower video.width to {primary.Width} (or below) to avoid wasted bitrate, "
                    + "or accept the upscale if the larger frame is needed for player compatibility."
            )
        );
    }

    private static void EmitSourceDolbyVisionWillBeStripped(
        EncodingProfile profile,
        MediaInfo source,
        List<EncoderRule> rules
    )
    {
        if (source.DolbyVision is null)
            return;
        if (profile.Video is not { Policy: StreamPolicy.Transcode })
            return;

        // Dolby Vision RPU survives only when the encoder + container combo preserves it. Without
        // a DV-capable path the RPU is stripped and the output becomes HDR10 (BL only).
        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.SourceDolbyVisionWillBeStripped,
                Severity: EncoderRuleSeverity.Warning,
                Field: "source.dolby_vision",
                Message: "Source contains Dolby Vision metadata (RPU). The encoder strips the RPU "
                    + "on re-encode; the output retains HDR10 base layer only.",
                Fix: "Set video.policy to Copy to preserve the Dolby Vision stream end-to-end."
            )
        );
    }

    private static void EmitStereoscopicSourceUnsupported(
        EncodingProfile profile,
        MediaInfo source,
        List<EncoderRule> rules
    )
    {
        if (source.StereoMode is null)
            return;
        if (profile.Video is not { Policy: StreamPolicy.Transcode })
            return;

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.StereoscopicSourceUnsupported,
                Severity: EncoderRuleSeverity.Error,
                Field: "source.stereo_mode",
                Message: $"3D source detected (stereo_mode={source.StereoMode}). NoMercy does not "
                    + "support 3D re-encode; the stereo frame layout would be flattened.",
                Fix: "Switch video.policy to Copy to preserve the 3D stream."
            )
        );
    }

    private static void EmitSphericalMetadataWillBeStripped(
        EncodingProfile profile,
        MediaInfo source,
        List<EncoderRule> rules
    )
    {
        if (source.SphericalProjection is null)
            return;
        if (profile.Video is not { Policy: StreamPolicy.Transcode })
            return;

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.SphericalMetadataWillBeStripped,
                Severity: EncoderRuleSeverity.Warning,
                Field: "source.spherical_projection",
                Message: $"VR projection metadata ({source.SphericalProjection}) will be stripped "
                    + "on re-encode; the output plays as a flat panoramic stretch.",
                Fix: "Use a Copy video policy to preserve the spherical metadata box."
            )
        );
    }

    private static void EmitSubtitlesBurnInPermanent(
        EncodingProfile profile,
        List<EncoderRule> rules
    )
    {
        // BurnIn writes subtitles into video pixels permanently. Worth flagging once so users
        // who pick the policy by accident see what they signed up for.
        foreach (
            SubtitleOutput subtitle in profile.Subtitles.Where(s =>
                s.Policy == SubtitlePolicy.BurnIn
            )
        )
        {
            rules.Add(
                new EncoderRule(
                    Id: EncoderRuleId.SubtitlesBurnInPermanent,
                    Severity: EncoderRuleSeverity.Info,
                    Field: "subtitles.policy",
                    Message: $"Subtitle policy BurnIn writes {subtitle.Codec} into video pixels permanently; "
                        + "viewers cannot turn this off.",
                    Fix: "Switch subtitles.policy to Extract or Copy to keep the track toggleable."
                )
            );
            return;
        }
    }

    // ----------------------------------------------------------------------
    // Profile-only rules
    // ----------------------------------------------------------------------

    private static void EmitProfileLevelResolutionMismatch(
        EncodingProfile profile,
        List<EncoderRule> rules
    )
    {
        // Level vs resolution check at profile-save time: assume the spec target frame rate
        // (30 fps for SDR, 60 fps for HFR-marked profiles). Without a source we can only check
        // the static side: width × height × DefaultProfileFps vs the level's MaxLumaSamplesPerSec.
        if (
            profile.Video is not { Policy: StreamPolicy.Transcode } video
            || string.IsNullOrEmpty(video.Level)
            || video.Width <= 0
        )
            return;

        int height = video.Height ?? video.Width * 9 / 16; // fall back to 16:9
        const double assumedFps = 30.0;
        long lumaSamplesPerSec = (long)(video.Width * height * assumedFps);

        CodecLevelFpsCaps.LevelCap? cap = CodecLevelFpsCaps.Lookup(video.Codec, video.Level);
        if (cap is null || lumaSamplesPerSec <= cap.MaxLumaSamplesPerSec)
            return;

        CodecLevelFpsCaps.LevelCap? nextFit = CodecLevelFpsCaps.FindNextFit(
            video.Codec,
            lumaSamplesPerSec
        );

        string fix = nextFit is not null
            ? $"Raise video.level to {nextFit.Level} (supports up to {nextFit.MaxLumaSamplesPerSec:N0} luma samples/sec)."
            : "No standard level supports this resolution at 30 fps.";

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.LevelResolutionMismatch,
                Severity: EncoderRuleSeverity.Error,
                Field: "video.level",
                Message: $"Level {video.Level} cannot sustain {video.Width}x{height} at 30 fps "
                    + $"({lumaSamplesPerSec:N0} luma samples/sec required, "
                    + $"level {video.Level} allows {cap.MaxLumaSamplesPerSec:N0}).",
                Fix: fix
            )
        );
    }

    private static void EmitBitrateTooLowForResolution(
        EncodingProfile profile,
        List<EncoderRule> rules
    )
    {
        // Rough rule of thumb per resolution tier, in kbps. Source: SRGS encoding guidelines.
        if (
            profile.Video is not { Policy: StreamPolicy.Transcode } video
            || (
                video.RateControl != RateControlMode.Vbr && video.RateControl != RateControlMode.Cbr
            )
            || video.BitrateKbps <= 0
            || video.Width <= 0
        )
            return;

        int minBitrate = MinimumBitrateKbpsFor(video.Width);
        if (video.BitrateKbps >= minBitrate)
            return;

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.BitrateTooLowForResolution,
                Severity: EncoderRuleSeverity.Warning,
                Field: "video.bitrate_kbps",
                Message: $"Bitrate {video.BitrateKbps} kbps is below the conservative minimum "
                    + $"({minBitrate} kbps) for {video.Width}-wide output; visible artefacts likely.",
                Fix: $"Raise video.bitrate_kbps to at least {minBitrate}, "
                    + "or switch rate_control to CRF for quality-targeted encoding."
            )
        );
    }

    /// <summary>
    ///     Conservative minimum encoded bitrate for an output width, derived from the SRGS encoding
    ///     guideline ladder. Anything below the minimum is almost certainly artefact-heavy at the
    ///     declared resolution.
    /// </summary>
    public static int MinimumBitrateKbpsFor(int width)
    {
        if (width >= 3840)
            return 8_000;
        if (width >= 2560)
            return 5_000;
        if (width >= 1920)
            return 2_500;
        if (width >= 1280)
            return 1_500;
        if (width >= 854)
            return 700;
        return 300;
    }

    private static void EmitCrfOutOfTypicalRange(EncodingProfile profile, List<EncoderRule> rules)
    {
        // Typical x264/x265 CRF range is 17..28 for visually transparent / good quality. The codec
        // accepts 0..51 but values outside the typical range almost always mean a typo (e.g. 5
        // instead of 25) or a deliberate exotic choice. Surface as a warning either way.
        if (
            profile.Video is not { Policy: StreamPolicy.Transcode } video
            || video.RateControl != RateControlMode.Crf
            || (video.Codec != VideoCodecType.H264 && video.Codec != VideoCodecType.H265)
        )
            return;

        if (video.Crf is >= 17 and <= 28)
            return;

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.CrfOutOfTypicalRange,
                Severity: EncoderRuleSeverity.Warning,
                Field: "video.crf",
                Message: $"CRF {video.Crf} is outside the typical 17..28 range for {video.Codec}; "
                    + (video.Crf < 17 ? "expect very large output." : "expect heavy artefacts."),
                Fix: "Set video.crf to 23 for transparent quality, "
                    + "20 for archival, or 26 for size-constrained delivery."
            )
        );
    }

    private static void EmitHlsKeyframeSegmentMisalignment(
        EncodingProfile profile,
        List<EncoderRule> rules
    )
    {
        // HLS segments must start on a keyframe. When the configured keyframe interval doesn't
        // divide evenly into the segment duration, the muxer either truncates segments early
        // (causing playback hiccups) or skips keyframes (hurting seek precision).
        if (
            profile.Container
            is not (
                Container.HlsTs
                or Container.HlsFmp4
                or Container.AudioHlsTs
                or Container.AudioHlsFmp4
            )
        )
            return;

        if (
            profile.Video is not { Policy: StreamPolicy.Transcode } video
            || video.KeyframeIntervalSeconds <= 0
            || profile.SegmentDurationSeconds <= 0
        )
            return;

        if (profile.SegmentDurationSeconds % video.KeyframeIntervalSeconds == 0)
            return;

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.HlsKeyframeSegmentMisalignment,
                Severity: EncoderRuleSeverity.Warning,
                Field: "video.keyframe_interval_seconds",
                Message: $"Keyframe interval {video.KeyframeIntervalSeconds}s does not divide segment "
                    + $"duration {profile.SegmentDurationSeconds}s; segments may end mid-GOP and "
                    + "seek precision will suffer.",
                Fix: $"Set video.keyframe_interval_seconds to a divisor of {profile.SegmentDurationSeconds} "
                    + "(typically 2 for 6s segments)."
            )
        );
    }

    private static void EmitLadderInverted(EncodingProfile profile, List<EncoderRule> rules)
    {
        // Manual ladder inverted: a higher resolution rung has a lower bitrate than a lower-res
        // rung. ProfileValidator already rejects unsorted ascending bitrate; this rule catches the
        // resolution × bitrate inversion that the bitrate-only check misses.
        if (profile.Ladder is not { Mode: LadderMode.Manual, Rungs: { Length: > 1 } rungs })
            return;

        for (int i = 1; i < rungs.Length; i++)
        {
            LadderRung lower = rungs[i - 1];
            LadderRung higher = rungs[i];
            if (higher.Width <= lower.Width || higher.BitrateKbps >= lower.BitrateKbps)
                continue;

            rules.Add(
                new EncoderRule(
                    Id: EncoderRuleId.LadderInverted,
                    Severity: EncoderRuleSeverity.Error,
                    Field: $"ladder.rungs[{i}]",
                    Message: $"Ladder rung {i} ({higher.Width}x{higher.Height}, {higher.BitrateKbps} kbps) has "
                        + $"a wider resolution but a lower bitrate than rung {i - 1} "
                        + $"({lower.Width}x{lower.Height}, {lower.BitrateKbps} kbps).",
                    Fix: $"Raise rung {i} bitrate above rung {i - 1}, or reorder so wider resolutions come last."
                )
            );
            return;
        }
    }

    private static void EmitAudioAc3OffLadderBitrate(
        EncodingProfile profile,
        List<EncoderRule> rules
    )
    {
        // AC-3 / E-AC-3 only accept a fixed bitrate ladder per the AC-3 (ATSC A/52) spec. Any
        // bitrate outside this set silently picks the nearest supported value in libavcodec —
        // surprises the user when the encode output doesn't match the request.
        foreach (AudioOutput audio in profile.Audio.Where(a => a.Policy == StreamPolicy.Transcode))
        {
            int[]? ladder = audio.Codec switch
            {
                AudioCodecType.Ac3 => Ac3BitrateLadderKbps,
                AudioCodecType.Eac3 => Eac3BitrateLadderKbps,
                _ => null,
            };
            if (ladder is null || audio.BitrateKbps <= 0)
                continue;
            if (Array.IndexOf(ladder, audio.BitrateKbps) >= 0)
                continue;

            int nearest = ladder.OrderBy(b => Math.Abs(b - audio.BitrateKbps)).First();
            rules.Add(
                new EncoderRule(
                    Id: audio.Codec == AudioCodecType.Ac3
                        ? EncoderRuleId.AudioAc3OffLadderBitrate
                        : EncoderRuleId.AudioEac3OffLadderBitrate,
                    Severity: EncoderRuleSeverity.Warning,
                    Field: "audio.bitrate_kbps",
                    Message: $"{audio.Codec} only supports a fixed bitrate ladder; {audio.BitrateKbps} kbps "
                        + "will be coerced by the encoder to the nearest supported value.",
                    Fix: $"Set audio.bitrate_kbps to {nearest} (nearest supported value)."
                )
            );
        }
    }

    /// <summary>ATSC A/52 AC-3 ladder, kbps. Any value outside the set is coerced by libavcodec.</summary>
    private static readonly int[] Ac3BitrateLadderKbps =
    [
        32,
        40,
        48,
        56,
        64,
        80,
        96,
        112,
        128,
        160,
        192,
        224,
        256,
        320,
        384,
        448,
        512,
        576,
        640,
    ];

    /// <summary>E-AC-3 ladder, kbps. Supports finer steps but still a fixed set.</summary>
    private static readonly int[] Eac3BitrateLadderKbps =
    [
        32,
        40,
        48,
        56,
        64,
        80,
        96,
        112,
        128,
        160,
        192,
        224,
        256,
        288,
        320,
        384,
        448,
        512,
        576,
        640,
        768,
        896,
        1024,
        1152,
        1280,
        1408,
        1536,
        1664,
        1792,
        1920,
        2048,
        2304,
        2560,
        2688,
        2816,
        2944,
        3072,
        3200,
        3328,
        3456,
        3584,
        3712,
        3840,
        3968,
        4096,
        4224,
        4352,
        4480,
        4608,
        4736,
        4864,
        4992,
        5120,
        5248,
        5376,
        5504,
        5632,
        5760,
        5888,
        6016,
        6144,
    ];

    private static void EmitSubtitlesContainerIncompatible(
        EncodingProfile profile,
        List<EncoderRule> rules
    )
    {
        // ContainerCompatibility owns the codec×container matrix; ask it whether each declared
        // subtitle codec round-trips through the chosen container without re-encoding.
        // Extract + Copy both write the subtitle track into the output. BurnIn renders into video
        // pixels — no track in the container — and Omit drops the track entirely. Validate only
        // the two policies that need container compatibility.
        foreach (
            SubtitleOutput subtitle in profile.Subtitles.Where(s =>
                s.Policy is SubtitlePolicy.Extract or SubtitlePolicy.Copy
            )
        )
        {
            if (ContainerCompatibility.SupportsSubtitle(profile.Container, subtitle.Codec))
                continue;

            rules.Add(
                new EncoderRule(
                    Id: EncoderRuleId.SubtitlesContainerIncompatible,
                    Severity: EncoderRuleSeverity.Error,
                    Field: "subtitles.codec",
                    Message: $"Container {profile.Container} does not support subtitle codec "
                        + $"{subtitle.Codec}; the muxer will drop the track.",
                    Fix: $"Pick a container that supports {subtitle.Codec}, switch the subtitle "
                        + "codec to a compatible one, or change subtitles.policy to BurnIn."
                )
            );
        }
    }

    private static void EmitHdrInverseTonemapUnsupported(
        EncodingProfile profile,
        List<EncoderRule> rules
    )
    {
        // Reverse direction of HDR conversion: the encoder can tonemap HDR -> SDR, but never
        // synthesises HDR from an SDR source (inverse tonemap requires colour-volume metadata
        // the source doesn't have).
        if (profile.HdrPolicy != HdrPolicy.AlwaysPreserve)
            return;

        if (profile.Video is not { Policy: StreamPolicy.Transcode } video)
            return;

        if (video.BitDepth >= 10)
            return; // request is consistent — preserve HDR from a 10-bit source.

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.HdrInverseTonemapUnsupported,
                Severity: EncoderRuleSeverity.Error,
                Field: "hdr_policy",
                Message: "HdrPolicy.AlwaysPreserve with an 8-bit video output asks the encoder to "
                    + "synthesise HDR from an SDR-shaped pipeline; no inverse-tonemap is provided.",
                Fix: "Either raise video.bit_depth to 10+ on an HDR-capable codec (H.265 / AV1 / VP9), "
                    + "or change hdr_policy to PassthroughWhenPossible / AlwaysTonemap."
            )
        );
    }

    private static void EmitCustomArgsReservedFlag(EncodingProfile profile, List<EncoderRule> rules)
    {
        if (profile.CustomArguments is null || profile.CustomArguments.Count == 0)
            return;

        foreach (string key in profile.CustomArguments.Keys)
        {
            // Normalize: callers may store keys with or without the leading dash.
            string normalized = key.StartsWith('-') ? key : $"-{key}";
            if (!ReservedFlags.Contains(normalized))
                continue;

            rules.Add(
                new EncoderRule(
                    Id: EncoderRuleId.CustomArgsReservedFlag,
                    Severity: EncoderRuleSeverity.Error,
                    Field: $"custom_arguments[{key}]",
                    Message: $"CustomArgument '{key}' overrides a flag the encoder derives from typed "
                        + "profile fields. The profile's declared values would be ignored and the "
                        + "validator can no longer guarantee the output matches the profile.",
                    Fix: $"Remove the '{key}' override and set the matching typed field "
                        + "(codec / container / preset / hardware preference / ladder) instead."
                )
            );
        }
    }

    // ----------------------------------------------------------------------
    // Source-dependent rules
    // ----------------------------------------------------------------------

    private static void EmitLevelFrameRateCapExceeded(
        EncodingProfile profile,
        MediaInfo source,
        List<EncoderRule> rules
    )
    {
        if (
            profile.Video is not { Policy: StreamPolicy.Transcode } video
            || string.IsNullOrEmpty(video.Level)
            || source.VideoStreams.Count == 0
        )
            return;

        VideoStreamInfo primaryVideo = source.VideoStreams[0];
        double fps = primaryVideo.AverageFrameRate ?? primaryVideo.FrameRate;
        if (fps <= 0)
            return;

        long lumaSamplesPerSec = (long)(video.Width * (video.Height ?? primaryVideo.Height) * fps);

        CodecLevelFpsCaps.LevelCap? cap = CodecLevelFpsCaps.Lookup(video.Codec, video.Level);
        if (cap is null || lumaSamplesPerSec <= cap.MaxLumaSamplesPerSec)
            return;

        CodecLevelFpsCaps.LevelCap? nextFit = CodecLevelFpsCaps.FindNextFit(
            video.Codec,
            lumaSamplesPerSec
        );

        string fix = nextFit is not null
            ? $"Raise video.level to {nextFit.Level} (supports up to {nextFit.MaxLumaSamplesPerSec:N0} luma samples/sec)."
            : "No standard level supports this resolution × frame rate.";

        rules.Add(
            new EncoderRule(
                Id: EncoderRuleId.LevelFrameRateCapExceeded,
                Severity: EncoderRuleSeverity.Error,
                Field: "video.level",
                Message: $"Source {fps:F2} fps × {video.Width}x{video.Height ?? primaryVideo.Height} "
                    + $"requires {lumaSamplesPerSec:N0} luma samples/sec; level "
                    + $"{video.Level} allows {cap.MaxLumaSamplesPerSec:N0}.",
                Fix: fix
            )
        );
    }
}
