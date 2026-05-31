namespace NoMercy.Encoder.ContentAnalysis.Fingerprinting;

/// <summary>
/// A chromaprint-derived audio fingerprint. Each element of
/// <see cref="Hashes"/> represents a short audio frame (~0.125 s at the
/// 11025 Hz mono sample rate chromaprint uses internally); similar frames
/// have small Hamming distance between their 32-bit hashes.
/// </summary>
/// <param name="Hashes">Chromaprint hash values in frame order.</param>
/// <param name="FrameDuration">
/// Real-time duration one hash represents. chromaprint advances one hash
/// every 2048 samples at 11025 Hz which is ~0.1858 s, but the exact value
/// is carried so a cheap test fixture can fake a different rate.
/// </param>
/// <param name="StartTime">
/// When the first hash corresponds to in the source file. Needed because
/// we fingerprint a window (first N minutes for intros, last N for outros)
/// rather than the whole file.
/// </param>
public record AudioFingerprint(uint[] Hashes, TimeSpan FrameDuration, TimeSpan StartTime)
{
    public TimeSpan EndTime => StartTime + FrameDuration * Hashes.Length;

    public TimeSpan FrameToTime(int frameIndex) => StartTime + FrameDuration * frameIndex;
}

/// <summary>
/// Fingerprinting window. Keeping it small matters — a 5-minute window at
/// ~8 fps yields ~2400 frames and every pairwise compare is quadratic.
/// </summary>
public record FingerprintWindow(TimeSpan Start, TimeSpan Duration);
