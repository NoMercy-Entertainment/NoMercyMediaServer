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

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Data for NMTopResultCard component - search top result.
/// </summary>
public record TopResultCardData
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = null!;

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = null!;

    [JsonProperty(propertyName: "link")]
    public string Link { get; set; } = null!;

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "artists")]
    public IEnumerable<TopResultArtist> Artists { get; set; } = [];

    [JsonProperty(propertyName: "albums")]
    public IEnumerable<TopResultAlbum> Albums { get; set; } = [];

    [JsonProperty(propertyName: "track")]
    public TopResultTrack? Track { get; set; }

    public TopResultCardData() { }

    public TopResultCardData(Artist artist)
    {
        Id = artist.Id.ToString();
        Title = artist.Name;
        Type = "artist";
        Link = $"/music/artists/{artist.Id}";
        Cover = artist.Cover;
        ColorPalette = artist.ColorPalette;
        Link = $"/music/albums/{artist.Id}";
        Type = "album";
    }

    public TopResultCardData(Album album)
    {
        Id = album.Id.ToString();
        Title = album.Name;
        Type = "album";
        Link = $"/music/albums/{album.Id}";
        Cover = album.Cover;
        ColorPalette = album.ColorPalette;
        Artists = album.AlbumArtist.Select(selector: aa => new TopResultArtist
        {
            Id = aa.ArtistId.ToString(),
            Name = aa.Artist.Name,
            Link = new(uriString: $"/music/artists/{aa.ArtistId}", uriKind: UriKind.Relative),
            Type = "artist",
        });
    }

    public TopResultCardData(Track track)
    {
        Id = track.Id.ToString();
        Title = track.Name;
        Type = "track";
        Link = $"/music/tracks/{track.Id}";
        Cover = track.Cover;
        ColorPalette = track.ColorPalette;
        Artists = track.ArtistTrack.Select(selector: at => new TopResultArtist
        {
            Id = at.ArtistId.ToString(),
            Name = at.Artist.Name,
            Link = new(uriString: $"/music/artists/{at.ArtistId}", uriKind: UriKind.Relative),
            Type = "artist",
        });
        Albums = track.AlbumTrack.Select(selector: at => new TopResultAlbum
        {
            Id = at.AlbumId.ToString(),
            Name = at.Album.Name,
            Link = new(uriString: $"/music/albums/{at.AlbumId}", uriKind: UriKind.Relative),
            Type = "album",
        });
        Track = new()
        {
            Id = track.Id.ToString(),
            Name = track.Name,
            Duration = track.Duration,
            Path = $"/{track.FolderId}{track.Folder}{track.Filename}",
            Link = new(uriString: $"/music/tracks/{track.Id}", uriKind: UriKind.Relative),
            Type = "track",
            Disc = track.DiscNumber,
            Track = track.TrackNumber,
            Quality = track.Quality,
            Artists = track.ArtistTrack.Select(selector: at => new TopResultArtist
            {
                Id = at.ArtistId.ToString(),
                Name = at.Artist.Name,
                Link = new(uriString: $"/music/artists/{at.ArtistId}", uriKind: UriKind.Relative),
                Type = "artist",
            }),
            Albums = track.AlbumTrack.Select(selector: at => new TopResultAlbum
            {
                Id = at.AlbumId.ToString(),
                Name = at.Album.Name,
                Link = new(uriString: $"/music/albums/{at.AlbumId}", uriKind: UriKind.Relative),
                Type = "album",
            }),
        };
    }

    public TopResultCardData(SearchTrackCardDto track)
    {
        Id = track.Id.ToString();
        Title = track.Name;
        Type = "track";
        Link = $"/music/tracks/{track.Id}";
        string? cover = track.AlbumCover ?? track.ArtistCover;
        Cover = cover is not null ? $"/images/music{cover}" : null;
        string? colorPaletteStr = track.AlbumColorPalette ?? track.ArtistColorPalette;
        ColorPalette = ColorPalette.FromJsonOrNull(json: colorPaletteStr);
        Artists = track.Artists.Select(selector: at => new TopResultArtist
        {
            Id = at.Id.ToString(),
            Name = at.Name,
            Link = new(uriString: $"/music/artists/{at.Id}", uriKind: UriKind.Relative),
            Type = "artist",
        });
        Albums = track.Albums.Select(selector: at => new TopResultAlbum
        {
            Id = at.Id.ToString(),
            Name = at.Name,
            Link = new(uriString: $"/music/albums/{at.Id}", uriKind: UriKind.Relative),
            Type = "album",
        });
        Track = new()
        {
            Id = track.Id.ToString(),
            Name = track.Name,
            Duration = track.Duration,
            Path = $"/{track.FolderId}{track.Folder}{track.Filename}",
            Link = new(uriString: $"/music/tracks/{track.Id}", uriKind: UriKind.Relative),
            Type = "track",
            Disc = track.DiscNumber,
            Track = track.TrackNumber,
            Quality = track.Quality,
            Artists = track.Artists.Select(selector: at => new TopResultArtist
            {
                Id = at.Id.ToString(),
                Name = at.Name,
                Link = new(uriString: $"/music/artists/{at.Id}", uriKind: UriKind.Relative),
                Type = "artist",
            }),
            Albums = track.Albums.Select(selector: at => new TopResultAlbum
            {
                Id = at.Id.ToString(),
                Name = at.Name,
                Link = new(uriString: $"/music/albums/{at.Id}", uriKind: UriKind.Relative),
                Type = "album",
            }),
        };
    }

    public TopResultCardData(ArtistCardDto artist)
    {
        Id = artist.Id.ToString();
        Title = artist.Name;
        Type = "artist";
        Link = $"/music/artists/{artist.Id}";
        Cover = artist.Cover is not null ? $"/images/music{artist.Cover}" : null;
        ColorPalette = ColorPalette.FromJsonOrNull(json: artist.ColorPalette);
    }

    public TopResultCardData(AlbumCardDto album)
    {
        Id = album.Id.ToString();
        Title = album.Name;
        Type = "album";
        Link = $"/music/albums/{album.Id}";
        Cover = album.Cover is not null ? $"/images/music{album.Cover}" : null;
        ColorPalette = ColorPalette.FromJsonOrNull(json: album.ColorPalette);
    }
}

public record TopResultArtist
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = null!;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = null!;

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = null!;
}

public record TopResultAlbum
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = null!;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = null!;

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = null!;
}

public record TopResultTrack
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = null!;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = null!;

    [JsonProperty(propertyName: "duration")]
    public string? Duration { get; set; }

    [JsonProperty(propertyName: "path")]
    public string? Path { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = null!;

    [JsonProperty(propertyName: "disc")]
    public int Disc { get; set; }

    [JsonProperty(propertyName: "track")]
    public int Track { get; set; }

    [JsonProperty(propertyName: "quality")]
    public int? Quality { get; set; }

    [JsonProperty(propertyName: "artist_track")]
    public IEnumerable<TopResultArtist> Artists { get; set; } = [];

    [JsonProperty(propertyName: "album_track")]
    public IEnumerable<TopResultAlbum> Albums { get; set; } = [];
}
