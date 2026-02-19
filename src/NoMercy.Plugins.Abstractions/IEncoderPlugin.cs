namespace NoMercy.Plugins.Abstractions;

public interface IEncoderPlugin : IPlugin
{
    EncodingProfile GetProfile(MediaInfo info);
}
