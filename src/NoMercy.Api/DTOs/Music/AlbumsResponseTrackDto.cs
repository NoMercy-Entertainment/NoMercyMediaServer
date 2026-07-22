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

public record AlbumsResponseTrackDto
{
    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "date")]
    public DateTime? Date { get; set; }

    [JsonProperty(propertyName: "disc")]
    public int? Disc { get; set; }

    [JsonProperty(propertyName: "duration")]
    public string? Duration { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool Favorite { get; set; }

    [JsonProperty(propertyName: "filename")]
    public string? Filename { get; set; }

    [JsonProperty(propertyName: "folder")]
    public string? Folder { get; set; }

    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "library_id")]
    public Ulid LibraryId { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; }

    [JsonProperty(propertyName: "origin")]
    public Guid Origin { get; set; }

    [JsonProperty(propertyName: "path")]
    public string Path { get; set; }

    [JsonProperty(propertyName: "quality")]
    public int? Quality { get; set; }

    [JsonProperty(propertyName: "track")]
    public int? Track { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    [JsonProperty(propertyName: "album_track")]
    public List<AlbumDto> Album { get; set; }

    [JsonProperty(propertyName: "artist_track")]
    public List<ArtistDto> Artist { get; set; }

    public AlbumsResponseTrackDto(AlbumTrack artistTrack, Ulid libraryId, string country)
    {
        ColorPalette = artistTrack.Track.ColorPalette;
        Cover = artistTrack.Track.Cover is not null
            ? new Uri(uriString: $"/images/music{artistTrack.Track.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Date = artistTrack.Track.Date;
        Disc = artistTrack.Track.DiscNumber;
        Duration = artistTrack.Track.Duration;
        Favorite = artistTrack.Track.TrackUser.Count != 0;
        Filename = artistTrack.Track.Filename;
        Folder = artistTrack.Track.Folder;
        Id = artistTrack.Track.Id;
        LibraryId = libraryId;
        Name = artistTrack.Track.Name;
        Origin = Info.DeviceId;
        Path = artistTrack.Track.Folder + "/" + artistTrack.Track.Filename;
        Quality = artistTrack.Track.Quality;
        Track = artistTrack.Track.TrackNumber;
        Type = "track";
        Link = new(uriString: $"/music/albums/{artistTrack.AlbumId}", uriKind: UriKind.Relative);

        Album = artistTrack
            .Track.AlbumTrack.Select(selector: albumTrack => new AlbumDto(albumTrack: albumTrack, country: country))
            .ToList();

        Artist = artistTrack
            .Track.ArtistTrack.Select(selector: trackArtist => new ArtistDto(artistTrack: trackArtist, country: country))
            .ToList();
    }
}
