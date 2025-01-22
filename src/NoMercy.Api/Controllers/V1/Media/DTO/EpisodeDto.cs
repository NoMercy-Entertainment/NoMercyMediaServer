using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Episode;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record EpisodeDto
{
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("episode_number")] public long EpisodeNumber { get; set; }
    [JsonProperty("season_number")] public long SeasonNumber { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("airDate")] public DateTime? AirDate { get; set; }
    [JsonProperty("still")] public string? Still { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("progress")] public object? Progress { get; set; }
    [JsonProperty("available")] public bool Available { get; set; }
    [JsonProperty("translations")] public IEnumerable<TranslationDto> Translations { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }

    public EpisodeDto(Episode episode)
    {
        string? title = episode.Translations.FirstOrDefault()?.Title;
        string? overview = episode.Translations.FirstOrDefault()?.Overview;

        VideoFile? videoFile = episode.VideoFiles.FirstOrDefault();
        UserData? userData = videoFile?.UserData.FirstOrDefault();

        Id = episode.Id;
        Title = !string.IsNullOrEmpty(title)
            ? title
            : episode.Title;
        Overview = !string.IsNullOrEmpty(overview)
            ? overview
            : episode.Overview;
        EpisodeNumber = episode.EpisodeNumber;
        SeasonNumber = episode.SeasonNumber;
        Link = new($"/tv/{episode.TvId}/watch?season={episode.SeasonNumber}&episode={episode.EpisodeNumber}", UriKind.Relative);
        AirDate = episode.AirDate;
        Still = episode.Still;
        ColorPalette = episode.ColorPalette;
        Available = episode.VideoFiles.Count != 0;
        Translations = episode.Translations
            .Select(translation => new TranslationDto(translation));

        Progress = userData?.UpdatedAt is not null && videoFile?.Duration is not null
            ? (int)Math.Round((double)(100 * (userData.Time ?? 0)) / (videoFile.Duration?.ToSeconds() ?? 0))
            : null;
    }

    public EpisodeDto(int tvId, int seasonNumber, int episodeNumber, string country)
    {
        TmdbEpisodeClient tmdbEpisodeClient = new(tvId, seasonNumber, episodeNumber);
        TmdbEpisodeAppends? episodeData = tmdbEpisodeClient.WithAllAppends().Result;

        if (episodeData is null) return;

        string? title = episodeData.Translations.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Data.Title;

        string? overview = episodeData.Translations.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Data.Overview;

        Id = episodeData.Id;
        Title = !string.IsNullOrEmpty(title)
            ? title
            : episodeData.Name;
        Overview = !string.IsNullOrEmpty(overview)
            ? overview
            : episodeData.Overview;
        EpisodeNumber = episodeData.EpisodeNumber;
        SeasonNumber = episodeData.SeasonNumber;
        AirDate = episodeData.AirDate;
        Still = episodeData.StillPath;
        ColorPalette = new();
        Available = false;
        Link = new($"/tv/{tvId}/watch?season={seasonNumber}&episode={episodeNumber}", UriKind.Relative);

        Translations = episodeData.Translations.Translations
            .Select(translation => new TranslationDto(translation));
    }
}
