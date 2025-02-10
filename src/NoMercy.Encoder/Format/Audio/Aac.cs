using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Audio;

public class Aac : BaseAudio
{
    public Aac(string audioCodec = "aac")
    {
        SetAudioCodec(audioCodec);
    }

    public override CodecDto[] AvailableCodecs =>
    [
        AudioCodecs.Aac,
    ];

    protected override string[] AvailableContainers =>
    [
        AudioContainers.Aac,
        AudioContainers.M4A,

        VideoContainers.Mkv,
        VideoContainers.Mp4,
        VideoContainers.Flv,
        VideoContainers.Hls
    ];
}