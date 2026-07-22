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
public class Recommendation : ColorPaletteTimeStamps
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

    [JsonProperty(propertyName: "mediaId")]
    public int MediaId { get; set; }

    [ForeignKey(name: "TvFromId")]
    public int? TvFromId { get; set; }

    [JsonIgnore]
    public Tv? TvFrom { get; set; }

    [ForeignKey(name: "TvToId")]
    public int? TvToId { get; set; }

    [JsonIgnore]
    public Tv? TvTo { get; set; }

    [ForeignKey(name: "RecommendationFrom")]
    public int? MovieFromId { get; set; }

    [JsonIgnore]
    public Movie? MovieFrom { get; set; }

    [ForeignKey(name: "RecommendationTo")]
    public int? MovieToId { get; set; }

    [JsonIgnore]
    public Movie? MovieTo { get; set; }
}
