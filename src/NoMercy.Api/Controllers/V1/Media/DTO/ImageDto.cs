using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record ImageDto
{
    [JsonProperty("height")] public long Height { get; set; }
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("src")] public string? Src { get; set; }
    [JsonProperty("type")] public string? Type { get; set; }
    [JsonProperty("width")] public long Width { get; set; }
    [JsonProperty("iso_639_1")] public string? Iso6391 { get; set; }
    [JsonProperty("voteAverage")] public double VoteAverage { get; set; }
    [JsonProperty("voteCount")] public long VoteCount { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }

    public ImageDto(Image media)
    {
        Id = media.Id;
        Src = media.FilePath;
        Width = media.Width ?? 0;
        Type = media.Type;
        Height = media.Height ?? 0;
        Iso6391 = media.Iso6391;
        VoteAverage = media.VoteAverage ?? 0;
        VoteCount = media.VoteCount ?? 0;
        ColorPalette = media.ColorPalette;
    }

    public ImageDto(TmdbImage media)
    {
        Src = media.FilePath;
        Width = media.Width;
        Height = media.Height;
        Iso6391 = media.Iso6391;
        VoteAverage = media.VoteAverage;
        VoteCount = media.VoteCount;
        ColorPalette = new();
    }

    public ImageDto(TmdbProfile image)
    {
        Src = image.FilePath;
        Width = image.Width;
        Height = image.Height;
        Iso6391 = image.Iso6391;
        VoteAverage = 0;
        VoteCount = 0;
        ColorPalette = new();
    }
}