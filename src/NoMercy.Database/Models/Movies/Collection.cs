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

namespace NoMercy.Database.Models.Movies;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(Title))]
[Index(propertyName: nameof(TitleSort))]
[Index(propertyName: nameof(LibraryId))]
public class Collection : ColorPaletteTimeStamps, IHasLibrary
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "title_sort")]
    public string? TitleSort { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [MaxLength(length: 4096)]
    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "parts")]
    public int Parts { get; set; }

    [JsonProperty(propertyName: "library_id")]
    public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    [JsonProperty(propertyName: "collection_movies")]
    public ICollection<CollectionMovie> CollectionMovies { get; set; } = [];

    [JsonProperty(propertyName: "translations")]
    public ICollection<Translation> Translations { get; set; } = [];

    [JsonProperty(propertyName: "images")]
    public ICollection<Image> Images { get; set; } = [];

    [JsonProperty(propertyName: "collection_user")]
    public ICollection<CollectionUser> CollectionUser { get; set; } = [];

    [JsonProperty(propertyName: "user_data")]
    public ICollection<UserData> UserData { get; set; } = [];

    // public Collection(TmdbCollectionAppends tmdbCollection, Ulid libraryId)
    // {
    //     Id = tmdbCollection.Id;
    //     Title = tmdbCollection.Name;
    //     TitleSort = tmdbCollection.Name.TitleSort(tmdbCollection.Parts.MinBy(movie => movie.ReleaseDate)?.ReleaseDate);
    //     Backdrop = tmdbCollection.BackdropPath;
    //     Poster = tmdbCollection.PosterPath;
    //     Overview = tmdbCollection.Overview;
    //     Parts = tmdbCollection.Parts.Length;
    //     LibraryId = libraryId;
    // }
}
