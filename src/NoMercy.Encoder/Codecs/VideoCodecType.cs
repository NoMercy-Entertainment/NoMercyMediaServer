namespace NoMercy.Encoder.Codecs;

public enum VideoCodecType
{
    H264,
    H265,
    Vp9,
    Av1,

    /// <summary>
    /// Stream copy (no re-encode). Source video bytes are remuxed into the
    /// output container as-is, preserving the exact codec, bit depth, HDR
    /// metadata, and bitrate. Used by archival presets where the user wants
    /// the original quality with a different container (e.g. source MKV →
    /// MKV with new audio tracks). Skips every encode-time decision: no
    /// hardware encoder picked, no CRF translation, no preset / profile /
    /// level / tune applied, no HDR tonemap, no pixel-format conversion.
    /// Encoder pipeline emits <c>-c:v copy</c>.
    /// </summary>
    Copy,
}
