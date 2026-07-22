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

public record MusicPlaylistResponseItemDto
{
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty(propertyName: "updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    [JsonProperty(propertyName: "tracks")]
    public ICollection<PlaylistTrack> Tracks { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    public MusicPlaylistResponseItemDto(Playlist playlist)
    {
        Id = playlist.Id;
        Name = playlist.Name;
        Description = playlist.Description;
        Cover = playlist.Cover is not null
            ? new Uri(uriString: $"/images/music{playlist.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        ColorPalette = playlist.ColorPalette;
        CreatedAt = playlist.CreatedAt;
        UpdatedAt = playlist.UpdatedAt;
        Tracks = playlist.Tracks;
        Type = "playlist";
        Link = new(uriString: $"/music/playlists/{Id}", uriKind: UriKind.Relative);
    }
}
