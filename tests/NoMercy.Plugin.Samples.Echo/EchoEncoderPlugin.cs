using Microsoft.Extensions.Logging;
using NoMercy.Plugins.Abstractions;

namespace NoMercy.Plugin.Samples.Echo;

public class EchoEncoderPlugin : IEncoderPlugin
{
    public string Name => "Echo";
    public string Description => "Sample plugin — returns a fixed encoding profile for any input.";
    public Guid Id { get; } = Guid.Parse("11111111-2222-3333-4444-555555555555");
    public Version Version { get; } = new(0, 1, 0);

    public void Initialize(IPluginContext context)
    {
        context.Logger.LogInformation("Echo plugin initialized");
    }

    public EncodingProfile GetProfile(MediaInfo info) =>
        new()
        {
            Name = $"Echo-{Path.GetFileName(info.FilePath)}",
            VideoCodec = "h264",
            AudioCodec = "aac",
        };

    public void Dispose() { }
}
