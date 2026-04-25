namespace NoMercy.Encoder.Codecs;

/// <summary>
/// Produces spec-accurate CODECS attribute strings for HLS EXT-X-STREAM-INF
/// and EXT-X-MEDIA tags as defined in:
///   • RFC 6381 §3.3 — codec string format
///   • ISO 14496-15 Annex E — avc1 / hvc1 parameter encoding
///   • AV1 Codec ISO Media File Format Binding §5 — av01 parameter encoding
///   • MP4 Registration Authority — mp4a object type descriptors
/// </summary>
public static class HlsCodecsStringBuilder
{
    // -----------------------------------------------------------------------
    // Video
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the avc1 codec string for H.264.
    /// Format: avc1.PPCCLL where PP=profile_idc (hex), CC=constraint flags
    /// (hex), LL=level_idc (hex).
    ///
    /// Profile IDC values:
    ///   66 (0x42) = Baseline
    ///   77 (0x4D) = Main
    ///   100 (0x64) = High
    ///   110 (0x6E) = High 10
    ///   122 (0x7A) = High 4:2:2
    ///   244 (0xF4) = High 4:4:4 Predictive
    ///
    /// Constraint byte: 0x00 for High/Main, 0x40 for Baseline (constrained).
    /// Level examples: "4.0" → 0x28 (40), "4.1" → 0x29, "5.0" → 0x32, "5.1" → 0x33.
    /// </summary>
    public static string ForH264(string? profile, string? level)
    {
        byte profileIdc = NormalizeH264Profile(profile) switch
        {
            "baseline" => 0x42,
            "main" => 0x4D,
            "high10" => 0x6E,
            "high422" => 0x7A,
            "high444" => 0xF4,
            _ => 0x64, // high (default)
        };

        // Constraint byte: Baseline uses 0x40 (constrained_set1_flag),
        // all others use 0x00 for broadest player compat.
        byte constraintByte = profileIdc == 0x42 ? (byte)0x40 : (byte)0x00;

        byte levelIdc = ParseH264Level(level);

        return $"avc1.{profileIdc:X2}{constraintByte:X2}{levelIdc:X2}";
    }

    /// <summary>
    /// Returns the hvc1 codec string for HEVC/H.265.
    /// Format: hvc1.P.CCC.LLL.BB where:
    ///   P   = general_profile_space (empty) + general_profile_idc (1 or 2)
    ///   CCC = general_profile_compatibility_flags (hex, 32-bit, e.g. 4 → "4")
    ///   LLL = tier ('L' or 'H') + general_level_idc (integer * 3 for 30 fps base)
    ///   BB  = general_constraint_indicator_flags (hex bytes, trailing zeros dropped)
    ///
    /// Common values for HLS delivery:
    ///   SDR Main (profile 1, compat 0x60000000 → "6"): hvc1.1.6.L93.B0
    ///   HDR Main10 (profile 2, compat 0x40000000 → "4"): hvc1.2.4.L120.B0
    /// </summary>
    public static string ForHevc(string? profile, string? level, bool tenBit)
    {
        // Profile
        int profileIdc = tenBit ? 2 : 1; // 1=Main, 2=Main10

        // Compatibility flags: Main=0x60000000, Main10=0x40000000
        // Encoded as the leading non-zero hex word only.
        string compatFlags = tenBit ? "4" : "6";

        // Level: spec uses level_idc = level × 30 (e.g. L4.0 → 120, L3.1 → 93)
        int levelIdc = ParseHevcLevel(level, tenBit);
        string levelStr = $"L{levelIdc}";

        return $"hvc1.{profileIdc}.{compatFlags}.{levelStr}.B0";
    }

    /// <summary>
    /// Returns the av01 codec string for AV1.
    /// Format: av01.P.LLT.DD where:
    ///   P  = profile (0=Main, 1=High, 2=Professional)
    ///   LL = level index (two-digit, padded, e.g. "04" for level 4.0)
    ///   T  = tier ('M'=Main, 'H'=High)
    ///   DD = bit depth (08 or 10)
    ///
    /// Level index mapping (AV1 spec Table A.1):
    ///   2.0→00, 2.1→01, 3.0→04, 3.1→05, 4.0→08, 4.1→09,
    ///   5.0→12, 5.1→13, 5.2→14, 5.3→15, 6.0→16, 6.1→17, 6.2→18, 6.3→19
    /// </summary>
    public static string ForAv1(string? level, bool tenBit)
    {
        int levelIndex = ParseAv1LevelIndex(level, tenBit);
        string levelStr = levelIndex.ToString("D2");
        string bitDepth = tenBit ? "10" : "08";
        return $"av01.0.{levelStr}M.{bitDepth}";
    }

    // -----------------------------------------------------------------------
    // Audio
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns mp4a.40.2 for AAC-LC (ISO 14496-3 AudioObjectType=2).
    /// </summary>
    public static string ForAacLc() => "mp4a.40.2";

    /// <summary>
    /// Returns mp4a.40.5 for HE-AAC / AAC+ (SBR, AudioObjectType=5).
    /// </summary>
    public static string ForHeAac() => "mp4a.40.5";

    /// <summary>
    /// Returns ac-3 for Dolby Digital (AC-3).
    /// </summary>
    public static string ForAc3() => "ac-3";

    /// <summary>
    /// Returns ec-3 for Dolby Digital Plus (E-AC-3).
    /// </summary>
    public static string ForEac3() => "ec-3";

    // -----------------------------------------------------------------------
    // Encoder-name dispatch helpers (used by PlaylistGenerator)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Derives the CODECS string from FFmpeg encoder name + plan metadata.
    /// Covers all video codecs the encoder supports.
    /// </summary>
    public static string VideoCodecString(
        string encoderName,
        string? profile,
        string? level,
        bool tenBit
    )
    {
        string lower = encoderName.ToLowerInvariant();

        if (lower.Contains("264") || lower.Contains("x264") || lower.Contains("h264"))
            return ForH264(profile, level);

        if (lower.Contains("265") || lower.Contains("hevc"))
            return ForHevc(profile, level, tenBit);

        if (lower.Contains("av1") || lower.Contains("svtav1") || lower.Contains("aom"))
            return ForAv1(level, tenBit);

        // VP9 — no RFC 6381 standardised short-form; use vp09 signaling
        if (lower.Contains("vp9") || lower.Contains("libvpx"))
        {
            string bitDepth = tenBit ? "10" : "08";
            return $"vp09.00.41.{bitDepth}";
        }

        // Fallback: treat as H.264 High 4.0
        return ForH264(profile ?? "high", level ?? "4.0");
    }

    /// <summary>
    /// Derives the CODECS string from FFmpeg encoder name for audio.
    /// </summary>
    public static string AudioCodecString(string encoderName, bool heAac = false)
    {
        return encoderName.ToLowerInvariant() switch
        {
            "aac" or "libfdk_aac" => heAac ? ForHeAac() : ForAacLc(),
            "ac3" => ForAc3(),
            "eac3" => ForEac3(),
            "libopus" or "opus" => "opus",
            _ => ForAacLc(),
        };
    }

    // -----------------------------------------------------------------------
    // Private parsing helpers
    // -----------------------------------------------------------------------

    private static string NormalizeH264Profile(string? profile)
    {
        if (string.IsNullOrEmpty(profile))
            return "high";

        return profile.ToLowerInvariant() switch
        {
            "baseline" or "constrained baseline" or "cb" => "baseline",
            "main" or "m" => "main",
            "high 10" or "high10" or "hi10p" => "high10",
            "high 4:2:2" or "high422" or "hi422p" => "high422",
            "high 4:4:4" or "high444" or "hi444pp" => "high444",
            _ => "high",
        };
    }

    private static byte ParseH264Level(string? level)
    {
        if (string.IsNullOrEmpty(level))
            return 0x28; // 4.0

        // Handle both "4.0" and "40" style input
        string normalized = level.Replace(".", "");
        if (int.TryParse(normalized, out int numeric))
            return (byte)numeric;

        return level switch
        {
            "1" or "1.0" => 10,
            "1.1" => 11,
            "1.2" => 12,
            "1.3" => 13,
            "2" or "2.0" => 20,
            "2.1" => 21,
            "2.2" => 22,
            "3" or "3.0" => 30,
            "3.1" => 31,
            "3.2" => 32,
            "4" or "4.0" => 40,
            "4.1" => 41,
            "4.2" => 42,
            "5" or "5.0" => 50,
            "5.1" => 51,
            "5.2" => 52,
            "6" or "6.0" => 60,
            "6.1" => 61,
            "6.2" => 62,
            _ => 40,
        };
    }

    private static int ParseHevcLevel(string? level, bool tenBit)
    {
        // HEVC level_idc = level_value × 30
        // Common defaults: SDR → L3.1 (93), HDR10 → L4.0 (120)
        if (string.IsNullOrEmpty(level))
            return tenBit ? 120 : 93;

        return level switch
        {
            "1" or "1.0" => 30,
            "2" or "2.0" => 60,
            "2.1" => 63,
            "3" or "3.0" => 90,
            "3.1" => 93,
            "4" or "4.0" => 120,
            "4.1" => 123,
            "5" or "5.0" => 150,
            "5.1" => 153,
            "5.2" => 156,
            "6" or "6.0" => 180,
            "6.1" => 183,
            "6.2" => 186,
            _ => tenBit ? 120 : 93,
        };
    }

    private static int ParseAv1LevelIndex(string? level, bool tenBit = false)
    {
        // AV1 spec Table A.1 level index mapping.
        // Defaults are opinionated by bit depth: 10-bit content typically
        // targets 4K → level 5.3 (index 15); 8-bit defaults to 4.0 (index 8).
        if (string.IsNullOrEmpty(level))
            return tenBit ? 15 : 8;

        return level switch
        {
            "2.0" => 0,
            "2.1" => 1,
            "3.0" => 4,
            "3.1" => 5,
            "4.0" => 8,
            "4.1" => 9,
            "5.0" => 12,
            "5.1" => 13,
            "5.2" => 14,
            "5.3" => 15,
            "6.0" => 16,
            "6.1" => 17,
            "6.2" => 18,
            "6.3" => 19,
            _ => 8,
        };
    }
}
