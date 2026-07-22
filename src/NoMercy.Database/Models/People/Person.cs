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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.People;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(Name))]
[Index(propertyName: nameof(TitleSort))]
[Index(propertyName: nameof(ImdbId))]
[Index(propertyName: nameof(BirthDay))]
[Index(propertyName: nameof(Popularity))]
public class Person : ColorPaletteTimeStamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "adult")]
    public bool Adult { get; set; }

    [JsonProperty(propertyName: "also_known_as")]
    public string? AlsoKnownAs { get; set; }

    [MaxLength(length: 4096)]
    [JsonProperty(propertyName: "biography")]
    public string? Biography { get; set; }

    [JsonProperty(propertyName: "birthday")]
    public DateTime? BirthDay { get; set; }

    [JsonProperty(propertyName: "deathday")]
    public DateTime? DeathDay { get; set; }

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

    [JsonProperty(propertyName: "title_sort")]
    public string TitleSort { get; set; } = string.Empty;

    [JsonProperty(propertyName: "casts")]
    public ICollection<Cast> Casts { get; set; } = [];

    [JsonProperty(propertyName: "crews")]
    public ICollection<Crew> Crews { get; set; } = [];

    [JsonProperty(propertyName: "images")]
    public ICollection<Image> Images { get; set; } = [];

    [JsonProperty(propertyName: "translations")]
    public ICollection<Translation> Translations { get; set; } = [];

    [Column(name: "Gender")]
    [JsonProperty(propertyName: "gender")]
    [System.Text.Json.Serialization.JsonIgnore]
    public TmdbGender TmdbGender { get; set; }

    [NotMapped]
    [JsonProperty(propertyName: "Gender")]
    public string Gender
    {
        get => TmdbGender.ToString();
        set => TmdbGender = Enum.Parse<TmdbGender>(value: value);
    }

    [Column(name: "ExternalIds")]
    [JsonProperty(propertyName: "external_ids")]
    [System.Text.Json.Serialization.JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _externalIds { get; set; }

    [NotMapped]
    public TmdbPersonExternalIds? ExternalIds
    {
        get =>
            _externalIds is null
                ? null
                : JsonConvert.DeserializeObject<TmdbPersonExternalIds>(value: _externalIds);
        set => _externalIds = JsonConvert.SerializeObject(value: value);
    }
}
