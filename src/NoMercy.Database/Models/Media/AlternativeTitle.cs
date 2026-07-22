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

namespace NoMercy.Database.Models.Media;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(Title), additionalPropertyNames: nameof(TvId), IsUnique = true)]
[Index(propertyName: nameof(Title), additionalPropertyNames: nameof(MovieId), IsUnique = true)]
public class AlternativeTitle
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Identity)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "iso_3166_1")]
    public string? Iso31661 { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "movie_id")]
    public int? MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    [JsonProperty(propertyName: "tv_id")]
    public int? TvId { get; set; }
    public Tv Tv { get; set; } = null!;
}
