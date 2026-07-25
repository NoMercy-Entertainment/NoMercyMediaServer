// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.People;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.NewtonSoftConverters;

namespace NoMercy.Api.DTOs.Media;

public record PeopleResponseItemDto
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("adult")]
    public bool Adult { get; set; }

    [JsonProperty("also_known_as")]
    public string[]? AlsoKnownAs { get; set; }

    [JsonProperty("biography")]
    public string? Biography { get; set; }

    [JsonProperty("birthday")]
    public DateTime? Birthday { get; set; }

    [JsonProperty("color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty("deathday")]
    public DateTime? DeathDay { get; set; }

    [JsonProperty("gender")]
    public string Gender { get; set; }

    [JsonProperty("homepage")]
    public string? Homepage { get; set; }

    [JsonProperty("imdb_id")]
    public string? ImdbId { get; set; }

    [JsonProperty("known_for_department")]
    public string? KnownForDepartment { get; set; }

    [JsonProperty("media_type")]
    public string MediaType { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("place_of_birth")]
    public string? PlaceOfBirth { get; set; }

    [JsonProperty("popularity")]
    public double Popularity { get; set; }

    [JsonProperty("poster")]
    public string? Poster { get; set; }

    [JsonProperty("profile")]
    public string? Profile { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonProperty("link")]
    public Uri Link { get; set; }

    public PeopleResponseItemDto(Person person)
    {
        string biography = (
            person.Translations.FirstOrDefault()?.Biography ?? person.Biography
        ).OrEmpty();

        Id = person.Id;
        Name = person.Name;
        Biography = biography;
        Adult = person.Adult;
        AlsoKnownAs = person.AlsoKnownAs.FromJson<string[]>() ?? [];
        Birthday = person.BirthDay;
        DeathDay = person.DeathDay;
        Gender = person.Gender;
        Homepage = person.Homepage;
        ImdbId = person.ImdbId;
        KnownForDepartment = person.KnownForDepartment;
        PlaceOfBirth = person.PlaceOfBirth;
        Popularity = person.Popularity;
        Profile = person.Profile;
        Poster = person.Profile;
        ColorPalette = person.ColorPalette;
        CreatedAt = person.CreatedAt;
        MediaType = "person";
        Type = "person";
        Link = new($"/person/{Id}", UriKind.Relative);
    }
}
