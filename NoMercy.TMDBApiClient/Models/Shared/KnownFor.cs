﻿using Newtonsoft.Json;

namespace NoMercy.TMDBApi.Models.Shared;

public class KnownFor
{
    [JsonProperty("poster_path")] public string PosterPath { get; set; }

    [JsonProperty("adult")] public bool? Adult { get; set; }

    [JsonProperty("overview")] public string Overview { get; set; }

    [JsonProperty("release_date")] public DateTime? ReleaseDate { get; set; }

    [JsonProperty("original_title")] public string OriginalTitle { get; set; }

    [JsonProperty("genre_ids")] public int[] GenreIds { get; set; } = { };

    [JsonProperty("id")] public int Id { get; set; }

    [JsonProperty("media_type")] public string MediaType { get; set; }

    [JsonProperty("original_language")] public string OriginalLanguage { get; set; }

    [JsonProperty("title")] public string Title { get; set; }

    [JsonProperty("backdrop_path")] public string BackdropPath { get; set; }

    [JsonProperty("popularity")] public double Popularity { get; set; }

    [JsonProperty("vote_count")] public int VoteCount { get; set; }

    [JsonProperty("video")] public bool? Video { get; set; }

    [JsonProperty("vote_average")] public float VoteAverage { get; set; }

    [JsonProperty("first_air_date")] public DateTime? FirstAirDate { get; set; }

    [JsonProperty("origin_country")] public string[] OriginCountry { get; set; } = { };

    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("original_name")] public string OriginalName { get; set; }
}

public interface IKnownForMovie
{
    [JsonProperty("release_date")] public DateTime? ReleaseDate { get; set; }
}

public interface IKnownForTv
{
    [JsonProperty("first_air_date")] public DateTime? FirstAirDate { get; set; }
}