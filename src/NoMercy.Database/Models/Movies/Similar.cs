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
[Index(propertyName: nameof(MediaId), additionalPropertyNames: nameof(TvFromId), IsUnique = true)]
[Index(propertyName: nameof(MediaId), additionalPropertyNames: nameof(MovieFromId), IsUnique = true)]
[Index(propertyName: nameof(Title))]
[Index(propertyName: nameof(TitleSort))]
[Index(propertyName: nameof(TvFromId))]
[Index(propertyName: nameof(TvToId))]
[Index(propertyName: nameof(MovieFromId))]
[Index(propertyName: nameof(MovieToId))]
public class Similar : ColorPaletteTimeStamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Identity)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [MaxLength(length: 4096)]
    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty(propertyName: "media_id")]
    public int MediaId { get; set; }

    [JsonProperty(propertyName: "tv_from_id")]
    public int? TvFromId { get; set; }
    public Tv? TvFrom { get; set; }

    [JsonProperty(propertyName: "tv_to_id")]
    public int? TvToId { get; set; }
    public Tv? TvTo { get; set; }

    [JsonProperty(propertyName: "movie_from_id")]
    public int? MovieFromId { get; set; }
    public Movie? MovieFrom { get; set; }

    [JsonProperty(propertyName: "movie_to_id")]
    public int? MovieToId { get; set; }
    public Movie? MovieTo { get; set; }
}
