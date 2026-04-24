namespace NoMercy.Encoder.Codecs;

using NoMercy.Encoder.Codecs.Definitions;

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

        _encodersByName = new();
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

    public IEnumerable<(VideoCodecType CodecType, EncoderInfo Encoder)> EnumerateVideoEncoders()
    {
        foreach ((VideoCodecType codecType, ICodecDefinition def) in _videoDefinitions)
        {
            foreach (EncoderInfo encoder in def.Encoders)
                yield return (codecType, encoder);
        }
    }

    /// <summary>
    /// Returns true when the encoder handle is a hardware-accelerated encoder.
    /// Detection is based on well-known vendor suffixes rather than the registry
    /// because callers may supply handles that are not yet registered
    /// (e.g. from FfmpegCapabilities probes).
    /// </summary>
    public static bool IsHardware(string ffmpegEncoderName)
    {
        return ffmpegEncoderName.Contains("_nvenc", StringComparison.OrdinalIgnoreCase)
            || ffmpegEncoderName.Contains("_qsv", StringComparison.OrdinalIgnoreCase)
            || ffmpegEncoderName.Contains("_amf", StringComparison.OrdinalIgnoreCase)
            || ffmpegEncoderName.Contains("_videotoolbox", StringComparison.OrdinalIgnoreCase)
            || ffmpegEncoderName.Contains("_vaapi", StringComparison.OrdinalIgnoreCase);
    }
}
