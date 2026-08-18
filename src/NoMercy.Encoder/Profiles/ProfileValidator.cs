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

using System.Globalization;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Profiles;

public record ProfileValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings
);

public static class ProfileValidator
{
    private static readonly HashSet<string> ForbiddenCustomArgs = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "c:v",
        "c:a",
        "c:s",
        "f",
        "vcodec",
        "acodec",
        "scodec",
    };

    private static readonly HashSet<Container> AudioOnlyContainers =
    [
        Container.Mp3,
        Container.Aac,
        Container.Flac,
        Container.Ogg,
        Container.Mka,
    ];

    public static ProfileValidationResult Validate(EncodingProfile profile)
    {
        List<string> errors = [];
        List<string> warnings = [];

        ValidateContainerCompatibility(profile, errors);
        ValidateAudioBitrate(profile, errors);
        ValidateLadder(profile, errors);
        ValidateCmafCompatibility(profile, errors);
        ValidateCustomArguments(profile, warnings);
        ValidateHlsDerivatives(profile, errors, warnings);
        ValidateSubtitleAcquisition(profile, errors, warnings);

        return new(errors.Count == 0, errors, warnings);
    }

    private static void ValidateContainerCompatibility(EncodingProfile profile, List<string> errors)
    {
        if (profile.Video is { Policy: StreamPolicy.Transcode } video)
        {
            if (!ContainerCompatibility.SupportsVideo(profile.Container, video.Codec))
                errors.Add(
                    $"Container {profile.Container} does not support video codec {video.Codec}. {SuggestContainerForVideoCodec(video.Codec)}"
                );
        }

        foreach (
            AudioOutput audio in (profile.Audio ?? []).Where(a =>
                a.Policy == StreamPolicy.Transcode
            )
        )
        {
            if (!ContainerCompatibility.SupportsAudio(profile.Container, audio.Codec))
                errors.Add(
                    $"Container {profile.Container} does not support audio codec {audio.Codec}. {SuggestContainerForAudioCodec(audio.Codec)}"
                );
        }
    }

    private static void ValidateAudioBitrate(EncodingProfile profile, List<string> errors)
    {
        foreach (
            AudioOutput audio in (profile.Audio ?? []).Where(a =>
                a.Policy == StreamPolicy.Transcode
            )
        )
        {
            if (
                audio.BitrateKbps <= 0
                && audio.Codec != AudioCodecType.Flac
                && audio.Codec != AudioCodecType.TrueHd
            )
                errors.Add($"Audio output for {audio.Codec}: BitrateKbps must be > 0.");
        }
    }

    private static void ValidateLadder(EncodingProfile profile, List<string> errors)
    {
        if (profile.Ladder is null)
            return;
        if (profile.Ladder.Mode == LadderMode.Manual)
        {
            if (profile.Ladder.Rungs is null || profile.Ladder.Rungs.Length == 0)
            {
                errors.Add("Manual ladder requires non-empty Rungs[].");
                return;
            }

            for (int i = 1; i < profile.Ladder.Rungs.Length; i++)
            {
                if (profile.Ladder.Rungs[i].BitrateKbps <= profile.Ladder.Rungs[i - 1].BitrateKbps)
                {
                    errors.Add("Manual ladder rungs must be sorted ascending by bitrate.");
                    break;
                }
            }
        }

        if (profile.Ladder.Mode == LadderMode.Auto && profile.Ladder.AutoConfig is not null)
            ValidateAutoLadder(profile.Ladder.AutoConfig, errors);
    }

    private static void ValidateAutoLadder(AutoLadderConfig config, List<string> errors)
    {
        // Rule 6 — Empty Tiers array (check first; other rules reference tier content)
        if (config.Tiers.Length == 0)
        {
            errors.Add("AutoLadder.Tiers cannot be empty.");
            return;
        }

        // Rule 1 — MinRungs > MaxRungs
        if (config.MinRungs > config.MaxRungs)
            errors.Add(
                $"AutoLadder.MinRungs cannot exceed MaxRungs (MinRungs={config.MinRungs}, MaxRungs={config.MaxRungs})."
            );

        // Rule 2 — AppleHlsRecommended + tier with all-null recommended bitrates
        if (
            config.BitrateStrategy
            is BitrateStrategy.AppleHlsRecommended
                or BitrateStrategy.BitrateLadder
        )
        {
            foreach (LadderTier tier in config.Tiers)
            {
                if (
                    tier.RecommendedBitrateH264Kbps is null
                    && tier.RecommendedBitrateHevcKbps is null
                    && tier.RecommendedBitrateAv1Kbps is null
                )
                    errors.Add(
                        $"Tier '{tier.Label}' missing recommended bitrate for AppleHlsRecommended strategy."
                    );
            }
        }

        // Rule 3 — CrfBased + Crf out of [0, 51]
        if (config.BitrateStrategy == BitrateStrategy.CrfBased)
        {
            if (config.Crf < 0 || config.Crf > 51)
                errors.Add($"AutoLadder.Crf out of range [0, 51] (got {config.Crf}).");
        }

        // Rule 4 — PercentOfSource + SourcePercentage out of (0, 200]
        if (config.BitrateStrategy == BitrateStrategy.PercentOfSource)
        {
            if (config.SourcePercentage <= 0 || config.SourcePercentage > 200)
                errors.Add(
                    "AutoLadder.SourcePercentage must be in (0, 200] "
                        + $"(got {config.SourcePercentage.ToString(CultureInfo.InvariantCulture)})."
                );
        }

        // Rule 5 — Mixed policy requires both codec types
        if (config.CodecPolicy == LadderCodecPolicy.Mixed)
        {
            if (config.LowTierCodec is null || config.HighTierCodec is null)
                errors.Add(
                    "AutoLadder.CodecPolicy=Mixed requires both LowTierCodec and HighTierCodec."
                );
        }

        // Rule 7 — LowTierFramerateMultiplier out of (0, 1.0]
        if (config.LowTierFramerateMultiplier <= 0 || config.LowTierFramerateMultiplier > 1.0)
            errors.Add(
                "AutoLadder.LowTierFramerateMultiplier must be in (0, 1.0] "
                    + $"(got {config.LowTierFramerateMultiplier.ToString(CultureInfo.InvariantCulture)})."
            );
    }

    private static void ValidateCmafCompatibility(EncodingProfile profile, List<string> errors)
    {
        bool cmafOn =
            profile.Hls?.CmafCompatible == true
            && profile.Container is Container.HlsFmp4 or Container.AudioHlsFmp4;
        if (!cmafOn)
            return;

        if (
            profile.Video is { Policy: StreamPolicy.Transcode } video
            && !ContainerCompatibility.IsCmafCompatible(video.Codec)
        )
        {
            errors.Add($"CMAF requires a CMAF-compatible video codec; got {video.Codec}.");
        }

        foreach (
            AudioOutput audio in (profile.Audio ?? []).Where(a =>
                a.Policy == StreamPolicy.Transcode
            )
        )
        {
            if (!ContainerCompatibility.IsCmafCompatible(audio.Codec))
                errors.Add($"CMAF requires a CMAF-compatible audio codec; got {audio.Codec}.");
        }
    }

    private static string SuggestContainerForVideoCodec(VideoCodecType codec)
    {
        IEnumerable<string> compatible = Enum.GetValues<Container>()
            .Where(c => ContainerCompatibility.SupportsVideo(c, codec))
            .Select(c => c.ToString());
        return $"Compatible containers for {codec}: {string.Join(", ", compatible)}.";
    }

    private static string SuggestContainerForAudioCodec(AudioCodecType codec)
    {
        IEnumerable<string> compatible = Enum.GetValues<Container>()
            .Where(c => ContainerCompatibility.SupportsAudio(c, codec))
            .Select(c => c.ToString());
        return $"Compatible containers for {codec}: {string.Join(", ", compatible)}.";
    }

    private static void ValidateCustomArguments(EncodingProfile profile, List<string> warnings)
    {
        if (profile.CustomArguments is null)
            return;
        foreach (string key in profile.CustomArguments.Keys.Where(ForbiddenCustomArgs.Contains))
            warnings.Add(
                $"CustomArgument '{key}' overrides codec/container choice — will hard-reject in a future release."
            );
    }

    private static void ValidateSubtitleAcquisition(
        EncodingProfile profile,
        List<string> errors,
        List<string> warnings
    )
    {
        SubtitleAcquisitionConfig? acq = profile.SubtitleAcquisition;
        if (acq is null || !acq.Enabled)
            return;

        // Rule 1 — acquisition enabled but no subtitle output declared
        if ((profile.Subtitles ?? []).Length == 0)
            errors.Add("SubtitleAcquisition requires at least one declared subtitle output");

        // Rule 2 — acquisition enabled on audio-only container
        if (AudioOnlyContainers.Contains(profile.Container))
            errors.Add("SubtitleAcquisition is incompatible with audio-only containers");

        // Rule 3 — MinRating out of [0, 10]
        if (acq.MinRating < 0 || acq.MinRating > 10)
            errors.Add("SubtitleAcquisition.MinRating must be in [0, 10]");

        // Rule 4 — MaxPerLanguage < 1
        if (acq.MaxPerLanguage < 1)
            errors.Add("SubtitleAcquisition.MaxPerLanguage must be at least 1");

        // Rule 5 — ExactMatchOnly + TitleOnly: warn, never embed
        if (
            acq is
            {
                EmbedPolicy: SubtitleEmbedPolicy.ExactMatchOnly,
                Strategy: SubtitleMatchStrategy.TitleOnly
            }
        )
            warnings.Add(
                "TitleOnly + ExactMatchOnly will never embed; titles can't satisfy exact-match. Acquisition will run sidecar-only."
            );
    }

    /// <summary>
    /// Validates the profile against source media properties (HFR level cap,
    /// 3D stereoscopic, VR spherical). Callers that have a <see cref="MediaInfo"/>
    /// should call this after <see cref="Validate"/> — it produces additional
    /// errors and warnings based on what the source actually contains.
    /// </summary>
    public static ProfileValidationResult ValidateWithSource(
        EncodingProfile profile,
        MediaInfo source
    )
    {
        List<string> errors = [];
        List<string> warnings = [];

        ValidateLevelFrameRateCap(profile, source, errors);
        ValidateStereoscopicSource(profile, source, errors);
        ValidateSphericalMetadata(profile, source, warnings);

        return new(errors.Count == 0, errors, warnings);
    }

    private static void ValidateLevelFrameRateCap(
        EncodingProfile profile,
        MediaInfo source,
        List<string> errors
    )
    {
        if (
            profile.Video is not { Policy: StreamPolicy.Transcode } video
            || string.IsNullOrEmpty(video.Level)
        )
            return;

        if (source.VideoStreams.Count == 0)
            return;

        VideoStreamInfo primaryVideo = source.VideoStreams[0];
        double fps = primaryVideo.AverageFrameRate ?? primaryVideo.FrameRate;

        if (fps <= 0)
            return;

        // A null (or legacy 0) width means "keep source width" — resolve it
        // against the actual source here, since the source is available.
        int effectiveWidth = video.Width is int w and > 0 ? w : primaryVideo.Width;
        int effectiveHeight = video.Height ?? primaryVideo.Height;
        long lumaSamplesPerSec = (long)(effectiveWidth * effectiveHeight * fps);

        CodecLevelFpsCaps.LevelCap? cap = CodecLevelFpsCaps.Lookup(video.Codec, video.Level);
        if (cap is null)
            return;

        if (lumaSamplesPerSec <= cap.MaxLumaSamplesPerSec)
            return;

        CodecLevelFpsCaps.LevelCap? nextFit = CodecLevelFpsCaps.FindNextFit(
            video.Codec,
            lumaSamplesPerSec
        );

        string fix = nextFit is not null
            ? $"Raise level to {nextFit.Level} (supports up to "
                + $"{nextFit.MaxLumaSamplesPerSec.ToString("N0", CultureInfo.InvariantCulture)} luma samples/sec)."
            : "No standard level supports this resolution × frame-rate combination.";

        errors.Add(
            $"Level {video.Level} cap exceeded: "
                + $"{lumaSamplesPerSec.ToString("N0", CultureInfo.InvariantCulture)} luma samples/sec required "
                + $"but level {video.Level} allows "
                + $"{cap.MaxLumaSamplesPerSec.ToString("N0", CultureInfo.InvariantCulture)}. {fix}"
        );
    }

    private static void ValidateStereoscopicSource(
        EncodingProfile profile,
        MediaInfo source,
        List<string> errors
    )
    {
        if (source.StereoMode is null)
            return;

        if (profile.Video is not { Policy: StreamPolicy.Transcode })
            return;

        errors.Add(
            $"3D source detected (stereo_mode={source.StereoMode}). "
                + "NoMercy does not support 3D re-encode. "
                + "Switch the video output policy to Copy to preserve the source."
        );
    }

    private static void ValidateSphericalMetadata(
        EncodingProfile profile,
        MediaInfo source,
        List<string> warnings
    )
    {
        if (source.SphericalProjection is null)
            return;

        if (profile.Video is not { Policy: StreamPolicy.Transcode })
            return;

        warnings.Add(
            $"VR projection metadata ({source.SphericalProjection}) will be stripped on re-encode. "
                + "Use a stream-copy video output to preserve it."
        );
    }

    private static void ValidateHlsDerivatives(
        EncodingProfile profile,
        List<string> errors,
        List<string> warnings
    )
    {
        // When HlsDerivatives is null the caller relies on defaults; contextual flags
        // (like GenerateSpriteVtt) are only meaningful for HLS containers and
        // FinalizeStage skips them when the container is not HLS.
        // Only validate an explicitly-set HlsDerivatives record.
        if (profile.HlsDerivatives is not HlsDerivatives d)
            return;

        bool isHls =
            profile.Container
            is Container.HlsFmp4
                or Container.HlsTs
                or Container.AudioHlsFmp4
                or Container.AudioHlsTs;

        // Rule 1 — SpriteVtt requires HLS container
        if (d.GenerateSpriteVtt && !isHls)
            errors.Add(
                "HlsDerivatives.GenerateSpriteVtt requires HLS container (HlsFmp4 or HlsTs)"
            );

        // Rule 2 — IFramePlaylists cannot run on Copy video
        if (d.GenerateIFramePlaylists && profile.Video is { Policy: StreamPolicy.Copy })
            errors.Add(
                "HlsDerivatives.GenerateIFramePlaylists cannot run on Copy video — keyframe positions must be re-muxed"
            );

        // Rule 3 — warn when MasterPlaylist disabled on HLS (power-user option)
        if (!d.GenerateMasterPlaylist && isHls)
            warnings.Add(
                "GenerateMasterPlaylist=false on HLS container — clients won't have a coordinated entry point"
            );

        // Rule 4 — SpriteVtt numeric ranges
        if (d.SpriteVttIntervalSeconds <= 0 || d.SpriteVttIntervalSeconds > 600)
            errors.Add(
                $"HlsDerivatives.SpriteVttIntervalSeconds must be in [1, 600] (got {d.SpriteVttIntervalSeconds})"
            );

        if (d.SpriteVttColumns <= 0 || d.SpriteVttColumns > 20)
            errors.Add(
                $"HlsDerivatives.SpriteVttColumns must be in [1, 20] (got {d.SpriteVttColumns})"
            );

        if (d.SpriteVttRows <= 0 || d.SpriteVttRows > 20)
            errors.Add($"HlsDerivatives.SpriteVttRows must be in [1, 20] (got {d.SpriteVttRows})");

        if (d.SpriteVttThumbnailWidth <= 0 || d.SpriteVttThumbnailWidth > 1920)
            errors.Add(
                $"HlsDerivatives.SpriteVttThumbnailWidth must be in [1, 1920] (got {d.SpriteVttThumbnailWidth})"
            );
    }
}
