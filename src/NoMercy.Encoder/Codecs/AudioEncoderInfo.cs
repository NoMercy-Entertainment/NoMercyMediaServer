namespace NoMercy.Encoder.Codecs;

public record AudioEncoderInfo(
    string FfmpegName,
    AudioCodecType CodecType,
    int[] Channels,
    int[] SampleRates,
    int MinBitrateKbps,
    int MaxBitrateKbps,
    int DefaultBitrateKbps,
    bool IsLossless,
    /// <summary>
    /// Exact bitrate ladder the encoder accepts. Null means "continuous
    /// within [Min, Max]" — the encoder picks the nearest internally.
    /// Non-null means ffmpeg will round down to the nearest rung if the
    /// user supplies an off-ladder value, typically without a warning;
    /// validate upfront so the user knows before encoding.
    /// AC3 is the canonical case.
    /// </summary>
    int[]? ValidBitrateLadder = null
);
