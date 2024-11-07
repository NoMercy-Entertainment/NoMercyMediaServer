﻿using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvEpisodeGroups
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("results")] public TmdbEpisodeGroupsResult[] Results { get; set; } = [];
}
