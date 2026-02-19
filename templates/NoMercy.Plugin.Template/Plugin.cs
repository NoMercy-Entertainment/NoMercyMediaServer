using Microsoft.Extensions.Logging;
using NoMercy.Plugins.Abstractions;

namespace NoMercy.Plugin.Template;

public class Plugin : IPlugin
{
    public string Name => "NoMercy.Plugin.Template";
    public string Description => "PLUGIN-DESCRIPTION-PLACEHOLDER";
    public Guid Id { get; } = Guid.Parse("PLUGIN-GUID-PLACEHOLDER");
    public Version Version { get; } = new(1, 0, 0);

    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        _context = context;
        _context.Logger.LogInformation("{PluginName} v{Version} initialized", Name, Version);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
