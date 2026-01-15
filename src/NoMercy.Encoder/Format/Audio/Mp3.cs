using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Audio;

public class Mp3 : BaseAudio
{
    public Mp3(string audioCodec = "libmp3lame")
    {
        SetAudioCodec(audioCodec);
    }

    public override CodecDto[] AvailableCodecs =>
    [
        AudioCodecs.Mp3
    ];

    protected override string[] AvailableContainers =>
    [
        AudioContainers.Mp3,

        VideoContainers.Mkv,
        VideoContainers.Mp4,
        VideoContainers.Flv,
        VideoContainers.Hls
    ];
}