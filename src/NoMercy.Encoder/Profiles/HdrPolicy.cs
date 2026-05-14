namespace NoMercy.Encoder.Profiles;

public enum HdrPolicy
{
    PassthroughWhenPossible,
    AlwaysTonemap,
    AlwaysPreserve,

    /// <summary>
    /// When the source is HDR, emit BOTH the HDR variant (passthrough) AND a
    /// tonemapped SDR companion per resolution. When the source is SDR, behaves
    /// like AlwaysTonemap (single SDR output per resolution). Mirrors the
    /// example media layout: video_WxH/ (HDR) + video_WxH_SDR/ (tonemap).
    /// </summary>
    EmitHdrAndSdr,
}
