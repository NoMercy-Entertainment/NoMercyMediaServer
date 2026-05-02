using NoMercy.OpticalMedia.Sources;

namespace NoMercy.OpticalMedia.Metadata;

public interface IDiscMetadataResolver
{
    Task<MetadataMatch[]> ResolveAsync(DiscInfo disc, CancellationToken ct);

    Task<MetadataMatch[]> SearchAsync(string query, MediaType type, CancellationToken ct);
}
