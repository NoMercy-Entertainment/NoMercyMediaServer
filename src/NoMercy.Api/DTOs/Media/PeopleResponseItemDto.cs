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
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "adult")]
    public bool Adult { get; set; }

    [JsonProperty(propertyName: "also_known_as")]
    public string[]? AlsoKnownAs { get; set; }

    [JsonProperty(propertyName: "biography")]
    public string? Biography { get; set; }

    [JsonProperty(propertyName: "birthday")]
    public DateTime? Birthday { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "deathday")]
    public DateTime? DeathDay { get; set; }

    [JsonProperty(propertyName: "gender")]
    public string Gender { get; set; }

    [JsonProperty(propertyName: "homepage")]
    public string? Homepage { get; set; }

    [JsonProperty(propertyName: "imdb_id")]
    public string? ImdbId { get; set; }

    [JsonProperty(propertyName: "known_for_department")]
    public string? KnownForDepartment { get; set; }

    [JsonProperty(propertyName: "media_type")]
    public string MediaType { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; }

    [JsonProperty(propertyName: "place_of_birth")]
    public string? PlaceOfBirth { get; set; }

    [JsonProperty(propertyName: "popularity")]
    public double Popularity { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "profile")]
    public string? Profile { get; set; }

    [JsonProperty(propertyName: "created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty(propertyName: "updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonProperty(propertyName: "link")]
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
        Link = new(uriString: $"/person/{Id}", uriKind: UriKind.Relative);
    }
}
