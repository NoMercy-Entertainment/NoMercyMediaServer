using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Audio;

public class Vorbis : BaseAudio
{
    public Vorbis(string audioCodec = "libvorbis")
    {
        SetAudioCodec(audioCodec);
        AddCustomArgument("-strict", "-2");
    }

    public override CodecDto[] AvailableCodecs =>
    [
        AudioCodecs.LibVorbis,
        AudioCodecs.Vorbis
    ];

    protected override string[] AvailableContainers =>
    [
        AudioContainers.Ogg,

        VideoContainers.Mkv,
        VideoContainers.Webm,
        VideoContainers.Hls
    ];
}