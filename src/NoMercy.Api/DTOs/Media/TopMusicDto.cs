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
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Music;

namespace NoMercy.Api.DTOs.Media;

public record TopMusicDto
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = "albums";

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    public TopMusicDto()
    {
        //
    }

    public TopMusicDto(PlaylistTrack musicPlay)
    {
        Id = musicPlay.Playlist.Id.ToString();
        Name = musicPlay.Playlist.Name;
        ColorPalette = musicPlay.Playlist.ColorPalette;
        Type = "playlist";
        Link = new(uriString: $"/music/playlists/{Id}", uriKind: UriKind.Relative);
        Cover = musicPlay.Playlist.Cover;
        Cover = Cover is not null
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;
    }

    public TopMusicDto(AlbumTrack albumTrack)
    {
        Id = albumTrack.Album.Id.ToString();
        Name = albumTrack.Album.Name;
        ColorPalette = albumTrack.Album.ColorPalette;
        Type = "album";
        Link = new(uriString: $"/music/albums/{Id}", uriKind: UriKind.Relative);
        Cover = albumTrack.Album.Cover;
        Cover = Cover is not null
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;
    }

    public TopMusicDto(ArtistTrack artistTrack)
    {
        Id = artistTrack.Artist.Id.ToString();
        Name = artistTrack.Artist.Name;
        ColorPalette = artistTrack.Artist.ColorPalette;
        Type = "artist";
        Link = new(uriString: $"/music/artists/{Id}", uriKind: UriKind.Relative);
        Cover = artistTrack.Artist.Cover;
        Cover = Cover is not null
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;
    }

    public TopMusicDto(TopMusicItemDto item)
    {
        Id = item.Id;
        Name = item.Name;
        ColorPalette = ColorPalette.FromJsonOrNull(json: item.ColorPalette);
        Type = item.Type;
        Link = item.Type switch
        {
            "artist" => new(uriString: $"/music/artists/{Id}", uriKind: UriKind.Relative),
            "album" => new(uriString: $"/music/albums/{Id}", uriKind: UriKind.Relative),
            "playlist" => new(uriString: $"/music/playlists/{Id}", uriKind: UriKind.Relative),
            _ => new(uriString: $"/music/{Id}", uriKind: UriKind.Relative),
        };
        Cover = item.Cover;
        Cover = Cover is not null
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;
    }
}
