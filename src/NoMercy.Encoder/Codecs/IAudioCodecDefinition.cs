namespace NoMercy.Encoder.Codecs;

public interface IAudioCodecDefinition
{
    AudioCodecType CodecType { get; }
    AudioEncoderInfo Encoder { get; }
}
