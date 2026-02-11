using Newtonsoft.Json;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Providers.TMDB.Models.Combined;

namespace NoMercy.Api.DTOs.Media;

public record TranslationDto
{
    public TranslationDto(Translation translation)
    {
        Iso31661 = translation.Iso31661 ?? string.Empty;
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