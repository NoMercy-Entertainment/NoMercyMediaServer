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

public record AlbumTrackDto
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
    public IEnumerable<ArtistDto> Artists { get; set; }

    [JsonProperty(propertyName: "album_track")]
    public IEnumerable<AlbumDto> Albums { get; set; }

    public AlbumTrackDto(AlbumTrack albumTrack, string country)
    {
        Id = albumTrack.Track.Id;
        Name = albumTrack.Track.Name;
        Cover = albumTrack.Album.Cover is not null
            ? new Uri(uriString: $"/images/music{albumTrack.Album.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Path = new Uri(
            uriString: $"/{albumTrack.Track.FolderId}{albumTrack.Track.Folder}{albumTrack.Track.Filename}",
            uriKind: UriKind.Relative
        ).ToString();
        Type = "track";
        ColorPalette = albumTrack.Album.ColorPalette;
        Date = albumTrack.Track.Date;
        Disc = albumTrack.Track.DiscNumber;
        Duration = albumTrack.Track.Duration;
        Favorite = albumTrack.Track.TrackUser.Count != 0;
        Quality = albumTrack.Track.Quality;
        Track = albumTrack.Track.TrackNumber;
        Lyrics = albumTrack.Track.Lyrics;
        Link = new(uriString: $"/music/tracks/{Id}", uriKind: UriKind.Relative);

        Artists = albumTrack.Track.ArtistTrack.Select(selector: artistTrack => new ArtistDto(
            artistTrack: artistTrack,
            country: country
        ));

        Albums = albumTrack.Track.AlbumTrack.Select(selector: trackAlbum => new AlbumDto(
            albumTrack: trackAlbum,
            country: country
        ));
    }
}
