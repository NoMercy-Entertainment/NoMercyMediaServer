using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record SpecialItemDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("watched")] public bool Watched { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("media_type")] public string MediaType { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("year")] public long Year { get; set; }
    [JsonProperty("genres")] public IEnumerable<GenreDto> Genres { get; set; }
    [JsonProperty("duration")] public int Duration { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }

    [JsonProperty("rating")] public Certification? Rating { get; set; }

    [JsonProperty("videoId")] public string? VideoId { get; set; }

    [JsonProperty("number_of_items")] public int? NumberOfItems { get; set; }
    [JsonProperty("have_items")] public int HaveItems { get; set; }

    public SpecialItemDto(SpecialItemsDto item)
    {
        Id = item.Id;
        Title = item.Title;
        Overview = item.Overview;
        Backdrop = item.Backdrop;
        Favorite = item.Favorite;
        Logo = item.Logo;
        Genres = item.Genres;
        MediaType = item.MediaType;
        ColorPalette = item.ColorPalette;
        Poster = item.Poster;
        Type = item.Type;
        Year = item.Year;
        Rating = item.Rating;
        NumberOfItems = item.NumberOfItems;
        HaveItems = item.HaveItems;
        VideoId = item.VideoId;
        Duration = item.Duration;
        Link = new($"/{item.MediaType}/{item.Id}", UriKind.Relative);
    }
}
