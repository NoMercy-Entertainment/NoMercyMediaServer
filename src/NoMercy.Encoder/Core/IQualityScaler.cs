namespace NoMercy.Encoder.Core;

/// <summary>
///     Translates a logical CRF / quality value on the source scale into the target encoder's
///     native scale. Implementations may use linear math, perceptual curves, VMAF-derived tables,
///     etc. The default is <see cref="LinearQualityScaler"/>; consumers can DI in a better
///     implementation per the journal's roadmap.
/// </summary>
public interface IQualityScaler
{
    /// <param name="sourceCrf">The CRF value the profile asks for.</param>
    /// <param name="sourceMax">
    ///     Maximum value on the source scale (51 for H.264/HEVC CRF, 63 for AV1 CRF).
    /// </param>
    /// <param name="targetMax">Maximum value on the target encoder's native scale.</param>
    /// <param name="hint">Identifies the target encoder for per-encoder quirks.</param>
    int Translate(int sourceCrf, int sourceMax, int targetMax, CodecHint hint);
}
