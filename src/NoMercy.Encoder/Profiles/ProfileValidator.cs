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

    public static ProfileValidationResult Validate(EncodingProfile profile)
    {
        List<string> errors = [];
        List<string> warnings = [];

        ValidateContainerCompatibility(profile, errors);
        ValidateAudioBitrate(profile, errors);
        ValidateLadder(profile, errors);
        ValidateCmafCompatibility(profile, errors);
        ValidateCustomArguments(profile, warnings);

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

        foreach (AudioOutput audio in profile.Audio.Where(a => a.Policy == StreamPolicy.Transcode))
        {
            if (!ContainerCompatibility.SupportsAudio(profile.Container, audio.Codec))
                errors.Add(
                    $"Container {profile.Container} does not support audio codec {audio.Codec}. {SuggestContainerForAudioCodec(audio.Codec)}"
                );
        }
    }

    private static void ValidateAudioBitrate(EncodingProfile profile, List<string> errors)
    {
        foreach (AudioOutput audio in profile.Audio.Where(a => a.Policy == StreamPolicy.Transcode))
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
                    $"AutoLadder.SourcePercentage must be in (0, 200] (got {config.SourcePercentage})."
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
                $"AutoLadder.LowTierFramerateMultiplier must be in (0, 1.0] (got {config.LowTierFramerateMultiplier})."
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

        foreach (AudioOutput audio in profile.Audio.Where(a => a.Policy == StreamPolicy.Transcode))
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
}
