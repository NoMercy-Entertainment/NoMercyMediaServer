namespace NoMercy.EncoderV2.FFmpeg;

/// <summary>
/// Selects the best available video codec based on hardware acceleration
/// </summary>
public interface ICodecSelector
{
    /// <summary>
    /// Selects the best H.264 codec for the current system
    /// </summary>
    string SelectH264Codec();

    /// <summary>
    /// Selects the best H.265 codec for the current system
    /// </summary>
    string SelectH265Codec();

    /// <summary>
    /// Resolves a requested codec to the best available codec
    /// Handles fallback when GPU-specific codec is not available
    /// </summary>
    string ResolveCodec(string requestedCodec);
}
