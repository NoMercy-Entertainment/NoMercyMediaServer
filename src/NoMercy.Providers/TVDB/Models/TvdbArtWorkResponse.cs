using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models;

public class TvdbArtWorkResponse : TvdbResponse<TvdbArtWork>
{
}
public class TvdbArtWork
{
    [JsonProperty("height")] public int Height { get; set; }
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("image")] public string Image { get; set; } = string.Empty;
    [JsonProperty("includesText")] public bool IncludesText { get; set; }
    [JsonProperty("language")] public string Language { get; set; } = string.Empty;
    [JsonProperty("score")] public int Score { get; set; }
    [JsonProperty("thumbnail")] public string Thumbnail { get; set; } = string.Empty;
    [JsonProperty("type")] public int Type { get; set; }
    [JsonProperty("width")] public int Width { get; set; }
}

public class TvdbArtWorkExtendedResponse : TvdbResponse<TvdbTvdbArtWorkExtended>
{
}
public class TvdbTvdbArtWorkExtended: TvdbArtWork
{
    [JsonProperty("episodeId")] public int? EpisodeId { get; set; }
    [JsonProperty("movieId")] public int? MovieId { get; set; }
    [JsonProperty("networkId")] public int? NetworkId { get; set; }
    [JsonProperty("peopleId")] public int? PeopleId { get; set; }
    [JsonProperty("seasonId")] public int? SeasonId { get; set; }
    [JsonProperty("seriesId")] public int? SeriesId { get; set; }
    [JsonProperty("seriesPeopleId")] public int? SeriesPeopleId { get; set; }
    [JsonProperty("status")] public TvdbStatus TvdbStatus { get; set; } = new();
    [JsonProperty("tagOptions")] public TvdbTagOption[] TvdbTagOptions { get; set; } = [];
    [JsonProperty("thumbnailHeight")] public int ThumbnailHeight { get; set; }
    [JsonProperty("thumbnailWidth")] public int ThumbnailWidth { get; set; }
    [JsonProperty("updatedAt")] public int UpdatedAt { get; set; }
}

public class TvdbArtWorkTypesResponse : TvdbResponse<TvdbTypes[]>
{
}
public class TvdbTypes
{
    [JsonProperty("height")] public int Height { get; set; }
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("imageFormat")] public string ImageFormat { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("recordType")] public string RecordType { get; set; } = string.Empty;
    [JsonProperty("slug")] public string Slug { get; set; } = string.Empty;
    [JsonProperty("thumbHeight")] public int ThumbHeight { get; set; }
    [JsonProperty("thumbWidth")] public int ThumbWidth { get; set; }
    [JsonProperty("width")] public int Width { get; set; }
}


