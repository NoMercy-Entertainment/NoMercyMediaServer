namespace NoMercy.Encoder.V3.Codecs;

public interface IAudioCodecDefinition
{
    AudioCodecType CodecType { get; }
    AudioEncoderInfo Encoder { get; }
}
