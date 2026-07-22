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

namespace NoMercy.Database.Models.TvShows;

[PrimaryKey(propertyName: nameof(Id))]
public class Special : ColorPaletteTimeStamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "logo")]
    public string? Logo { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty(propertyName: "creator")]
    public string? Creator { get; set; }

    [MaxLength(length: 4096)]
    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "items")]
    public ICollection<SpecialItem> Items { get; set; } = [];

    [JsonProperty(propertyName: "special_user")]
    public ICollection<SpecialUser> SpecialUser { get; set; } = [];
}
