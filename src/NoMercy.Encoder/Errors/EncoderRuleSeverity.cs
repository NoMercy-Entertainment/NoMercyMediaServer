namespace NoMercy.Encoder.Errors;

/// <summary>
/// Severity attached to an <see cref="EncoderRule"/>. The dashboard
/// renders Error as a red chip, Warning as yellow, Info as a neutral
/// pill. <see cref="ValidationEnvelope.Valid"/> is true only when no
/// rule of severity Error is present.
/// </summary>
public enum EncoderRuleSeverity
{
    Info,
    Warning,
    Error,
}
