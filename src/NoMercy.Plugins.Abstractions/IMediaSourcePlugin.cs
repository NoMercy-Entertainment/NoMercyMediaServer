namespace NoMercy.Plugins.Abstractions;

public interface IMediaSourcePlugin : IPlugin
{
    Task<IEnumerable<MediaFile>> ScanAsync(string path, CancellationToken ct = default);
}
