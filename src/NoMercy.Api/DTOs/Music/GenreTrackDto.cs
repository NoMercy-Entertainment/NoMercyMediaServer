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

public record GenreTrackDto
{
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "path")]
    public string Path { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "date")]
    public DateTime? Date { get; set; }

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

    [JsonProperty(propertyName: "lyrics")]
    public Lyric[]? Lyrics { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    [JsonProperty(propertyName: "artist_track")]
    public IEnumerable<ArtistDto> Artists { get; set; } = [];

    [JsonProperty(propertyName: "album_track")]
    public IEnumerable<AlbumDto> Albums { get; set; } = [];

    public GenreTrackDto(MusicGenreTrack genreTrack, string country)
    {
        Id = genreTrack.Track.Id;
        Name = genreTrack.Track.Name;
        Cover = genreTrack.Track.Cover is not null
            ? new Uri(uriString: $"/images/music{genreTrack.Track.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Path = new Uri(
            uriString: $"/{genreTrack.Track.FolderId}{genreTrack.Track.Folder}{genreTrack.Track.Filename}",
            uriKind: UriKind.Relative
        ).ToString();
        Type = "track";
        ColorPalette = genreTrack.Track.ColorPalette;
        Date = genreTrack.Track.Date;
        Disc = genreTrack.Track.DiscNumber;
        Duration = genreTrack.Track.Duration;
        Favorite = genreTrack.Track.TrackUser.Count != 0;
        Quality = genreTrack.Track.Quality;
        Track = genreTrack.Track.TrackNumber;
        Lyrics = genreTrack.Track.Lyrics;
        Link = new(uriString: $"/music/tracks/{Id}", uriKind: UriKind.Relative);

        Artists = genreTrack.Track.ArtistTrack.Select(selector: artistTrack => new ArtistDto(
            artistTrack: artistTrack,
            country: country
        ));

        Albums = genreTrack
            .Track.AlbumTrack.Select(selector: album => new AlbumDto(album: album.Album, country: country!))
            .GroupBy(keySelector: album => album.Id)
            .Select(selector: album => album.First())
            .OrderBy(keySelector: artistTrack => artistTrack.Year);
    }
}
