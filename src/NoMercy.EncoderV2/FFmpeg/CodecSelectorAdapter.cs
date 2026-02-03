using NoMercy.Encoder.Core;

namespace NoMercy.EncoderV2.FFmpeg;

/// <summary>
/// Adapts V1's CodecSelector for use in EncoderV2
/// Provides hardware-accelerated codec selection
/// </summary>
public class CodecSelectorAdapter : ICodecSelector
{
    public string SelectH264Codec()
    {
        return CodecSelector.SelectH264Codec().Value;
    }

    public string SelectH265Codec()
    {
        return CodecSelector.SelectH265Codec().Value;
    }

    public string ResolveCodec(string requestedCodec)
    {
        return CodecSelector.ResolveBestCodec(requestedCodec);
    }
}
