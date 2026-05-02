using Newtonsoft.Json;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Artwork;

public class TvdbArtworkResponse : TvdbResponse<TvdbArtwork> { }

public class TvdbArtworkExtendedResponse : TvdbResponse<TvdbArtworkExtended> { }

public class TvdbArtworkStatusesResponse : TvdbResponse<TvdbStatus[]> { }

public class TvdbArtworkTypesResponse : TvdbResponse<TvdbArtworkType[]> { }

public class TvdbArtwork
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("image")]
    public string Image { get; set; } = string.Empty;

    [JsonProperty("thumbnail")]
    public string Thumbnail { get; set; } = string.Empty;

    [JsonProperty("language")]
    public string? Language { get; set; }

    [JsonProperty("type")]
    public int Type { get; set; }

    [JsonProperty("score")]
    public double Score { get; set; }

    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }

    [JsonProperty("includesText")]
    public bool IncludesText { get; set; }
}

public class TvdbArtworkExtended : TvdbArtwork
{
    [JsonProperty("episodeId")]
    public long? EpisodeId { get; set; }

    [JsonProperty("movieId")]
    public long? MovieId { get; set; }

    [JsonProperty("networkId")]
    public long? NetworkId { get; set; }

    [JsonProperty("peopleId")]
    public long? PeopleId { get; set; }

    [JsonProperty("seasonId")]
    public long? SeasonId { get; set; }

    [JsonProperty("seriesId")]
    public long? SeriesId { get; set; }

    [JsonProperty("seriesPeopleId")]
    public long? SeriesPeopleId { get; set; }

    [JsonProperty("status")]
    public TvdbStatus? Status { get; set; }

    [JsonProperty("tagOptions")]
    public TvdbTagOption[] TagOptions { get; set; } = [];

    [JsonProperty("thumbnailHeight")]
    public int ThumbnailHeight { get; set; }

    [JsonProperty("thumbnailWidth")]
    public int ThumbnailWidth { get; set; }

    [JsonProperty("updatedAt")]
    public long UpdatedAt { get; set; }
}

public class TvdbArtworkType
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("recordType")]
    public string RecordType { get; set; } = string.Empty;

    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonProperty("imageFormat")]
    public string ImageFormat { get; set; } = string.Empty;

    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }

    [JsonProperty("thumbWidth")]
    public int ThumbWidth { get; set; }

    [JsonProperty("thumbHeight")]
    public int ThumbHeight { get; set; }
}
