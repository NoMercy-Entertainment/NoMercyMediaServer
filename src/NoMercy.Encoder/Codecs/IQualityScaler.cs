namespace NoMercy.Encoder.Codecs;

/// <summary>
/// Translates a profile-side CRF value to the encoder-native quality knob.
/// Each encoder family exposes a different parameter (CRF, CQ, QP, q:v) with
/// a different range — this interface isolates that per-family knowledge.
/// </summary>
public interface IQualityScaler
{
    /// <summary>
    /// Returns true when this scaler handles the given FFmpeg encoder name.
    /// The resolver picks the first match; <see cref="LinearQualityScaler"/>
    /// is always last (unconditional fallback).
    /// </summary>
    bool Supports(string encoderHandle);

    /// <summary>
    /// Translate a profile-side CRF (<paramref name="sourceCrf"/> on
    /// <c>0..<paramref name="sourceMax"/></c>) to the encoder-native quality
    /// value (<c>0..<paramref name="targetMax"/></c>). The
    /// <paramref name="hint"/> carries codec context the scaler may need
    /// (e.g. AV1 vs HEVC vs H.264).
    /// </summary>
    int Translate(int sourceCrf, int sourceMax, int targetMax, CodecHint hint);
}

/// <summary>
/// Carries codec context into <see cref="IQualityScaler.Translate"/> so
/// scalers that behave differently per codec (e.g. NVENC H.264 vs NVENC AV1)
/// can branch without taking a full CodecRegistry dependency.
/// </summary>
public sealed record CodecHint(string EncoderHandle, VideoCodecType Codec);
