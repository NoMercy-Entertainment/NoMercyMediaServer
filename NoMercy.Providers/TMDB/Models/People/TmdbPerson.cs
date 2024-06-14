﻿#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.People;

public enum TmdbGender
{
    Unknown,
    Female,
    Male,
    NonBinary
}

public class TmdbPerson
{
    [JsonProperty("birthday")] public DateTime? BirthDay { get; set; }
    [JsonProperty("known_for_department")] public string? KnownForDepartment { get; set; }
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("also_known_as")] public string[] AlsoKnownAs { get; set; }
    [JsonProperty("gender")] public TmdbGender TmdbGender { get; set; } = TmdbGender.Unknown;

    [JsonProperty("biography")] public string? Biography { get; set; }
    [JsonProperty("popularity")] public double Popularity { get; set; }
    [JsonProperty("place_of_birth")] public string? PlaceOfBirth { get; set; }
    [JsonProperty("profile_path")] public string? ProfilePath { get; set; }
    [JsonProperty("adult")] public bool Adult { get; set; }
    [JsonProperty("imdb_id")] public string? ImdbId { get; set; }
    [JsonProperty("external_ids")] public TmdbPersonExternalIds? ExternalIds { get; set; }
}