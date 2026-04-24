namespace NoMercy.Encoder.Profiles;

public enum BitDepthPolicy
{
    /// <summary>
    /// Emit a warning when the source bit depth cannot be preserved, then downgrade automatically.
    /// </summary>
    WarnAndDowngrade,

    /// <summary>
    /// Fail the job if the requested bit depth cannot be satisfied exactly.
    /// </summary>
    Strict,

    /// <summary>
    /// Switch to a software encoder rather than downgrade bit depth, even if hardware is preferred.
    /// </summary>
    PreferSoftware,

    /// <summary>
    /// Downgrade bit depth silently without emitting any diagnostic.
    /// </summary>
    SilentDowngrade,
}
