using Newtonsoft.Json;
using NoMercy.Database.Models;
using NoMercy.Providers.TMDB.Models.Combined;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record TranslationDto
{
    public TranslationDto(Translation translation)
    {
        Iso31661 = translation.Iso31661;
        Iso6391 = translation.Iso6391 ?? string.Empty;
        EnglishName = translation.EnglishName ?? string.Empty;
        Name = translation.Name ?? string.Empty;
        Biography = translation.Biography ?? string.Empty;
    }

    public TranslationDto(TmdbCombinedTranslation translation)
    {
        Iso31661 = translation.Iso31661;
        Iso6391 = translation.Iso6391;
        EnglishName = translation.EnglishName;
        Name = translation.Data.Name ?? string.Empty;
        Biography = translation.Data.Biography ?? string.Empty;
    }

    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; } = string.Empty;
    [JsonProperty("iso_639_1")] public string Iso6391 { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("english_name")] public string EnglishName { get; set; } = string.Empty;
    [JsonProperty("biography")] public string Biography { get; set; } = string.Empty;
}