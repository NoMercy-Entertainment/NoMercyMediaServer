using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Audio;

public class Ac3 : BaseAudio
{
    public Ac3()
    {
        SetAudioCodec(AudioFormats.Ac3);
    }

    protected override CodecDto[] AvailableCodecs =>
    [
        AudioCodecs.Ac3
    ];

    protected override string[] AvailableContainers =>
    [
        VideoContainers.Mkv,
        VideoContainers.Mp4,
        VideoContainers.Hls
    ];
}