namespace NoMercy.Encoder.Profiles;

public enum HdrPolicy
{
    /// <summary>
    /// Copy HDR metadata and colour primaries untouched when the codec and container support it;
    /// tonemap only when passthrough is not possible.
    /// </summary>
    PassthroughWhenPossible,

    /// <summary>
    /// Always run the tonemapping filter, even when the codec could carry HDR metadata natively.
    /// </summary>
    AlwaysTonemap,

    /// <summary>
    /// Preserve HDR metadata unconditionally; never tonemap and never convert to SDR.
    /// </summary>
    AlwaysPreserve,
}
