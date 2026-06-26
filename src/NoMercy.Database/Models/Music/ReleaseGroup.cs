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

namespace NoMercy.Database.Models.Music;

[PrimaryKey(nameof(Id))]
public class ReleaseGroup : ColorPaletteTimeStamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("disambiguation")]
    public string? Disambiguation { get; set; }

    [MaxLength(4096)]
    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("year")]
    public int Year { get; set; }

    [JsonProperty("cover")]
    public string? Cover { get; set; }

    [JsonProperty("library_id")]
    public Ulid? LibraryId { get; set; }
    public Library? Library { get; set; }

    [JsonProperty("albums")]
    public ICollection<AlbumReleaseGroup> AlbumReleaseGroup { get; set; } = [];

    [JsonProperty("artists")]
    public ICollection<ArtistReleaseGroup> ArtistReleaseGroup { get; set; } = [];

    [JsonProperty("translations")]
    public ICollection<Translation> Translations { get; set; } = [];
}
