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

using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.DTOs.Music;

public record ReleaseGroupDto
{
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "library_id")]
    public Ulid? LibraryId { get; set; }

    [JsonProperty(propertyName: "origin")]
    public Guid Origin { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    [JsonProperty(propertyName: "year")]
    public int Year { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    public ReleaseGroupDto(AlbumReleaseGroup artistReleaseGroup, string country)
    {
        string? description = artistReleaseGroup
            .ReleaseGroup.Translations.FirstOrDefault(predicate: translation =>
                translation.Iso31661 == country
            )
            ?.Description;

        Description = !string.IsNullOrEmpty(value: description)
            ? description
            : artistReleaseGroup.ReleaseGroup.Description;

        Id = artistReleaseGroup.ReleaseGroupId;
        Title = artistReleaseGroup.ReleaseGroup.Title;
        Cover = artistReleaseGroup.ReleaseGroup.Cover;
        Cover = Cover is not null
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        ColorPalette = artistReleaseGroup.ReleaseGroup.ColorPalette;
        LibraryId = artistReleaseGroup.ReleaseGroup.LibraryId;
        Origin = Info.DeviceId;
        Type = "release_groups";
        Year = artistReleaseGroup.ReleaseGroup.Year;
        Link = new(uriString: $"/music/release_groups/{Id}", uriKind: UriKind.Relative);
    }
}
