namespace NoMercy.EncoderV2.Specifications.HLS;

/// <summary>
/// Defines how HLS streams should be organized in the output
/// </summary>
public enum HLSOutputMode
{
    /// <summary>
    /// Combined video and audio in single HLS stream (current default)
    /// </summary>
    Combined,

    /// <summary>
    /// Separate video-only and audio-only HLS streams (V1-compatible)
    /// Creates folder structure: video_WxH_SDR/, audio_lang_codec/
    /// </summary>
    SeparateStreams
}
