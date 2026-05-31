namespace NoMercy.Encoder.Codecs;

public interface ICodecDefinition
{
    VideoCodecType CodecType { get; }
    EncoderInfo[] Encoders { get; }
}
