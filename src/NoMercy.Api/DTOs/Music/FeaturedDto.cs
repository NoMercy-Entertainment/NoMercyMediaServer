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

namespace NoMercy.Api.DTOs.Music;

public class FeaturedDto
{
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "disambiguation")]
    public string? Disambiguation { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "tracks")]
    public int Tracks { get; set; }

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "album_artist")]
    public Guid? AlbumArtist { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    public FeaturedDto(AlbumArtist albumArtist, string country)
    {
        string? description = albumArtist
            .Album.Translations.FirstOrDefault(predicate: translation => translation.Iso31661 == country)
            ?.Description;

        Id = albumArtist.Album.Id;
        Name = albumArtist.Album.Name;
        Cover = albumArtist.Album.Cover is not null
            ? new Uri(uriString: $"/images/music{albumArtist.Album.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Disambiguation = albumArtist.Album.Disambiguation;
        Link = new(uriString: $"/music/albums/{Id}", uriKind: UriKind.Relative);
        Description = !string.IsNullOrEmpty(value: description)
            ? description
            : albumArtist.Album.Description;
        Type = "album";
        ColorPalette = albumArtist.Album.ColorPalette;
        Tracks = albumArtist.Album.AlbumTrack.Count;
        Year = albumArtist.Album.Year;

        AlbumArtist = albumArtist.ArtistId;
    }

    public FeaturedDto(Album album, string country)
    {
        string? description = album
            .Translations.FirstOrDefault(predicate: translation => translation.Iso31661 == country)
            ?.Description;

        Id = album.Id;
        Name = album.Name;
        Disambiguation = album.Disambiguation;
        Cover = album.Cover is not null
            ? new Uri(uriString: $"/images/music{album.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Link = new(uriString: $"/music/artists/{Id}", uriKind: UriKind.Relative);
        Type = "artist";
        Description = !string.IsNullOrEmpty(value: description) ? description : album.Description;

        ColorPalette = album.ColorPalette;
    }
}
