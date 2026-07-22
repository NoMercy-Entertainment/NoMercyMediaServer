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
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.Providers.TMDB.Models.People;
using TmdbGender = NoMercy.Providers.TMDB.Models.People.TmdbGender;
using TmdbPersonExternalIds = NoMercy.Database.Models.People.TmdbPersonExternalIds;

namespace NoMercy.Api.DTOs.Media;

public record PersonResponseItemDto
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

    [JsonProperty(propertyName: "deathday")]
    public DateTime? DeathDay { get; set; }

    [JsonProperty(propertyName: "gender")]
    public string Gender { get; set; } = nameof(TmdbGender.Unknown);

    [JsonProperty(propertyName: "homepage")]
    public string? Homepage { get; set; }

    [JsonProperty(propertyName: "imdb_id")]
    public string? ImdbId { get; set; }

    [JsonProperty(propertyName: "known_for_department")]
    public string? KnownForDepartment { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "place_of_birth")]
    public string? PlaceOfBirth { get; set; }

    [JsonProperty(propertyName: "popularity")]
    public double Popularity { get; set; }

    [JsonProperty(propertyName: "profile")]
    public string? Profile { get; set; }

    [JsonProperty(propertyName: "titleSort")]
    public string TitleSort { get; set; } = string.Empty;

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty(propertyName: "updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    [JsonProperty(propertyName: "combined_credits")]
    public Credits CombinedCredits { get; set; } = new();

    [JsonProperty(propertyName: "external_ids")]
    public TmdbPersonExternalIds? ExternalIds { get; set; }

    [JsonProperty(propertyName: "translations")]
    public TranslationsDto TranslationsDto { get; set; } = new();

    [JsonProperty(propertyName: "known_for")]
    public KnownForDto[] KnownFor { get; set; } = [];

    [JsonProperty(propertyName: "images")]
    public ImagesDto ImagesDto { get; set; } = new();

    public PersonResponseItemDto(Person person)
    {
        string? biography = person.Translations.FirstOrDefault()?.Biography;

        Id = person.Id;
        Name = person.Name;
        Biography = !string.IsNullOrEmpty(value: biography) ? biography : person.Biography;
        Adult = person.Adult;
        AlsoKnownAs = person.AlsoKnownAs.FromJson<string[]>() ?? [];
        Birthday = person.BirthDay;
        DeathDay = person.DeathDay;
        Homepage = person.Homepage;
        ImdbId = person.ImdbId;
        KnownForDepartment = person.KnownForDepartment;
        PlaceOfBirth = person.PlaceOfBirth;
        Popularity = person.Popularity;
        Profile = person.Profile;
        ColorPalette = person.ColorPalette;
        CreatedAt = person.CreatedAt;
        ExternalIds = person.ExternalIds;
        Gender = person.Gender;
        Link = new(uriString: $"/person/{Id}", uriKind: UriKind.Relative);

        ImagesDto = new()
        {
            Profiles = person.Images.Select(selector: image => new ImageDto(media: image)).ToArray(),
        };

        CombinedCredits = new()
        {
            Cast = person
                .Casts.Select(selector: cast => new KnownForDto(cast: cast))
                .OrderByDescending(keySelector: knownFor => knownFor.Year)
                .ToArray(),

            Crew = person
                .Crews.Select(selector: crew => new KnownForDto(crew: crew))
                .OrderByDescending(keySelector: knownFor => knownFor.Year)
                .ToArray(),
        };

        KnownFor = person
            .Casts.Select(selector: crew => new KnownForDto(cast: crew))
            .Concat(second: person.Crews.Select(selector: crew => new KnownForDto(crew: crew)))
            .OrderByDescending(keySelector: knownFor => knownFor.Popularity)
            .ToArray();
    }

    public PersonResponseItemDto(
        TmdbPersonAppends tmdbPersonAppends,
        string? country,
        Person? person
    )
    {
        string? biography = tmdbPersonAppends
            .Translations.Translations.FirstOrDefault(predicate: translation =>
                translation.Iso31661 == country
            )
            ?.TmdbPersonTranslationData.Overview;

        Id = tmdbPersonAppends.Id;
        Name = tmdbPersonAppends.Name;
        Biography = !string.IsNullOrEmpty(value: biography) ? biography : tmdbPersonAppends.Biography;
        Adult = tmdbPersonAppends.Adult;
        AlsoKnownAs = tmdbPersonAppends.AlsoKnownAs;
        Birthday = tmdbPersonAppends.BirthDay;
        DeathDay = tmdbPersonAppends.DeathDay;
        Homepage = tmdbPersonAppends.Homepage?.ToString();
        ImdbId = tmdbPersonAppends.ImdbId;
        KnownForDepartment = tmdbPersonAppends.KnownForDepartment;
        PlaceOfBirth = tmdbPersonAppends.PlaceOfBirth;
        Popularity = tmdbPersonAppends.Popularity;
        Profile = tmdbPersonAppends.ProfilePath;
        ColorPalette = new();
        ExternalIds = tmdbPersonAppends.ExternalIds;
        Gender = Enum.Parse<TmdbGender>(value: tmdbPersonAppends.TmdbGender.ToString(), ignoreCase: true).ToString();
        Link = new(uriString: $"/person/{Id}", uriKind: UriKind.Relative);

        ImagesDto = new()
        {
            Profiles = tmdbPersonAppends
                .Images.Profiles.Select(selector: image => new ImageDto(image: image))
                .ToArray(),
        };

        CombinedCredits = new()
        {
            Cast = tmdbPersonAppends
                .CombinedCredits.Cast.Select(selector: cast => new KnownForDto(crew: cast, person: person))
                .Where(predicate: knownFor =>
                    RuntimeServerSettings.Current.ShowAdultContent || !knownFor.Adult
                )
                .OrderByDescending(keySelector: knownFor => knownFor.Year)
                .ToArray(),

            Crew = tmdbPersonAppends
                .CombinedCredits.Crew.Select(selector: crew => new KnownForDto(crew: crew, person: person))
                .Where(predicate: knownFor =>
                    RuntimeServerSettings.Current.ShowAdultContent || !knownFor.Adult
                )
                .OrderByDescending(keySelector: knownFor => knownFor.Year)
                .ToArray(),
        };

        KnownForDto[] cast = tmdbPersonAppends
            .CombinedCredits.Cast.Select(selector: cast => new KnownForDto(crew: cast, person: person))
            .Where(predicate: knownFor => RuntimeServerSettings.Current.ShowAdultContent || !knownFor.Adult)
            .DistinctBy(keySelector: knownFor => knownFor.Id)
            .ToArray();

        KnownForDto[] crew = tmdbPersonAppends
            .CombinedCredits.Crew.Select(selector: crew => new KnownForDto(crew: crew, person: person))
            .Where(predicate: knownFor => RuntimeServerSettings.Current.ShowAdultContent || !knownFor.Adult)
            .DistinctBy(keySelector: knownFor => knownFor.Id)
            .ToArray();

        KnownFor = cast.Concat(second: crew).OrderByDescending(keySelector: knownFor => knownFor.VoteCount).ToArray();
    }
}
