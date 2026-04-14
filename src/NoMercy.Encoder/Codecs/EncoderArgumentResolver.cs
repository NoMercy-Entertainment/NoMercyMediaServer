namespace NoMercy.Encoder.Codecs;

using NoMercy.Encoder.Profiles;

/// <summary>
/// Maps profile-level encoding parameters (preset, profile, CRF) to the correct
/// FFmpeg arguments for the resolved encoder. Profiles are written in libx264 terms
/// (e.g. preset=fast, profile=high, crf=22) but the actual encoder may be NVENC,
/// AMF, QSV, VAAPI, or VideoToolbox — each with different flag names and value sets.
/// </summary>
public static class EncoderArgumentResolver
{
    /// <summary>
    /// Returns the validated preset for the target encoder.
    /// Falls back to the encoder's middle preset if the profile preset is unsupported.
    /// </summary>
    public static string? ResolvePreset(string? profilePreset, EncoderInfo encoder)
    {
        if (encoder.Presets.Length == 0)
            return null; // Encoder has no preset concept (e.g. VAAPI)

        if (profilePreset is not null && encoder.Presets.Contains(profilePreset))
            return profilePreset;

        // Default to middle preset (balanced speed/quality)
        return encoder.Presets[encoder.Presets.Length / 2];
    }

    /// <summary>
    /// Returns the validated profile for the target encoder.
    /// Falls back to the encoder's first (safest) profile if unsupported.
    /// </summary>
    public static string? ResolveProfile(string? profileValue, EncoderInfo encoder)
    {
        if (encoder.Profiles.Length == 0)
            return null;

        if (profileValue is not null && encoder.Profiles.Contains(profileValue))
            return profileValue;

        return encoder.Profiles[0];
    }

    /// <summary>
    /// Maps CRF to the correct rate control flag for the encoder.
    /// Software encoders use -crf. Hardware encoders use -cq (NVENC), -qp (AMF/QSV),
    /// -global_quality (Intel ICQ), or -q:v (VideoToolbox).
    ///
    /// Returns the CRF value to pass to OutputOptions (0 if moved to extraFlags)
    /// and populates extraFlags with the hardware-specific quality flag.
    /// </summary>
    public static int ResolveQuality(
        int profileCrf,
        ResolvedCodec resolved,
        Dictionary<string, string> extraFlags
    )
    {
        if (profileCrf <= 0)
            return 0;

        bool supportsCrf = resolved.EncoderInfo.SupportedRateControl.Contains(RateControlMode.Crf);
        if (supportsCrf)
            return profileCrf; // Software encoder — use -crf as-is

        // Hardware encoder — map to the correct quality flag
        string qualityFlag = resolved.DefaultRateControl switch
        {
            RateControlMode.Cq => "-cq",
            RateControlMode.Icq => "-global_quality",
            RateControlMode.QualityLevel => "-q:v",
            _ => "-qp",
        };
        extraFlags[qualityFlag] = profileCrf.ToString();
        return 0; // Don't emit -crf
    }

    /// <summary>
    /// Resolves output dimensions. Never upscales beyond source. Ensures even height.
    /// </summary>
    public static (int width, int height) ResolveDimensions(
        VideoOutput profile,
        int sourceWidth,
        int sourceHeight
    )
    {
        int outputWidth = Math.Min(profile.Width, sourceWidth);
        int outputHeight = profile.Height ?? (outputWidth * sourceHeight / sourceWidth);

        // Encoders require even dimensions
        if (outputHeight % 2 != 0)
            outputHeight++;

        return (outputWidth, outputHeight);
    }
}
