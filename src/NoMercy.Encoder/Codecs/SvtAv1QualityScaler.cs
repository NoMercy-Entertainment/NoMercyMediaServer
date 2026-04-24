namespace NoMercy.Encoder.Codecs;

/// <summary>
/// Quality scaler for SVT-AV1: <c>libsvtav1</c>.
///
/// <para>SVT-AV1 uses CRF 0–63 natively — the same scale the profile is
/// written in for AV1 outputs — so the value passes through unchanged. No
/// scaling or inversion needed.</para>
///
/// <para>Source: SVT-AV1 encoder user guide §CRF mode;
/// Av1Definition.cs QualityRange(Min: 0, Max: 63, Default: 35).</para>
/// </summary>
public sealed class SvtAv1QualityScaler : IQualityScaler
{
    public bool Supports(string encoderHandle) =>
        string.Equals(encoderHandle, "libsvtav1", StringComparison.OrdinalIgnoreCase);

    public int Translate(int sourceCrf, int sourceMax, int targetMax, CodecHint hint)
    {
        int svtMax = targetMax > 0 ? targetMax : 63;
        return Math.Clamp(sourceCrf, 0, svtMax);
    }
}
