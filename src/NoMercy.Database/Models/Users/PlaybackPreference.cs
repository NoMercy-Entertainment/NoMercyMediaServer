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

[PrimaryKey(nameof(Id))]
[Index(nameof(UserId), nameof(LibraryId), IsUnique = true)]
[Index(nameof(UserId), nameof(TvId), IsUnique = true)]
[Index(nameof(UserId), nameof(MovieId), IsUnique = true)]
public class PlaybackPreference : MetadataTrack
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty("user_id")]
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    [JsonProperty("library_id")]
    public Ulid? LibraryId { get; set; }
    public Library? Library { get; set; }

    [JsonProperty("tv_id")]
    public int? TvId { get; set; }
    public Tv? Tv { get; set; }

    [JsonProperty("movie_id")]
    public int? MovieId { get; set; }
    public Movie? Movie { get; set; }

    [JsonProperty("collection_id")]
    public int? CollectionId { get; set; }
    public Collection? Collection { get; set; }

    [JsonProperty("special_id")]
    public Ulid? SpecialId { get; set; }
    public Special? Special { get; set; }
}
