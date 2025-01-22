using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record DirectorDto
{
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }

    public DirectorDto(Crew crew)
    {
        Id = crew.Person.Id;
        Name = crew.Person.Name;
        ColorPalette = crew.Person.ColorPalette;
    }

    public DirectorDto(TmdbCrew tmdbCrew)
    {
        Id = tmdbCrew.Id;
        Name = tmdbCrew.Name;
        ColorPalette = new();
    }
}