namespace NoMercy.Encoder.Core;

/// <summary>
///     Linear proportional mapping: <c>target = round(sourceCrf × targetMax / sourceMax)</c>. Not
///     perceptually perfect — see the journal's note that the roadmap allows publishing better
///     scaling curves via DI — but orders of magnitude closer than passing the source CRF through
///     unchanged. Applies per-encoder clamps:
///     <list type="bullet">
///         <item>QSV H.264/HEVC accept 1-51, not 0-51 — clamp lower bound to 1.</item>
///         <item>Negative source values clamp to 0 before scaling.</item>
///         <item>Result is always within <c>[0, targetMax]</c>, then per-encoder lower bound.</item>
///     </list>
/// </summary>
public sealed class LinearQualityScaler : IQualityScaler
{
    public int Translate(int sourceCrf, int sourceMax, int targetMax, CodecHint hint)
    {
        if (sourceMax <= 0)
            return 0;

        int clampedSource = Math.Max(0, sourceCrf);
        int translated = (int)Math.Round(clampedSource * (double)targetMax / sourceMax);

        if (translated < 0)
            translated = 0;
        if (translated > targetMax)
            translated = targetMax;

        return hint switch
        {
            CodecHint.QsvH264 or CodecHint.QsvHevc => Math.Max(1, translated),
            _ => translated,
        };
    }
}
