using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Season;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record SeasonDto
{
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("season_number")] public long SeasonNumber { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("episodes")] public IEnumerable<EpisodeDto> Episodes { get; set; }
    [JsonProperty("translations")] public IEnumerable<TranslationDto> Translations { get; set; }

    public SeasonDto(Season season)
    {
        string? title = season.Translations.FirstOrDefault()?.Title;
        string? overview = season.Translations.FirstOrDefault()?.Overview;

        Id = season.Id;
        Title = !string.IsNullOrEmpty(title)
            ? title
            : season.Title;
        Overview = !string.IsNullOrEmpty(overview)
            ? overview
            : season.Overview;
        Poster = season.Poster;
        SeasonNumber = season.SeasonNumber;
        ColorPalette = season.ColorPalette;
        Translations = season.Translations
            .Select(translation => new TranslationDto(translation));
        Episodes = season.Episodes
            .OrderBy(episode => episode.EpisodeNumber)
            .Select(episode => new EpisodeDto(episode));
    }

    public SeasonDto(int tvId, TmdbSeason tmdbSeason, string country)
    {
        TmdbSeasonClient tmdbSeasonClient = new(tvId, tmdbSeason.SeasonNumber);
        TmdbSeasonAppends? seasonData = tmdbSeasonClient.WithAllAppends().Result;

        string? title = seasonData?.Translations.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Data.Title;

        string? overview = seasonData?.Translations.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Data.Overview;

        Id = tmdbSeason.Id;
        Title = !string.IsNullOrEmpty(title)
            ? title
            : tmdbSeason.Name;
        Overview = !string.IsNullOrEmpty(overview)
            ? overview
            : tmdbSeason.Overview;
        Poster = tmdbSeason.PosterPath;
        SeasonNumber = tmdbSeason.SeasonNumber;
        ColorPalette = new();
        Translations = seasonData?.Translations.Translations
            .Select(translation => new TranslationDto(translation)) ?? [];
        Episodes = seasonData?.Episodes
            .OrderBy(episode => episode.EpisodeNumber)
            .Select(episode => new EpisodeDto(tvId, tmdbSeason.SeasonNumber, episode.EpisodeNumber, country)) ?? [];
    }
}