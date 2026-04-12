namespace NoMercy.Encoder.V3.Codecs;

public interface ICodecDefinition
{
    VideoCodecType CodecType { get; }
    EncoderInfo[] Encoders { get; }
}
