namespace NoMercy.Encoder.Profiles;

public enum CodecProfile
{
    /// <summary>
    /// Let the encoder choose the most appropriate profile for the codec and settings.
    /// </summary>
    Auto,

    /// <summary>
    /// H.264 Baseline — lowest complexity; best for devices with limited decoding capability.
    /// </summary>
    Baseline,

    /// <summary>
    /// H.264/H.265 Main — balanced profile suitable for most streaming targets.
    /// </summary>
    Main,

    /// <summary>
    /// H.265 Main 10 — Main profile with 10-bit colour support.
    /// </summary>
    Main10,

    /// <summary>
    /// H.264 High — highest quality for H.264; required for 1080p+ with reference frames.
    /// </summary>
    High,

    /// <summary>
    /// H.264 High 10 — High profile with 10-bit colour support.
    /// </summary>
    High10,
}
