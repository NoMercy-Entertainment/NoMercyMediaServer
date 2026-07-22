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

using System.Reflection;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Profiles.RuleCompleteness;

/// <summary>
/// Self-policing completeness gate for the encoder rule catalogue vs.
/// the profile-validator guard surface.
///
/// For every <see cref="EncoderRuleId"/> constant this test asserts it is
/// EITHER in <see cref="ProfileValidatorCoveredRuleIds"/> (proven by a
/// fires-on-bad + silent-on-valid-neighbor pair in
/// <c>ProfileRuleValidatorTests</c>) OR in <see cref="ExcludedRuleIds"/>
/// with a documented reason.
///
/// When someone adds a new rule to the catalogue without updating one of
/// those two sets, this test fails — that is the point.
/// </summary>
[Collection(name: "encoder-rule-completeness")]
public class ProfileRuleCompletenessTests
{
    /// <summary>
    /// Every rule ID that <see cref="ProfileRuleValidator"/> (Validate or
    /// ValidateWithSource) actually emits, and for which a fires-on-bad +
    /// silent-on-valid-neighbor pair exists in ProfileRuleValidatorTests.
    /// </summary>
    private static readonly HashSet<string> ProfileValidatorCoveredRuleIds =
    [
        EncoderRuleId.ProfileNameMissing,
        EncoderRuleId.ProfileNoOutputs,
        EncoderRuleId.VideoWidthInvalid,
        EncoderRuleId.VideoHeightInvalid,
        EncoderRuleId.VideoRateControlMissing,
        EncoderRuleId.VideoRateControlConflict,
        EncoderRuleId.CodecContainerMismatch,
        EncoderRuleId.AudioCodecContainerMismatch,
        EncoderRuleId.AudioBitrateMissing,
        EncoderRuleId.HlsFmp4CodecMismatch,
        EncoderRuleId.LadderDuplicateVariant,
        EncoderRuleId.LadderManualEmpty,
        EncoderRuleId.LadderManualUnsorted,
        EncoderRuleId.LevelResolutionMismatch,
        EncoderRuleId.LevelInvalid,
        EncoderRuleId.BitrateTooLowForResolution,
        EncoderRuleId.CrfOutOfTypicalRange,
        EncoderRuleId.HlsKeyframeSegmentMisalignment,
        EncoderRuleId.LadderInverted,
        EncoderRuleId.AudioAc3OffLadderBitrate,
        EncoderRuleId.AudioEac3OffLadderBitrate,
        EncoderRuleId.SubtitlesContainerIncompatible,
        EncoderRuleId.SubtitlesBurnInPermanent,
        EncoderRuleId.SubtitlesAssNeedsCapableClient,
        EncoderRuleId.HdrInverseTonemapUnsupported,
        EncoderRuleId.BitDepthH26xProfilePromoted,
        EncoderRuleId.CustomArgsReservedFlag,
        EncoderRuleId.DrmHttpNotHttps,
        EncoderRuleId.DrmKeyMissing,
        EncoderRuleId.SourceVariableFrameRate,
        EncoderRuleId.SourceDolbyVisionWillBeStripped,
        EncoderRuleId.SourceUpscalingDetected,
        EncoderRuleId.LevelFrameRateCapExceeded,
        EncoderRuleId.StereoscopicSourceUnsupported,
        EncoderRuleId.SphericalMetadataWillBeStripped,
    ];

    /// <summary>
    /// Rule IDs that are legitimately absent from <see cref="ProfileRuleValidator"/>
    /// and therefore excluded from the fires-on-bad coverage requirement.
    /// Each entry carries its exclusion category and reason.
    /// </summary>
    private static readonly Dictionary<string, string> ExcludedRuleIds = new()
    {
        // ---- Pending guard fix -------------------------------------------
        // pending guard fix — see guard-completeness-strategy:
        // BitDepthVp9ProfileMismatch has no guard in ProfileRuleValidator yet.
        [key: EncoderRuleId.BitDepthVp9ProfileMismatch] =
            "pending guard fix — see guard-completeness-strategy: no guard in ProfileRuleValidator yet",

        // pending guard fix — see guard-completeness-strategy:
        // ParentIdCycle throws a raw exception instead of emitting the rule ID.
        [key: EncoderRuleId.ParentIdCycle] =
            "pending guard fix — see guard-completeness-strategy: throws raw exception, not the rule ID",

        // pending guard fix — see guard-completeness-strategy:
        // BitDepthNoHardwareSupport is resolved silently in BitDepthPolicyResolver,
        // not emitted by ProfileRuleValidator during static profile validation.
        [key: EncoderRuleId.BitDepthNoHardwareSupport] =
            "pending guard fix — see guard-completeness-strategy: resolved silently in BitDepthPolicyResolver, not emitted by ProfileRuleValidator",

        // ---- Runtime-only (emitted during encode execution) ---------------
        // Emitted by BitDepthPolicyResolver at encode time, not at profile save.
        [key: EncoderRuleId.BitDepthAutoDowngrade] =
            "runtime-only: emitted by BitDepthPolicyResolver during encode, not by ProfileRuleValidator",
        [key: EncoderRuleId.BitDepthStrictViolation] =
            "runtime-only: emitted by BitDepthPolicyResolver during encode, not by ProfileRuleValidator",

        // Hardware and GPU availability are resolved at encode time against
        // the actual host system; they cannot be validated from profile data alone.
        [key: EncoderRuleId.HardwareForcedButUnavailable] =
            "runtime-only: hardware availability is known only at encode time",
        [key: EncoderRuleId.HardwareGpuTelemetryUnsupported] =
            "runtime-only: GPU telemetry support is known only at encode time",
        [key: EncoderRuleId.GpuCapacityExhausted] =
            "runtime-only: GPU slot exhaustion happens during encode dispatch, not at profile validation",

        // Encoder binary and capability checks happen at runtime.
        [key: EncoderRuleId.EncoderInitFailed] =
            "runtime-only: encoder init failure detected during encode, not at profile validation",
        [key: EncoderRuleId.CapabilityFpcalcMissing] =
            "runtime-only: fpcalc binary availability checked at runtime",
        [key: EncoderRuleId.CapabilityWhisperMissing] =
            "runtime-only: whisper binary availability checked at runtime",
        [key: EncoderRuleId.CapabilityTesseractModelMissing] =
            "runtime-only: tesseract model availability checked at runtime",

        // Source and output path errors are I/O concerns resolved at encode time.
        [key: EncoderRuleId.SourceNotAccessible] =
            "runtime-only: source file access checked at encode time",
        [key: EncoderRuleId.SourceReadError] =
            "runtime-only: source read errors occur during encode, not profile validation",
        [key: EncoderRuleId.OutputPathNotAllowed] =
            "runtime-only: output path ACL is checked at encode time",
        [key: EncoderRuleId.OutputWriteError] = "runtime-only: output write errors occur during encode",

        // Job and checkpoint errors are queue-lifecycle concerns.
        [key: EncoderRuleId.JobInterruptedNoCheckpoint] =
            "runtime-only: job interruption detected by the queue engine, not by the profile validator",

        // Disc ripping errors are peripheral / OS-level at rip time.
        [key: EncoderRuleId.DiscDriveBusy] = "runtime-only: disc drive state checked at rip time",
        [key: EncoderRuleId.DiscAacsCertMissing] =
            "runtime-only: AACS certificate presence checked at rip time",
        [key: EncoderRuleId.DiscBdplusConverterMissing] =
            "runtime-only: BD+ converter binary checked at rip time",
        [key: EncoderRuleId.DiscReadError] = "runtime-only: disc read errors occur during rip",

        // License errors are network / server-state concerns at encode time.
        [key: EncoderRuleId.LicenseRevoked] =
            "runtime-only: license validity checked against the license server at encode time",
        [key: EncoderRuleId.LicenseUnreachable] =
            "runtime-only: license server reachability is a runtime network concern",

        // Distribution errors arise during cluster encode dispatch.
        [key: EncoderRuleId.DistributionHmacInvalid] =
            "runtime-only: HMAC signature validated during distribution task dispatch",
        [key: EncoderRuleId.DistributionTimestampReplay] =
            "runtime-only: replay detection occurs during distribution task dispatch",
        [key: EncoderRuleId.DistributionWorkerNotRegistered] =
            "runtime-only: worker registry state is known only at dispatch time",

        // ---- Controller / API layer (not profile validator) ---------------
        // Emitted by EncodingPresetsController when a PUT targets a builtin preset.
        [key: EncoderRuleId.ProfileBuiltinReadonly] =
            "controller-only: emitted by EncodingPresetsController, not by ProfileRuleValidator",

        // Import trust-chain rules are validated by the profile import pipeline,
        // not by the static profile structure validator.
        [key: EncoderRuleId.ImportHttpNotHttps] =
            "import-pipeline-only: trust-chain URL scheme check in the import pipeline",
        [key: EncoderRuleId.ImportFetchFailed] =
            "import-pipeline-only: URL fetch failure surfaced by EncoderProfileService.ImportAsync",
        [key: EncoderRuleId.ImportSourceMissing] =
            "import-pipeline-only: neither inline body nor URL supplied — checked by EncoderProfileService.ImportAsync",
        [key: EncoderRuleId.ImportJsonMalformed] =
            "import-pipeline-only: profile body JSON parse failure surfaced by EncoderProfileService.ImportAsync",
        [key: EncoderRuleId.ImportSignatureInvalid] =
            "import-pipeline-only: cryptographic signature verified by ProfileSignatureVerifier",
        [key: EncoderRuleId.ImportPublisherUntrusted] =
            "import-pipeline-only: publisher trust verified against TrustedPublisherRegistry",
        [key: EncoderRuleId.ImportUnsignedRequiresFlag] =
            "import-pipeline-only: unsigned profile gate enforced by the import pipeline",

        // Trusted-publisher management rules are emitted by the publisher-registry
        // endpoints, not the profile structure validator.
        [key: EncoderRuleId.TrustedPublisherPublicKeyInvalid] =
            "publisher-registry-only: public key format validated by TrustedPublisherRegistry",
        [key: EncoderRuleId.TrustedPublisherAlreadyTrusted] =
            "publisher-registry-only: duplicate-trust check performed by TrustedPublisherRegistry",
    };

    [Fact]
    public void Every_EncoderRuleId_constant_is_either_covered_or_documented_as_excluded()
    {
        IEnumerable<string> allRuleIds = typeof(EncoderRuleId)
            .GetFields(bindingAttr: BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(predicate: f => f is { IsLiteral: true, IsInitOnly: false })
            .Select(selector: f => (string)f.GetValue(obj: null)!);

        List<string> uncategorised = [];

        foreach (string id in allRuleIds)
        {
            bool isCovered = ProfileValidatorCoveredRuleIds.Contains(item: id);
            bool isExcluded = ExcludedRuleIds.ContainsKey(key: id);

            if (!isCovered && !isExcluded)
                uncategorised.Add(item: id);
        }

        uncategorised
            .Should()
            .BeEmpty(
                because: "every EncoderRuleId constant must be either (a) covered by a "
                         + "fires-on-bad test in ProfileRuleValidatorTests or (b) listed in "
                         + "ProfileRuleCompletenessTests.ExcludedRuleIds with a documented reason. "
                         + "Uncategorised rule IDs: "
                         + string.Join(separator: ", ", values: uncategorised)
            );
    }

    [Fact]
    public void ProfileValidatorCoveredRuleIds_contains_no_phantom_entries()
    {
        IReadOnlySet<string> allRuleIds = typeof(EncoderRuleId)
            .GetFields(bindingAttr: BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(predicate: f => f is { IsLiteral: true, IsInitOnly: false })
            .Select(selector: f => (string)f.GetValue(obj: null)!)
            .ToHashSet();

        List<string> phantoms = ProfileValidatorCoveredRuleIds
            .Where(predicate: id => !allRuleIds.Contains(item: id))
            .ToList();

        phantoms
            .Should()
            .BeEmpty(
                because: "ProfileValidatorCoveredRuleIds must not reference IDs that no longer "
                         + "exist in EncoderRuleId. Stale entries: "
                         + string.Join(separator: ", ", values: phantoms)
            );
    }

    [Fact]
    public void ExcludedRuleIds_contains_no_phantom_entries()
    {
        IReadOnlySet<string> allRuleIds = typeof(EncoderRuleId)
            .GetFields(bindingAttr: BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(predicate: f => f is { IsLiteral: true, IsInitOnly: false })
            .Select(selector: f => (string)f.GetValue(obj: null)!)
            .ToHashSet();

        List<string> phantoms = ExcludedRuleIds.Keys.Where(predicate: id => !allRuleIds.Contains(item: id)).ToList();

        phantoms
            .Should()
            .BeEmpty(
                because: "ExcludedRuleIds must not reference IDs that no longer exist in "
                         + "EncoderRuleId. Stale entries: "
                         + string.Join(separator: ", ", values: phantoms)
            );
    }

    [Fact]
    public void No_rule_id_appears_in_both_covered_and_excluded_sets()
    {
        List<string> overlap = ProfileValidatorCoveredRuleIds
            .Intersect(second: ExcludedRuleIds.Keys)
            .ToList();

        overlap
            .Should()
            .BeEmpty(
                because: "a rule ID cannot be both covered by profile-validator tests and "
                         + "excluded from coverage — pick one. Overlapping IDs: "
                         + string.Join(separator: ", ", values: overlap)
            );
    }

    [Fact]
    public void ProfileValidator_emits_covered_rule_ids_for_known_trigger_profiles()
    {
        static VideoOutput VideoTranscode(
            VideoCodecType codec = VideoCodecType.H264,
            int width = 1920,
            int? height = 1080,
            RateControlMode rc = RateControlMode.Crf,
            int crf = 23,
            int bitrate = 0,
            int bitDepth = 8,
            int keyframeSeconds = 2,
            string? level = null
        ) =>
            new(
                Policy: StreamPolicy.Transcode,
                Codec: codec,
                Width: width,
                Height: height,
                RateControl: rc,
                Crf: crf,
                BitrateKbps: bitrate,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: null,
                CodecProfile: CodecProfile.Auto,
                Level: level,
                Tune: null,
                BitDepth: bitDepth,
                PixelFormat: null,
                KeyframeIntervalSeconds: keyframeSeconds,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: "video_%05d",
                PlaylistNameTemplate: "playlist"
            );

        static EncodingProfile MakeProfile(
            VideoOutput? video = null,
            Container container = Container.HlsFmp4,
            AudioOutput[]? audio = null,
            SubtitleOutput[]? subtitles = null,
            LadderConfig? ladder = null,
            int segmentDuration = 6,
            HdrPolicies hdrPolicies = HdrPolicies.PassthroughWhenPossible,
            DrmConfig? drm = null,
            Dictionary<string, string>? customArgs = null
        ) =>
            new(
                Id: Ulid.NewUlid(),
                Name: "completeness-trigger",
                Container: container,
                Video: video,
                Audio: audio ?? [],
                Subtitles: subtitles ?? [],
                SegmentDurationSeconds: segmentDuration,
                Ladder: ladder
            )
            {
                HdrPolicies = hdrPolicies,
                Drm = drm,
                CustomArguments = customArgs,
            };

        static AudioOutput AudioTrack(AudioCodecType codec, int bitrate) =>
            new(
                Policy: StreamPolicy.Transcode,
                Codec: codec,
                BitrateKbps: bitrate,
                Channels: 2,
                SampleRateHz: 48000,
                AllowedLanguages: ["eng"],
                DefaultLanguage: "eng",
                Loudness: null,
                Downmix: null,
                SegmentNameTemplate: "audio_%05d",
                PlaylistNameTemplate: "audio_playlist"
            );

        static SubtitleOutput SubtitleTrack(SubtitleCodecType codec, SubtitlePolicy policy) =>
            new(
                Policy: policy,
                Codec: codec,
                AllowedLanguages: ["eng"],
                IncludeForced: true,
                OcrLanguage: null,
                PlaylistNameTemplate: "subs"
            );

        Dictionary<string, EncodingProfile> triggerProfiles = new()
        {
            [key: EncoderRuleId.ProfileNameMissing] = MakeProfile(video: VideoTranscode()) with { Name = "" },
            [key: EncoderRuleId.ProfileNoOutputs] = MakeProfile(),
            [key: EncoderRuleId.VideoWidthInvalid] = MakeProfile(video: VideoTranscode(width: 0)),
            [key: EncoderRuleId.VideoHeightInvalid] = MakeProfile(video: VideoTranscode(height: 0)),
            [key: EncoderRuleId.VideoRateControlMissing] = MakeProfile(
                video: VideoTranscode(rc: RateControlMode.Crf, crf: 0)
            ),
            [key: EncoderRuleId.VideoRateControlConflict] = MakeProfile(
                video: VideoTranscode(rc: RateControlMode.Vbr, bitrate: 0, crf: 23)
            ),
            [key: EncoderRuleId.CodecContainerMismatch] = MakeProfile(
                video: VideoTranscode(codec: VideoCodecType.Vp9),
                container: Container.Mp4
            ),
            [key: EncoderRuleId.AudioCodecContainerMismatch] = MakeProfile(
                video: VideoTranscode(),
                container: Container.Mp4,
                audio: [AudioTrack(codec: AudioCodecType.Flac, bitrate: 0)]
            ),
            [key: EncoderRuleId.AudioBitrateMissing] = MakeProfile(
                video: VideoTranscode(),
                audio: [AudioTrack(codec: AudioCodecType.Aac, bitrate: 0)]
            ),
            [key: EncoderRuleId.HlsFmp4CodecMismatch] = MakeProfile(
                video: VideoTranscode(codec: VideoCodecType.H265),
                container: Container.HlsTs
            ),
            [key: EncoderRuleId.LadderDuplicateVariant] = MakeProfile(
                video: VideoTranscode(),
                ladder: new()
                {
                    Mode = LadderMode.Manual,
                    Rungs =
                    [
                        new(Width: 1280, Height: 720, Codec: VideoCodecType.H264, BitrateKbps: 2500, MaxBitrateKbps: 3000, BufferSizeKbps: 5000, Framerate: 24),
                        new(Width: 1280, Height: 720, Codec: VideoCodecType.H264, BitrateKbps: 2500, MaxBitrateKbps: 3000, BufferSizeKbps: 5000, Framerate: 24),
                    ],
                }
            ),
            [key: EncoderRuleId.LadderManualEmpty] = MakeProfile(
                video: VideoTranscode(),
                ladder: new() { Mode = LadderMode.Manual, Rungs = [] }
            ),
            [key: EncoderRuleId.LadderManualUnsorted] = MakeProfile(
                video: VideoTranscode(),
                ladder: new()
                {
                    Mode = LadderMode.Manual,
                    Rungs =
                    [
                        new(Width: 1920, Height: 1080, Codec: VideoCodecType.H264, BitrateKbps: 8000, MaxBitrateKbps: 9600, BufferSizeKbps: 16000, Framerate: 24),
                        new(Width: 1280, Height: 720, Codec: VideoCodecType.H264, BitrateKbps: 4000, MaxBitrateKbps: 4800, BufferSizeKbps: 8000, Framerate: 24),
                    ],
                }
            ),
            [key: EncoderRuleId.LevelResolutionMismatch] = MakeProfile(
                video: VideoTranscode(codec: VideoCodecType.H264, width: 3840, height: 2160, level: "4.0")
            ),
            [key: EncoderRuleId.LevelInvalid] = MakeProfile(
                video: VideoTranscode(codec: VideoCodecType.H264, level: "6.3")
            ),
            [key: EncoderRuleId.BitrateTooLowForResolution] = MakeProfile(
                video: VideoTranscode(rc: RateControlMode.Vbr, bitrate: 500, width: 3840, height: 2160)
            ),
            [key: EncoderRuleId.CrfOutOfTypicalRange] = MakeProfile(
                video: VideoTranscode(rc: RateControlMode.Crf, crf: 5)
            ),
            [key: EncoderRuleId.HlsKeyframeSegmentMisalignment] = MakeProfile(
                video: VideoTranscode(keyframeSeconds: 4),
                container: Container.HlsFmp4,
                segmentDuration: 6
            ),
            [key: EncoderRuleId.LadderInverted] = MakeProfile(
                video: VideoTranscode(),
                ladder: new()
                {
                    Mode = LadderMode.Manual,
                    Rungs =
                    [
                        new(Width: 854, Height: 480, Codec: VideoCodecType.H264, BitrateKbps: 4000, MaxBitrateKbps: 4800, BufferSizeKbps: 8000, Framerate: 24),
                        new(Width: 1920, Height: 1080, Codec: VideoCodecType.H264, BitrateKbps: 2000, MaxBitrateKbps: 2400, BufferSizeKbps: 4000, Framerate: 24),
                    ],
                }
            ),
            [key: EncoderRuleId.AudioAc3OffLadderBitrate] = MakeProfile(
                video: VideoTranscode(),
                audio: [AudioTrack(codec: AudioCodecType.Ac3, bitrate: 333)]
            ),
            [key: EncoderRuleId.AudioEac3OffLadderBitrate] = MakeProfile(
                video: VideoTranscode(),
                audio: [AudioTrack(codec: AudioCodecType.Eac3, bitrate: 137)]
            ),
            [key: EncoderRuleId.SubtitlesContainerIncompatible] = MakeProfile(
                video: VideoTranscode(),
                container: Container.Mp4,
                subtitles: [SubtitleTrack(codec: SubtitleCodecType.Ass, policy: SubtitlePolicy.Extract)]
            ),
            [key: EncoderRuleId.SubtitlesBurnInPermanent] = MakeProfile(
                video: VideoTranscode(),
                subtitles: [SubtitleTrack(codec: SubtitleCodecType.Ass, policy: SubtitlePolicy.BurnIn)]
            ),
            [key: EncoderRuleId.SubtitlesAssNeedsCapableClient] = MakeProfile(
                video: VideoTranscode(),
                container: Container.HlsFmp4,
                subtitles: [SubtitleTrack(codec: SubtitleCodecType.Ass, policy: SubtitlePolicy.Extract)]
            ),
            [key: EncoderRuleId.HdrInverseTonemapUnsupported] = MakeProfile(
                video: VideoTranscode(bitDepth: 8),
                hdrPolicies: HdrPolicies.AlwaysPreserve
            ),
            // H.264 + explicit 8-bit-only "High" profile + 10-bit depth: the
            // pipeline promotes High -> High10, the validator warns about it.
            [key: EncoderRuleId.BitDepthH26xProfilePromoted] = MakeProfile(
                video: VideoTranscode(codec: VideoCodecType.H264, bitDepth: 10) with
                {
                    CodecProfile = CodecProfile.High,
                },
                container: Container.Mp4
            ),
            [key: EncoderRuleId.CustomArgsReservedFlag] = MakeProfile(
                video: VideoTranscode(),
                customArgs: new() { [key: "-c:v"] = "libx264" }
            ),
            [key: EncoderRuleId.DrmHttpNotHttps] = MakeProfile(video: VideoTranscode()) with
            {
                Drm = new(
                    Scheme: "aes-128",
                    Parameters: new() { [key: "key_uri"] = "http://server/key.bin" }
                ),
            },
            [key: EncoderRuleId.DrmKeyMissing] = MakeProfile(video: VideoTranscode()) with
            {
                Drm = new(Scheme: "aes-128", Parameters: new()),
            },
        };

        List<string> notFiring = [];

        foreach ((string ruleId, EncodingProfile profile) in triggerProfiles)
        {
            ValidationEnvelope envelope = ProfileRuleValidator.Validate(profile: profile);
            bool fired =
                envelope.Errors.Any(predicate: r => r.Id == ruleId)
                || envelope.Warnings.Any(predicate: r => r.Id == ruleId);

            if (!fired)
                notFiring.Add(item: ruleId);
        }

        notFiring
            .Should()
            .BeEmpty(
                because: "every trigger profile must cause ProfileRuleValidator to emit its rule ID. "
                         + "Rules that did NOT fire: "
                         + string.Join(separator: ", ", values: notFiring)
            );
    }

    [Fact]
    public void ProfileValidator_is_silent_on_valid_neighbor_profiles()
    {
        static VideoOutput VideoTranscode(
            VideoCodecType codec = VideoCodecType.H264,
            int width = 1920,
            int? height = 1080,
            RateControlMode rc = RateControlMode.Crf,
            int crf = 23,
            int bitrate = 0,
            int bitDepth = 8,
            int keyframeSeconds = 2,
            string? level = null
        ) =>
            new(
                Policy: StreamPolicy.Transcode,
                Codec: codec,
                Width: width,
                Height: height,
                RateControl: rc,
                Crf: crf,
                BitrateKbps: bitrate,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: null,
                CodecProfile: CodecProfile.Auto,
                Level: level,
                Tune: null,
                BitDepth: bitDepth,
                PixelFormat: null,
                KeyframeIntervalSeconds: keyframeSeconds,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: "video_%05d",
                PlaylistNameTemplate: "playlist"
            );

        static EncodingProfile MakeProfile(
            VideoOutput? video = null,
            Container container = Container.HlsFmp4,
            AudioOutput[]? audio = null,
            SubtitleOutput[]? subtitles = null,
            LadderConfig? ladder = null,
            int segmentDuration = 6,
            HdrPolicies hdrPolicies = HdrPolicies.PassthroughWhenPossible,
            DrmConfig? drm = null,
            Dictionary<string, string>? customArgs = null
        ) =>
            new(
                Id: Ulid.NewUlid(),
                Name: "completeness-valid-neighbor",
                Container: container,
                Video: video,
                Audio: audio ?? [],
                Subtitles: subtitles ?? [],
                SegmentDurationSeconds: segmentDuration,
                Ladder: ladder
            )
            {
                HdrPolicies = hdrPolicies,
                Drm = drm,
                CustomArguments = customArgs,
            };

        static AudioOutput AudioTrack(AudioCodecType codec, int bitrate) =>
            new(
                Policy: StreamPolicy.Transcode,
                Codec: codec,
                BitrateKbps: bitrate,
                Channels: 2,
                SampleRateHz: 48000,
                AllowedLanguages: ["eng"],
                DefaultLanguage: "eng",
                Loudness: null,
                Downmix: null,
                SegmentNameTemplate: "audio_%05d",
                PlaylistNameTemplate: "audio_playlist"
            );

        static SubtitleOutput SubtitleTrack(SubtitleCodecType codec, SubtitlePolicy policy) =>
            new(
                Policy: policy,
                Codec: codec,
                AllowedLanguages: ["eng"],
                IncludeForced: true,
                OcrLanguage: null,
                PlaylistNameTemplate: "subs"
            );

        Dictionary<string, EncodingProfile> validNeighbors = new()
        {
            [key: EncoderRuleId.ProfileNameMissing] = MakeProfile(video: VideoTranscode()) with
            {
                Name = "Valid Profile",
            },
            [key: EncoderRuleId.ProfileNoOutputs] = MakeProfile(
                audio: [AudioTrack(codec: AudioCodecType.Aac, bitrate: 192)]
            ) with
            {
                Container = Container.Aac,
            },
            [key: EncoderRuleId.VideoWidthInvalid] = MakeProfile(video: VideoTranscode(width: 1920)),
            [key: EncoderRuleId.VideoHeightInvalid] = MakeProfile(video: VideoTranscode(height: 1080)),
            [key: EncoderRuleId.VideoRateControlMissing] = MakeProfile(
                video: VideoTranscode(rc: RateControlMode.Crf, crf: 23)
            ),
            [key: EncoderRuleId.VideoRateControlConflict] = MakeProfile(
                video: VideoTranscode(rc: RateControlMode.Vbr, bitrate: 4000, crf: 0)
            ),
            [key: EncoderRuleId.CodecContainerMismatch] = MakeProfile(
                video: VideoTranscode(codec: VideoCodecType.H264),
                container: Container.Mp4
            ),
            [key: EncoderRuleId.AudioCodecContainerMismatch] = MakeProfile(
                video: VideoTranscode(),
                container: Container.Mp4,
                audio: [AudioTrack(codec: AudioCodecType.Aac, bitrate: 192)]
            ),
            [key: EncoderRuleId.AudioBitrateMissing] = MakeProfile(
                video: VideoTranscode(),
                audio: [AudioTrack(codec: AudioCodecType.Aac, bitrate: 192)]
            ),
            [key: EncoderRuleId.HlsFmp4CodecMismatch] = MakeProfile(
                video: VideoTranscode(codec: VideoCodecType.H264),
                container: Container.HlsTs
            ),
            [key: EncoderRuleId.LadderDuplicateVariant] = MakeProfile(
                video: VideoTranscode(),
                ladder: new()
                {
                    Mode = LadderMode.Manual,
                    Rungs =
                    [
                        new(Width: 1280, Height: 720, Codec: VideoCodecType.H264, BitrateKbps: 2500, MaxBitrateKbps: 3000, BufferSizeKbps: 5000, Framerate: 24),
                        new(Width: 1920, Height: 1080, Codec: VideoCodecType.H264, BitrateKbps: 4500, MaxBitrateKbps: 5400, BufferSizeKbps: 9000, Framerate: 24),
                    ],
                }
            ),
            [key: EncoderRuleId.LadderManualEmpty] = MakeProfile(
                video: VideoTranscode(),
                ladder: new()
                {
                    Mode = LadderMode.Manual,
                    Rungs = [new(Width: 1920, Height: 1080, Codec: VideoCodecType.H264, BitrateKbps: 4000, MaxBitrateKbps: 4800, BufferSizeKbps: 8000, Framerate: 24)],
                }
            ),
            [key: EncoderRuleId.LadderManualUnsorted] = MakeProfile(
                video: VideoTranscode(),
                ladder: new()
                {
                    Mode = LadderMode.Manual,
                    Rungs =
                    [
                        new(Width: 1280, Height: 720, Codec: VideoCodecType.H264, BitrateKbps: 4000, MaxBitrateKbps: 4800, BufferSizeKbps: 8000, Framerate: 24),
                        new(Width: 1920, Height: 1080, Codec: VideoCodecType.H264, BitrateKbps: 8000, MaxBitrateKbps: 9600, BufferSizeKbps: 16000, Framerate: 24),
                    ],
                }
            ),
            [key: EncoderRuleId.LevelResolutionMismatch] = MakeProfile(
                video: VideoTranscode(codec: VideoCodecType.H264, width: 1920, height: 1080, level: "4.1")
            ),
            [key: EncoderRuleId.LevelInvalid] = MakeProfile(
                video: VideoTranscode(codec: VideoCodecType.H264, level: "4.1")
            ),
            [key: EncoderRuleId.BitrateTooLowForResolution] = MakeProfile(
                video: VideoTranscode(rc: RateControlMode.Vbr, bitrate: 3000, width: 1920, height: 1080)
            ),
            [key: EncoderRuleId.CrfOutOfTypicalRange] = MakeProfile(
                video: VideoTranscode(rc: RateControlMode.Crf, crf: 23)
            ),
            [key: EncoderRuleId.HlsKeyframeSegmentMisalignment] = MakeProfile(
                video: VideoTranscode(keyframeSeconds: 2),
                container: Container.HlsFmp4,
                segmentDuration: 6
            ),
            [key: EncoderRuleId.LadderInverted] = MakeProfile(
                video: VideoTranscode(),
                ladder: new()
                {
                    Mode = LadderMode.Manual,
                    Rungs =
                    [
                        new(Width: 854, Height: 480, Codec: VideoCodecType.H264, BitrateKbps: 1000, MaxBitrateKbps: 1200, BufferSizeKbps: 2000, Framerate: 24),
                        new(Width: 1920, Height: 1080, Codec: VideoCodecType.H264, BitrateKbps: 4500, MaxBitrateKbps: 5400, BufferSizeKbps: 9000, Framerate: 24),
                    ],
                }
            ),
            [key: EncoderRuleId.AudioAc3OffLadderBitrate] = MakeProfile(
                video: VideoTranscode(),
                audio: [AudioTrack(codec: AudioCodecType.Ac3, bitrate: 320)]
            ),
            [key: EncoderRuleId.AudioEac3OffLadderBitrate] = MakeProfile(
                video: VideoTranscode(),
                audio: [AudioTrack(codec: AudioCodecType.Eac3, bitrate: 128)]
            ),
            [key: EncoderRuleId.SubtitlesContainerIncompatible] = MakeProfile(
                video: VideoTranscode(),
                container: Container.HlsFmp4,
                subtitles: [SubtitleTrack(codec: SubtitleCodecType.WebVtt, policy: SubtitlePolicy.Extract)]
            ),
            [key: EncoderRuleId.SubtitlesBurnInPermanent] = MakeProfile(
                video: VideoTranscode(),
                subtitles: [SubtitleTrack(codec: SubtitleCodecType.WebVtt, policy: SubtitlePolicy.Extract)]
            ),
            [key: EncoderRuleId.SubtitlesAssNeedsCapableClient] = MakeProfile(
                video: VideoTranscode(),
                container: Container.Mkv,
                subtitles: [SubtitleTrack(codec: SubtitleCodecType.Ass, policy: SubtitlePolicy.Extract)]
            ),
            [key: EncoderRuleId.HdrInverseTonemapUnsupported] = MakeProfile(
                video: VideoTranscode(bitDepth: 10),
                hdrPolicies: HdrPolicies.AlwaysPreserve
            ),
            // H.264 + High10 + 10-bit already agree — nothing to promote, no warning.
            [key: EncoderRuleId.BitDepthH26xProfilePromoted] = MakeProfile(
                video: VideoTranscode(codec: VideoCodecType.H264, bitDepth: 10) with
                {
                    CodecProfile = CodecProfile.High10,
                },
                container: Container.Mp4
            ),
            [key: EncoderRuleId.CustomArgsReservedFlag] = MakeProfile(
                video: VideoTranscode(),
                customArgs: new() { [key: "-loglevel"] = "info" }
            ),
            [key: EncoderRuleId.DrmHttpNotHttps] = MakeProfile(video: VideoTranscode()) with
            {
                Drm = new(
                    Scheme: "aes-128",
                    Parameters: new() { [key: "key_uri"] = "https://server/key.bin" }
                ),
            },
            [key: EncoderRuleId.DrmKeyMissing] = MakeProfile(video: VideoTranscode()) with
            {
                Drm = new(
                    Scheme: "aes-128",
                    Parameters: new() { [key: "key_uri"] = "https://server/key.bin" }
                ),
            },
        };

        List<string> falsePositives = [];

        foreach ((string ruleId, EncodingProfile profile) in validNeighbors)
        {
            ValidationEnvelope envelope = ProfileRuleValidator.Validate(profile: profile);
            bool fired =
                envelope.Errors.Any(predicate: r => r.Id == ruleId)
                || envelope.Warnings.Any(predicate: r => r.Id == ruleId);

            if (fired)
                falsePositives.Add(item: ruleId);
        }

        falsePositives
            .Should()
            .BeEmpty(
                because: "valid-neighbor profiles must NOT cause ProfileRuleValidator to emit their "
                         + "rule ID (precision check — the guard is too broad if any of these fire). "
                         + "False-positive rule IDs: "
                         + string.Join(separator: ", ", values: falsePositives)
            );
    }

    [Fact]
    public void Catalogue_count_matches_known_total()
    {
        int count = typeof(EncoderRuleId)
            .GetFields(bindingAttr: BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Count(predicate: f => f is { IsLiteral: true, IsInitOnly: false });

        count
            .Should()
            .Be(
                expected: 71,
                because: "EncoderRuleId currently catalogues 71 rules; "
                         + "if this count changed, update the completeness sets above and this guard"
            );
    }
}
