namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Controls the explicit channel-mix matrix applied when reducing a
/// multi-channel source to fewer channels. <see cref="Auto"/> leaves
/// downmixing to ffmpeg's default matrix (via <c>-ac N</c>), while the
/// other modes emit a <c>pan=</c> filter with ITU-R BS.775 coefficients
/// so the output is reproducible across ffmpeg versions.
/// </summary>
public enum DownmixMode
{
    /// <summary>No explicit matrix — ffmpeg's <c>-ac N</c> default is used.</summary>
    Auto,

    /// <summary>
    /// ITU-R BS.775 5.1 → stereo mix. Center channel is folded at -3 dB,
    /// surrounds at -3 dB. Produces a centered, intelligible mix that
    /// preserves dialog.
    /// </summary>
    StereoItuR128,

    /// <summary>Sum all input channels into a single mono output.</summary>
    Mono,

    /// <summary>
    /// Custom pan matrix supplied in the profile as a raw
    /// <see cref="P:NoMercy.Encoder.Profiles.DownmixConfig.CustomPanMatrix"/>
    /// string (e.g. <c>stereo|FL&lt;FL+0.5*FC|FR&lt;FR+0.5*FC</c>).
    /// </summary>
    Custom,
}
