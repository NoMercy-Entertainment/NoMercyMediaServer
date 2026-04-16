namespace NoMercy.Encoder.ContentAnalysis.Fingerprinting;

/// <summary>
/// A skip-intro / skip-outro boundary, expressed in real-time offsets
/// into the source file. Confidence is 0..1 — the proportion of the
/// matched fingerprint window that was actually similar (not just a
/// low-magnitude false positive).
/// </summary>
public record IntroMarker(TimeSpan Start, TimeSpan End, double Confidence)
{
    public TimeSpan Duration => End - Start;
}
