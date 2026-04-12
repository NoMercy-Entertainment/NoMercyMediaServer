namespace NoMercy.Encoder.V3.DiscRipping;

public interface IDiscMetadataResolver
{
    Task<MetadataMatch[]> ResolveAsync(DiscInfo disc, CancellationToken ct);

    Task<MetadataMatch[]> SearchAsync(string query, MediaType type, CancellationToken ct);
}
