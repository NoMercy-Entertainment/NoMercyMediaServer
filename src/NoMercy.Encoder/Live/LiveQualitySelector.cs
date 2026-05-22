namespace NoMercy.Encoder.Live;

/// <summary>
///     Picks the variant a live-transcode session will produce. The selector reads the source's
///     properties + the client's decode capabilities and returns dimensions, codecs and bitrate
///     that the client can actually play.
///     Pure function: no I/O, no ffmpeg, no state. Wire it into a session manager that owns the
///     ffmpeg process.
/// </summary>
public static class LiveQualitySelector
{
    /// <summary>
    ///     Codec preference order when the client supports several. Earlier = preferred. AV1 wins
    ///     because it's the most efficient; HEVC second; H.264 last because it's the broadest
    ///     compatibility fallback. Matches the journal's roadmap of favouring efficient codecs
    ///     when the client says it can decode them.
    /// </summary>
    private static readonly string[] VideoCodecPreference = ["av1", "vp9", "hevc", "h264"];

    private static readonly string[] AudioCodecPreference = ["opus", "ac3", "eac3", "aac", "mp3"];

    public static SelectedVariant Select(SourceMetadata source, ClientCapabilities client)
    {
        string codec = ChooseVideoCodec(client.SupportedVideoCodecs);
        string audioCodec = ChooseAudioCodec(client.SupportedAudioCodecs);

        (int width, int height) = ClampDimensions(
            source.Width,
            source.Height,
            client.MaxWidth,
            client.MaxHeight
        );

        bool convertHdrToSdr = source.IsHdr && !ClientSupportsHdr(client.HdrSupport);
        int bitrate = EstimateBitrateKbps(codec, width, height);

        return new SelectedVariant
        {
            Codec = codec,
            AudioCodec = audioCodec,
            Width = width,
            Height = height,
            BitrateKbps = bitrate,
            ConvertHdrToSdr = convertHdrToSdr,
        };
    }

    /// <summary>
    ///     <c>true</c> when the client claims any HDR transfer it can render. Empty / "none" / "sdr"
    ///     all return <c>false</c>.
    /// </summary>
    public static bool ClientSupportsHdr(string hdrSupport)
    {
        string normalized = (hdrSupport ?? string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "hdr10" or "hdr" or "pq" => true,
            "hlg" => true,
            "dovi" or "dolby_vision" => true,
            _ => false,
        };
    }

    private static string ChooseVideoCodec(IReadOnlyList<string> supported)
    {
        foreach (string preferred in VideoCodecPreference)
        {
            foreach (string clientCodec in supported)
            {
                if (string.Equals(clientCodec, preferred, StringComparison.OrdinalIgnoreCase))
                    return preferred;
            }
        }
        return "h264"; // Universal fallback — every modern player decodes H.264.
    }

    private static string ChooseAudioCodec(IReadOnlyList<string> supported)
    {
        foreach (string preferred in AudioCodecPreference)
        {
            foreach (string clientCodec in supported)
            {
                if (string.Equals(clientCodec, preferred, StringComparison.OrdinalIgnoreCase))
                    return preferred;
            }
        }
        return "aac";
    }

    /// <summary>
    ///     Clamps to the client's max while preserving aspect ratio when one dimension overflows
    ///     and the other doesn't. If both client caps are zero (client didn't advertise a limit),
    ///     return source dimensions unchanged.
    /// </summary>
    public static (int width, int height) ClampDimensions(
        int sourceWidth,
        int sourceHeight,
        int maxWidth,
        int maxHeight
    )
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
            return (0, 0);
        if (maxWidth <= 0 && maxHeight <= 0)
            return (sourceWidth, sourceHeight);

        double sourceAspect = (double)sourceWidth / sourceHeight;
        int width = sourceWidth;
        int height = sourceHeight;

        if (maxWidth > 0 && width > maxWidth)
        {
            width = maxWidth;
            height = (int)Math.Round(width / sourceAspect);
        }

        if (maxHeight > 0 && height > maxHeight)
        {
            height = maxHeight;
            width = (int)Math.Round(height * sourceAspect);
        }

        return (EvenInt(width), EvenInt(height));
    }

    /// <summary>
    ///     ffmpeg's H.264 / HEVC encoders require even dimensions for chroma subsampling. Round
    ///     down to the nearest even number rather than up, so the result never exceeds the
    ///     requested max.
    /// </summary>
    private static int EvenInt(int value) => value - (value & 1);

    /// <summary>
    ///     Rough target bitrate per resolution tier per codec, matching the auto-ladder defaults
    ///     described in part 03 of the journal. Real-world content complexity skew is left to
    ///     the encoder's plan stage; this is the live-transcode starting point.
    /// </summary>
    public static int EstimateBitrateKbps(string codec, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return 1000;

        int pixels = width * height;
        bool isEfficientCodec =
            codec.Equals("av1", StringComparison.OrdinalIgnoreCase)
            || codec.Equals("hevc", StringComparison.OrdinalIgnoreCase)
            || codec.Equals("vp9", StringComparison.OrdinalIgnoreCase);

        if (pixels >= 3840 * 2160)
            return isEfficientCodec ? 7500 : 12000; // 2160p
        if (pixels >= 2560 * 1440)
            return isEfficientCodec ? 5000 : 8000; // 1440p
        if (pixels >= 1920 * 1080)
            return isEfficientCodec ? 3000 : 4500; // 1080p
        if (pixels >= 1280 * 720)
            return isEfficientCodec ? 1700 : 2500; // 720p
        if (pixels >= 854 * 480)
            return isEfficientCodec ? 750 : 1100; // 480p
        return isEfficientCodec ? 400 : 600; // 360p and below
    }
}
