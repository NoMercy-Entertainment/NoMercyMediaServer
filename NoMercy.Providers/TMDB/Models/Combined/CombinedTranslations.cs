﻿using Newtonsoft.Json;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace NoMercy.Providers.TMDB.Models.Combined;

public class CombinedTranslations
{
    [JsonProperty("id")] public int Id { get; set; }

    [JsonProperty("translations")] public CombinedTranslation[] Translations { get; set; } = [];
}

public class CombinedTranslation
{
    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; }

    [JsonProperty("iso_639_1")] public string Iso6391 { get; set; }

    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("english_name")] public string EnglishName { get; set; }

    [JsonProperty("data")] public CombinedTranslationData Data { get; set; }
}

public class CombinedTranslationData
{
    [JsonProperty("name")] public string? Name { get; set; }

    [JsonProperty("title")] public string? Title { get; set; }

    [JsonProperty("overview")] public string? Overview { get; set; }

    [JsonProperty("homepage")] public Uri? Homepage { get; set; }

    [JsonProperty("biography")] public string? Biography { get; set; }

    [JsonProperty("tagline")] public string? Tagline { get; set; }
}