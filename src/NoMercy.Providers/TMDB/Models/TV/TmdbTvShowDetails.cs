using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Networks;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvShowDetails : TmdbTvShow
{
    [JsonProperty("adult")] public bool Adult { get; set; }
    [JsonProperty("created_by")] public TmdbCreatedBy[] CreatedBy { get; set; } = [];
    [JsonProperty("episode_run_time")] public int[]? EpisodeRunTime { get; set; } = [];
    [JsonProperty("genres")] public TmdbGenre[] Genres { get; set; } = [];
    [JsonProperty("homepage")] public Uri? Homepage { get; set; }
    [JsonProperty("in_production")] public bool InProduction { get; set; }
    [JsonProperty("languages")] public string[] Languages { get; set; } = [];
    [JsonProperty("last_episode_to_air")] public TmdbEpisode? LastEpisodeToAir { get; set; }
    [JsonProperty("next_episode_to_air")] public TmdbEpisode? NextEpisodeToAir { get; set; }
    [JsonProperty("networks")] public TmdbNetwork[] Networks { get; set; } = [];
    [JsonProperty("number_of_episodes")] public int NumberOfEpisodes { get; set; }
    [JsonProperty("number_of_seasons")] public int NumberOfSeasons { get; set; }
    [JsonProperty("production_companies")] public TmdbProductionCompany[] ProductionCompanies { get; set; } = [];
    [JsonProperty("production_countries")] public TmdbProductionCountry[] ProductionCountries { get; set; } = [];
    [JsonProperty("seasons")] public List<TmdbSeason> Seasons { get; set; } = [];
    [JsonProperty("spoken_languages")] public TmdbSpokenLanguage[] SpokenLanguages { get; set; } = [];
    [JsonProperty("status")] public string? Status { get; set; }
    [JsonProperty("tagline")] public string? Tagline { get; set; }
    [JsonProperty("type")] public string? Type { get; set; }
}