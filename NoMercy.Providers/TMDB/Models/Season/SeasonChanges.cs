﻿using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Season;

public class SeasonChanges
{
    [JsonProperty("changes")] public Change[] Changes { get; set; } = [];
}

public class Change
{
    [JsonProperty("key")] public string Key { get; set; } = string.Empty;
    [JsonProperty("items")] public Item[] Items { get; set; } = [];
}

public class Item
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("action")] public Action? Action { get; set; }
    [JsonProperty("time")] public string Time { get; set; } = string.Empty;
    [JsonProperty("value")] public string? Value { get; set; }
    [JsonProperty("original_value")] public string OriginalValue { get; set; } = string.Empty;
    [JsonProperty("iso_639_1")] public string Iso6391 { get; set; } = string.Empty;
}

public class ValueClass
{
    [JsonProperty("episode_id")] public int EpisodeId { get; set; }
    [JsonProperty("episode_number")] public int EpisodeNumber { get; set; }
}