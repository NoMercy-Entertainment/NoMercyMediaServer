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
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Data for NMTrackRow component - single track in a list.
/// </summary>
public record TrackRowData
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = null!;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = null!;

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "path")]
    public string Path { get; set; } = null!;

    [JsonProperty(propertyName: "link")]
    public string Link { get; set; } = null!;

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "date")]
    public string? Date { get; set; }

    [JsonProperty(propertyName: "disc")]
    public int? Disc { get; set; }

    [JsonProperty(propertyName: "duration")]
    public string? Duration { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool Favorite { get; set; }

    [JsonProperty(propertyName: "quality")]
    public int? Quality { get; set; }

    [JsonProperty(propertyName: "track")]
    public int? Track { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = null!;

    [JsonProperty(propertyName: "lyrics")]
    public IEnumerable<LyricLine>? Lyrics { get; set; }

    [JsonProperty(propertyName: "album_id")]
    public string AlbumId { get; set; } = null!;

    [JsonProperty(propertyName: "album_name")]
    public string AlbumName { get; set; } = null!;

    [JsonProperty(propertyName: "album_track")]
    public IEnumerable<TrackArtist> AlbumTrack { get; set; } = [];

    [JsonProperty(propertyName: "artist_track")]
    public IEnumerable<TrackArtist> ArtistTrack { get; set; } = [];

    public TrackRowData() { }

    public TrackRowData(Track track, bool isFavorite = false)
    {
        Id = track.Id.ToString();
        Name = track.Name;
        Cover = track.Cover;
        Path = track.Filename.OrEmpty();
        Link = $"/music/tracks/{track.Id}";
        ColorPalette = track.ColorPalette;
        Date = track.Date?.ToString(format: "yyyy-MM-dd");
        Disc = track.DiscNumber;
        Duration = track.Duration;
        Favorite = isFavorite;
        Quality = track.Quality;
        Track = track.TrackNumber;
        Type = "track";
        AlbumId = (track.AlbumTrack.FirstOrDefault()?.AlbumId.ToString()).OrEmpty();
        AlbumName = (track.AlbumTrack.FirstOrDefault()?.Album.Name).OrEmpty();
        ArtistTrack = track.ArtistTrack.Select(selector: at => new TrackArtist
        {
            Id = at.ArtistId.ToString(),
            Name = at.Artist.Name,
            Link = new(uriString: $"/music/artists/{at.ArtistId}", uriKind: UriKind.Relative),
            Type = "artist",
        });
    }

    public TrackRowData(Track track, string country)
    {
        Id = track.Id.ToString();
        Name = track.Name;
        ColorPalette =
            track.AlbumTrack.FirstOrDefault()?.Album.ColorPalette
            ?? track.ArtistTrack.FirstOrDefault()?.Artist.ColorPalette;
        string? cover =
            track.AlbumTrack.FirstOrDefault()?.Album.Cover
            ?? track.ArtistTrack.FirstOrDefault()?.Artist.Cover;
        Cover = cover is not null ? $"/images/music{cover}" : null;
        Path = $"/{track.FolderId}{track.Folder}{track.Filename}";
        Link = $"/music/tracks/{track.Id}";
        Date = track.UpdatedAt.ToString(format: "yyyy-MM-dd");
        Disc = track.DiscNumber;
        Duration = track.Duration;
        Favorite = track.TrackUser.Count != 0;
        Quality = track.Quality;
        Track = track.TrackNumber;
        Type = "track";
        AlbumId = (track.AlbumTrack.FirstOrDefault()?.AlbumId.ToString()).OrEmpty();
        AlbumName = (track.AlbumTrack.FirstOrDefault()?.Album.Name).OrEmpty();
        ArtistTrack = track
            .ArtistTrack.DistinctBy(keySelector: at => at.ArtistId)
            .Select(selector: at => new TrackArtist
            {
                Id = at.ArtistId.ToString(),
                Name = at.Artist.Name,
                Link = new(uriString: $"/music/artists/{at.ArtistId}", uriKind: UriKind.Relative),
                Type = "artist",
            });
        AlbumTrack = track
            .AlbumTrack.DistinctBy(keySelector: at => at.AlbumId)
            .Select(selector: at => new TrackArtist
            {
                Id = at.AlbumId.ToString(),
                Name = at.Album.Name,
                Link = new(uriString: $"/music/albums/{at.AlbumId}", uriKind: UriKind.Relative),
                Type = "album",
            });
    }

    public TrackRowData(SearchTrackCardDto track)
    {
        Id = track.Id.ToString();
        Name = track.Name;
        string? colorPaletteStr = track.AlbumColorPalette ?? track.ArtistColorPalette;
        ColorPalette = ColorPalette.FromJsonOrNull(json: colorPaletteStr);
        string? cover = track.AlbumCover ?? track.ArtistCover;
        Cover = cover is not null ? $"/images/music{cover}" : null;
        Path = $"/{track.FolderId}{track.Folder}{track.Filename}";
        Link = $"/music/tracks/{track.Id}";
        Date = track.UpdatedAt.ToString(format: "yyyy-MM-dd");
        Disc = track.DiscNumber;
        Duration = track.Duration;
        Favorite = track.Favorite;
        Quality = track.Quality;
        Track = track.TrackNumber;
        Type = "track";
        AlbumId = track.AlbumId.OrEmpty();
        AlbumName = track.AlbumName.OrEmpty();
        ArtistTrack = track.Artists.Select(selector: at => new TrackArtist
        {
            Id = at.Id.ToString(),
            Name = at.Name,
            Link = new(uriString: $"/music/artists/{at.Id}", uriKind: UriKind.Relative),
            Type = "artist",
        });
        AlbumTrack = track.Albums.Select(selector: at => new TrackArtist
        {
            Id = at.Id.ToString(),
            Name = at.Name,
            Link = new(uriString: $"/music/albums/{at.Id}", uriKind: UriKind.Relative),
            Type = "album",
        });
    }
}

public record LyricLine
{
    [JsonProperty(propertyName: "time")]
    public double Time { get; set; }

    [JsonProperty(propertyName: "text")]
    public string Text { get; set; } = null!;

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = null!;
}

public record TrackArtist
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
