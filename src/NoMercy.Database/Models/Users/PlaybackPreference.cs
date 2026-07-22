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

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Users;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(UserId), additionalPropertyNames: nameof(LibraryId), IsUnique = true)]
[Index(propertyName: nameof(UserId), additionalPropertyNames: nameof(TvId), IsUnique = true)]
[Index(propertyName: nameof(UserId), additionalPropertyNames: nameof(MovieId), IsUnique = true)]
public class PlaybackPreference : MetadataTrack
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty(propertyName: "user_id")]
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    [JsonProperty(propertyName: "library_id")]
    public Ulid? LibraryId { get; set; }
    public Library? Library { get; set; }

    [JsonProperty(propertyName: "tv_id")]
    public int? TvId { get; set; }
    public Tv? Tv { get; set; }

    [JsonProperty(propertyName: "movie_id")]
    public int? MovieId { get; set; }
    public Movie? Movie { get; set; }

    [JsonProperty(propertyName: "collection_id")]
    public int? CollectionId { get; set; }
    public Collection? Collection { get; set; }

    [JsonProperty(propertyName: "special_id")]
    public Ulid? SpecialId { get; set; }
    public Special? Special { get; set; }
}
