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
using NoMercy.Api.DTOs.Media;
using NoMercy.Database;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Models.Shared;
using TmdbGender = NoMercy.Providers.TMDB.Models.People.TmdbGender;

namespace NoMercy.Api.DTOs.Common;

public record PeopleDto
{
    [JsonProperty(propertyName: "character")]
    public string? Character { get; set; }

    [JsonProperty(propertyName: "adult")]
    public bool? Adult { get; set; }

    [JsonProperty(propertyName: "job")]
    public string? Job { get; set; }

    [JsonProperty(propertyName: "profile")]
    public string? ProfilePath { get; set; }

    [JsonProperty(propertyName: "gender")]
    public string Gender { get; set; }

    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "known_for_department")]
    public string? KnownForDepartment { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; }

    [JsonProperty(propertyName: "popularity")]
    public double Popularity { get; set; }

    [JsonProperty(propertyName: "deathday")]
    public DateTime? DeathDay { get; set; }

    [JsonProperty(propertyName: "translations")]
    public TranslationDto[] Translations { get; set; }

    [JsonProperty(propertyName: "order")]
    public int? Order { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    public PeopleDto()
    {
        Name = string.Empty;
        Adult = null;
        Gender = string.Empty;
        Translations = [];
        Link = new(uriString: "/person/0", uriKind: UriKind.Relative);
    }

    public PeopleDto(Cast cast)
    {
        Id = cast.Person.Id;
        Adult = cast.Person.Adult;
        Character = cast.Role.Character;
        ProfilePath = cast.Person.Profile;
        KnownForDepartment = cast.Person.KnownForDepartment;
        Name = cast.Person.Name;
        ColorPalette = cast.Person.ColorPalette;
        DeathDay = cast.Person.DeathDay;
        Gender = cast.Person.Gender;
        Order = cast.Role.Order;
        Link = new(uriString: $"/person/{cast.Person.Id}", uriKind: UriKind.Relative);
        Translations = [];
    }

    public PeopleDto(TmdbCast tmdbCast)
    {
        Id = tmdbCast.Id;
        Adult = tmdbCast.Adult;
        Character = tmdbCast.Character;
        ProfilePath = tmdbCast.ProfilePath;
        KnownForDepartment = tmdbCast.KnownForDepartment;
        Name = tmdbCast.Name.OrEmpty();
        ColorPalette = new();
        Gender = Enum.Parse<TmdbGender>(value: tmdbCast.Gender.ToString(), ignoreCase: true).ToString();
        Order = tmdbCast.Order;
        Link = new(uriString: $"/person/{tmdbCast.Id}", uriKind: UriKind.Relative);
        Translations = [];
    }

    public PeopleDto(Crew crew)
    {
        Id = crew.Person.Id;
        Adult = crew.Person.Adult;
        Job = crew.Job.Task;
        ProfilePath = crew.Person.Profile;
        KnownForDepartment = crew.Person.KnownForDepartment;
        Name = crew.Person.Name;
        ColorPalette = crew.Person.ColorPalette;
        DeathDay = crew.Person.DeathDay;
        Gender = crew.Person.Gender;
        Order = crew.Job.Order;
        Link = new(uriString: $"/person/{crew.Person.Id}", uriKind: UriKind.Relative);
        Translations = [];
    }

    public PeopleDto(TmdbCrew tmdbCrew)
    {
        Id = tmdbCrew.Id;
        Adult = tmdbCrew.Adult;
        Job = tmdbCrew.Job;
        ProfilePath = tmdbCrew.ProfilePath;
        KnownForDepartment = tmdbCrew.KnownForDepartment;
        Name = tmdbCrew.Name;
        ColorPalette = new();
        Gender = Enum.Parse<TmdbGender>(value: tmdbCrew.Gender.ToString(), ignoreCase: true).ToString();
        Order = tmdbCrew.Order;
        Link = new(uriString: $"/person/{tmdbCrew.Id}", uriKind: UriKind.Relative);
        Translations = [];
    }

    public PeopleDto(TmdbCreatedBy crew)
    {
        Id = crew.Id;
        Job = "Creator";
        ProfilePath = crew.ProfilePath;
        Name = crew.Name;
        ColorPalette = new();
        Gender = Enum.Parse<TmdbGender>(value: crew.Gender.ToString(), ignoreCase: true).ToString();
        Link = new(uriString: $"/person/{crew.Id}", uriKind: UriKind.Relative);
        Translations = [];
    }

    public PeopleDto(Creator creator)
    {
        Id = creator.Person.Id;
        Adult = creator.Person.Adult;
        Job = "Creator";
        ProfilePath = creator.Person.Profile;
        KnownForDepartment = creator.Person.KnownForDepartment;
        Name = creator.Person.Name;
        ColorPalette = creator.Person.ColorPalette;
        DeathDay = creator.Person.DeathDay;
        Gender = creator.Person.Gender;
        Link = new(uriString: $"/person/{creator.Person.Id}", uriKind: UriKind.Relative);
        Translations = [];
    }
}
