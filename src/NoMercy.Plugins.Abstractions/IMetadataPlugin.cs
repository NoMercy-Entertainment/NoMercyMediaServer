namespace NoMercy.Plugins.Abstractions;

public interface IMetadataPlugin : IPlugin
{
    Task<MediaMetadata?> GetMetadataAsync(string title, MediaType type, CancellationToken ct = default);
}
