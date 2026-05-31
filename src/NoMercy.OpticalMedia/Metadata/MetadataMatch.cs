namespace NoMercy.OpticalMedia.Metadata;

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
    MediaType Type,
    /// <summary>Runtime in minutes as reported by TMDB.</summary>
    int? Runtime = null,
    string? BackdropUrl = null,
    /// <summary>
    /// When <see cref="Type"/> is <see cref="MediaType.TvShow"/> and the
    /// duration pass identified a specific season, this is the season number.
    /// Null for movie matches or when only the show root was identified.
    /// </summary>
    int? SeasonNumber = null,
    /// <summary>Episode number within the season.</summary>
    int? EpisodeNumber = null
);
