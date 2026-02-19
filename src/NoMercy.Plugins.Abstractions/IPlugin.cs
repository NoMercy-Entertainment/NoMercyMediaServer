namespace NoMercy.Plugins.Abstractions;

public interface IPlugin : IDisposable
{
    string Name { get; }
    string Description { get; }
    Guid Id { get; }
    Version Version { get; }
    void Initialize(IPluginContext context);
}
