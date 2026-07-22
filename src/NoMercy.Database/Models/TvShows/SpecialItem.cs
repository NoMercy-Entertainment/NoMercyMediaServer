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

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.TvShows;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(SpecialId), additionalPropertyNames: nameof(EpisodeId), IsUnique = true)]
[Index(propertyName: nameof(SpecialId), additionalPropertyNames: nameof(MovieId), IsUnique = true)]
public class SpecialItem
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "order")]
    public int Order { get; set; }

    [JsonProperty(propertyName: "special_id")]
    public Ulid SpecialId { get; set; }

    [JsonProperty(propertyName: "special")]
    public Special Special { get; set; } = null!;

    [JsonProperty(propertyName: "episode_id")]
    public int? EpisodeId { get; set; }
    public Episode? Episode { get; set; }

    [JsonProperty(propertyName: "movie_id")]
    public int? MovieId { get; set; }
    public Movie? Movie { get; set; }

    [JsonProperty(propertyName: "user_data")]
    public ICollection<UserData> UserData { get; set; } = [];
}
