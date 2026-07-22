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

public record MusicSearchResponseItemDto
{
    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "disambiguation")]
    public string? Disambiguation { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; }

    [JsonProperty(propertyName: "track_id")]
    public string? TrackId { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    [JsonProperty(propertyName: "tracks")]
    public int Tracks { get; set; }

    public MusicSearchResponseItemDto(Artist artist)
    {
        ColorPalette = artist.ColorPalette;
        Cover = artist.Cover ?? artist.Images.FirstOrDefault()?.FilePath;
        Cover = !string.IsNullOrEmpty(value: Cover)
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Disambiguation = artist.Disambiguation;
        Description = artist.Description;
        Id = artist.Id;
        Name = artist.Name;
        Type = "artist";
        Link = new(uriString: $"/music/artists/{Id}", uriKind: UriKind.Relative);

        Tracks = artist.ArtistTrack.Select(selector: artistTrack => artistTrack.Track).Count();
    }

    public MusicSearchResponseItemDto(Album album)
    {
        ColorPalette = album.ColorPalette;
        Cover = album.Cover ?? album.Images.FirstOrDefault()?.FilePath;
        Cover = !string.IsNullOrEmpty(value: Cover)
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Disambiguation = album.Disambiguation;
        Description = album.Description;
        Id = album.Id;
        Name = album.Name;
        Type = "album";
        Link = new(uriString: $"/music/albums/{Id}", uriKind: UriKind.Relative);

        Tracks = album.AlbumTrack.Select(selector: artistTrack => artistTrack.Track).Count();
    }
}
