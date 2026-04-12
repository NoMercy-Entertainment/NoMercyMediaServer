namespace NoMercy.Encoder.V3.Codecs;

using NoMercy.Encoder.V3.Codecs.Definitions;

public class CodecRegistry
{
    private readonly Dictionary<VideoCodecType, ICodecDefinition> _videoDefinitions;
    private readonly Dictionary<string, EncoderInfo> _encodersByName;

    public CodecRegistry()
    {
        ICodecDefinition[] definitions =
        [
            new H264Definition(),
            new H265Definition(),
            new Av1Definition(),
            new Vp9Definition(),
        ];

        _videoDefinitions = definitions.ToDictionary(d => d.CodecType);

        _encodersByName = new Dictionary<string, EncoderInfo>();
        foreach (ICodecDefinition def in definitions)
        {
            foreach (EncoderInfo encoder in def.Encoders)
            {
                _encodersByName[encoder.FfmpegName] = encoder;
            }
        }
    }

    public ICodecDefinition GetVideoDefinition(VideoCodecType codecType) =>
        _videoDefinitions[codecType];

    public EncoderInfo? GetVideoEncoderByName(string ffmpegName) =>
        _encodersByName.GetValueOrDefault(ffmpegName);

    public AudioEncoderInfo GetAudioEncoder(AudioCodecType codecType) =>
        AudioCodecDefinitions.GetEncoder(codecType);
}
