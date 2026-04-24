namespace NoMercy.Encoder.Profiles;

public enum HardwarePreference
{
    /// <summary>
    /// Use hardware acceleration when available; fall back to software if not supported.
    /// </summary>
    PreferHardware,

    /// <summary>
    /// Prefer software encoding to maximise quality, even when hardware is available.
    /// </summary>
    PreferQuality,

    /// <summary>
    /// Require hardware acceleration; fail the job if no supported hardware is detected.
    /// </summary>
    ForceHardware,

    /// <summary>
    /// Always use software encoding regardless of available hardware accelerators.
    /// </summary>
    ForceSoftware,
}
