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

[PrimaryKey(nameof(Id))]
[Index(nameof(MediaId), nameof(TvFromId), IsUnique = true)]
[Index(nameof(MediaId), nameof(MovieFromId), IsUnique = true)]
[Index(nameof(Title))]
[Index(nameof(TitleSort))]
[Index(nameof(TvFromId))]
[Index(nameof(TvToId))]
[Index(nameof(MovieFromId))]
[Index(nameof(MovieToId))]
public class Similar : ColorPaletteTimeStamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("backdrop")]
    public string? Backdrop { get; set; }

    [MaxLength(4096)]
    [JsonProperty("overview")]
    public string? Overview { get; set; }

    [JsonProperty("poster")]
    public string? Poster { get; set; }

    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty("media_id")]
    public int MediaId { get; set; }

    [JsonProperty("tv_from_id")]
    public int? TvFromId { get; set; }
    public Tv? TvFrom { get; set; }

    [JsonProperty("tv_to_id")]
    public int? TvToId { get; set; }
    public Tv? TvTo { get; set; }

    [JsonProperty("movie_from_id")]
    public int? MovieFromId { get; set; }
    public Movie? MovieFrom { get; set; }

    [JsonProperty("movie_to_id")]
    public int? MovieToId { get; set; }
    public Movie? MovieTo { get; set; }
}
