namespace NoMercy.Plugins.Abstractions;

public interface IEncoderPlugin : IPlugin
{
    // Return a profile to override the configured one for this source, or null to
    // opt out (the configured profile stands). The first plugin returning non-null
    // wins.
    EncodingProfile? GetProfile(MediaInfo info);
}
