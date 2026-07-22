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

public record PlaylistResponseItemDto
{
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "country")]
    public string? Country { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool Favorite { get; set; }

    [JsonProperty(propertyName: "library_id")]
    public Ulid? LibraryId { get; set; }

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "artists")]
    public IEnumerable<ArtistDto> Artists { get; set; }

    [JsonProperty(propertyName: "tracks")]
    public IEnumerable<PlaylistTrackDto> Tracks { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    public PlaylistResponseItemDto(Playlist playlist, string? country = "US")
    {
        ColorPalette = playlist.ColorPalette;
        Cover = !string.IsNullOrEmpty(value: playlist.Cover)
            ? new Uri(uriString: $"/images/music{playlist.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Description = playlist.Description;
        Id = playlist.Id;
        Name = playlist.Name;
        Link = new(uriString: $"/music/playlists/{Id}", uriKind: UriKind.Relative);
        Type = "playlist";
        Artists = [];

        Tracks = playlist.Tracks.Select(selector: albumTrack => new PlaylistTrackDto(trackTrack: albumTrack, country: country!));
    }
}
