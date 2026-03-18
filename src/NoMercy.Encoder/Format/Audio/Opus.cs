using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Audio;

public class Opus : BaseAudio
{
    public Opus(string audioCodec = "libopus")
    {
        SetAudioCodec(audioCodec);
        AddCustomArgument("-strict", "-2");
    }

    public override CodecDto[] AvailableCodecs =>
    [
        AudioCodecs.LibOpus,
        AudioCodecs.Opus
    ];

    protected override string[] AvailableContainers =>
    [
        AudioContainers.Ogg,

        VideoContainers.Mkv,
        VideoContainers.Mp4,
        VideoContainers.Webm,
        VideoContainers.Hls
    ];
}