using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Episode;

public class TmdbEpisodeDetails : TmdbEpisode
{
    [JsonProperty("crew")] public TmdbCrew[] Crew { get; set; } = [];
    [JsonProperty("cast")] public TmdbCast[] Cast { get; set; } = [];
    [JsonProperty("guest_stars")] public TmdbGuestStar[] GuestStars { get; set; } = [];
}