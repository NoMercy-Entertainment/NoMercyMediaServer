using Newtonsoft.Json;
using NoMercy.Database.Models.Media;
using NoMercy.Providers.TMDB.Models.Combined;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media;

public record TranslationDto
{
    public TranslationDto(Translation translation)
    {
        Iso31661 = translation.Iso31661.OrEmpty();
        Iso6391 = translation.Iso6391.OrEmpty();
        EnglishName = translation.EnglishName.OrEmpty();
        Name = translation.Name.OrEmpty();
        Biography = translation.Biography.OrEmpty();
    }

    public TranslationDto(TmdbCombinedTranslation translation)
    {
        Iso31661 = translation.Iso31661;
        Iso6391 = translation.Iso6391;
        EnglishName = translation.EnglishName;
        Name = translation.Data.Name.OrEmpty();
        Biography = translation.Data.Biography.OrEmpty();
    }

    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; } = string.Empty;
    [JsonProperty("iso_639_1")] public string Iso6391 { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("english_name")] public string EnglishName { get; set; } = string.Empty;
    [JsonProperty("biography")] public string Biography { get; set; } = string.Empty;
}