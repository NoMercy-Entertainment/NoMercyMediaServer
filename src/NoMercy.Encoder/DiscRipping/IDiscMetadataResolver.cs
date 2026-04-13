namespace NoMercy.Encoder.DiscRipping;

public interface IDiscMetadataResolver
{
    Task<MetadataMatch[]> ResolveAsync(DiscInfo disc, CancellationToken ct);

    Task<MetadataMatch[]> SearchAsync(string query, MediaType type, CancellationToken ct);
}
