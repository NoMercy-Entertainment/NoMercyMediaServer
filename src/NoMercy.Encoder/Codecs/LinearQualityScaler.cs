namespace NoMercy.Encoder.Codecs;

/// <summary>
/// Default quality scaler — proportional linear mapping clamped to
/// [0, targetMax]. Used when no encoder-specific scaler matches.
///
/// <para>Formula: <c>Round(sourceCrf / sourceMax * targetMax)</c>, then
/// clamped to [0, targetMax].</para>
///
/// <para>Source: ffmpeg-user list "CRF scaling across encoders" thread;
/// x264 / x265 encode guides (trac.ffmpeg.org/wiki/Encode/H.264).</para>
/// </summary>
public sealed class LinearQualityScaler : IQualityScaler
{
    /// <summary>
    /// Always returns true — this scaler is the unconditional fallback.
    /// The resolver must register it last so specific scalers win first.
    /// </summary>
    public bool Supports(string encoderHandle) => true;

    public int Translate(int sourceCrf, int sourceMax, int targetMax, CodecHint hint)
    {
        if (sourceMax <= 0)
            return 0;

        int scaled = (int)Math.Round((double)sourceCrf / sourceMax * targetMax);
        return Math.Clamp(scaled, 0, targetMax);
    }
}
