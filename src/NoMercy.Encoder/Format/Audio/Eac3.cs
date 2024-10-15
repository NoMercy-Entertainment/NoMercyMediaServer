using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Audio;

public class Eac3 : BaseAudio
{
    public Eac3()
    {
        SetAudioCodec(AudioFormats.Eac3);
    }

    protected override CodecDto[] AvailableCodecs =>
    [
        AudioCodecs.Eac3
    ];

    protected override string[] AvailableContainers =>
    [
        VideoContainers.Mkv,
        VideoContainers.Mp4,
        VideoContainers.Hls
    ];
}