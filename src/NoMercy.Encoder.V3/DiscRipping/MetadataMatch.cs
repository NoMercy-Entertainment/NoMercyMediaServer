namespace NoMercy.Encoder.V3.DiscRipping;

public enum MediaType
{
    Movie,
    TvShow,
    Music,
}

public record MetadataMatch(
    string Source,
    double Confidence,
    string Title,
    int? Year,
    string? PosterUrl,
    string? ExternalId,
    MediaType Type
);
