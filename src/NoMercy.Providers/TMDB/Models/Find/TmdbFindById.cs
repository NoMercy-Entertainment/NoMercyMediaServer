using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.People;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Providers.TMDB.Models.Find;

public class TmdbFindById
{
    [JsonProperty("movie_results")] public TmdbMovie[] MovieResults { get; set; } = [];
    [JsonProperty("person_results")] public TmdbPerson[] PersonResults { get; set; } = [];
    [JsonProperty("tv_results")] public TmdbTvShow[] TvResults { get; set; } = [];
    [JsonProperty("tv_episode_results")] public Episode.TmdbEpisode[] TvEpisodeResults { get; set; } = [];
    [JsonProperty("tv_season_results")] public Season.TmdbSeason[] TvSeasonResults { get; set; } = [];
}