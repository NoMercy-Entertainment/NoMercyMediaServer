﻿using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class MovieChanges
{
    [JsonProperty("changes")] public List<Changes> ChangesChanges { get; set; } = new();
}

public class Changes
{
    [JsonProperty("key")] public string Key { get; set; } = string.Empty;

    [JsonProperty("items")] public List<Change> Items { get; set; } = new();
}

public class Change
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;

    [JsonProperty("action")] public string Action { get; set; } = string.Empty;

    [JsonProperty("time")] public string Time { get; set; } = string.Empty;

    [JsonProperty("iso_639_1")] public string Iso6391 { get; set; } = string.Empty;

    [JsonProperty("value")] public string Value { get; set; } = string.Empty;

    [JsonProperty("original_value")] public string OriginalValue { get; set; } = string.Empty;
}