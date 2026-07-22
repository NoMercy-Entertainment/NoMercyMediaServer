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

namespace NoMercy.Data.DTOs;

public record CarouselResponseItemDto
{
    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "disambiguation")]
    public string? Disambiguation { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool Favorite { get; set; }

    [JsonProperty(propertyName: "folder")]
    public string? Folder { get; set; }

    [JsonProperty(propertyName: "id")]
    public string Id { get; set; }

    [JsonProperty(propertyName: "library_id")]
    public Ulid? LibraryId { get; set; }

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

    public CarouselResponseItemDto(Artist artist)
    {
        ColorPalette = artist.ColorPalette;
        Cover = artist.Cover is not null
            ? new Uri(uriString: $"/images/music{artist.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Disambiguation = artist.Disambiguation;
        Description = artist.Description;
        Folder = artist.Folder.OrEmpty();
        Id = artist.Id.ToString();
        LibraryId = artist.LibraryId;
        Name = artist.Name;
        Type = "artist";
        Link = new(uriString: $"/music/artists/{Id}", uriKind: UriKind.Relative);

        Tracks = artist
            .ArtistTrack.DistinctBy(keySelector: artistTrack => artistTrack.Track.Name.ToLower())
            .Count();
    }

    public CarouselResponseItemDto(Album album)
    {
        ColorPalette = album.ColorPalette;
        Cover = album.Cover is not null
            ? new Uri(uriString: $"/images/music{album.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Disambiguation = album.Disambiguation;
        Description = album.Description;
        Folder = album.Folder.OrEmpty();
        Id = album.Id.ToString();
        LibraryId = album.LibraryId;
        Name = album.Name;
        Type = "album";
        Link = new(uriString: $"/music/albums/{Id}", uriKind: UriKind.Relative);

        Tracks = album.AlbumTrack.DistinctBy(keySelector: albumTrack => albumTrack.Track.Name.ToLower()).Count();
    }

    public CarouselResponseItemDto(ArtistUser artistUser)
    {
        ColorPalette = artistUser.Artist.ColorPalette;
        Cover = artistUser.Artist.Cover ?? artistUser.Artist.Images.FirstOrDefault()?.FilePath;
        Cover = Cover is not null
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Disambiguation = artistUser.Artist.Disambiguation;
        Description = artistUser.Artist.Description;
        Folder = artistUser.Artist.Folder.OrEmpty();
        Id = artistUser.Artist.Id.ToString();
        LibraryId = artistUser.Artist.LibraryId;
        Name = artistUser.Artist.Name;
        Type = "artist";
        Link = new(uriString: $"/music/artists/{Id}", uriKind: UriKind.Relative);

        Tracks = artistUser
            .Artist.ArtistTrack.DistinctBy(keySelector: artistTrack => artistTrack.Track.Name.ToLower())
            .Count();
    }

    public CarouselResponseItemDto(AlbumUser playlist)
    {
        ColorPalette = playlist.Album.ColorPalette;
        Cover = playlist.Album.Cover is not null
            ? new Uri(uriString: $"/images/music{playlist.Album.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Disambiguation = playlist.Album.Disambiguation;
        Description = playlist.Album.Description;
        Folder = playlist.Album.Folder.OrEmpty();
        Id = playlist.Album.Id.ToString();
        LibraryId = playlist.Album.LibraryId;
        Name = playlist.Album.Name;
        Type = "album";
        Link = new(uriString: $"/music/albums/{Id}", uriKind: UriKind.Relative);

        Tracks = playlist
            .Album.AlbumTrack.DistinctBy(keySelector: albumTrack => albumTrack.Track.Name.ToLower())
            .Count();
    }

    public CarouselResponseItemDto(Playlist playlist)
    {
        ColorPalette = playlist.ColorPalette;
        Cover = playlist.Cover is not null
            ? new Uri(uriString: $"/images/music{playlist.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Description = playlist.Description;
        Id = playlist.Id.ToString();
        Name = playlist.Name;
        Type = "playlist";
        Link = new(uriString: $"/music/playlists/{Id}", uriKind: UriKind.Relative);

        Tracks = playlist
            .Tracks.DistinctBy(keySelector: playlistTrack => playlistTrack.Track.Name.ToLower())
            .Count();
    }

    public CarouselResponseItemDto(Track track)
    {
        ColorPalette = track.ColorPalette;
        Cover = track.Cover is not null
            ? new Uri(uriString: $"/images/music{track.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Folder = track.Folder.OrEmpty();
        Id = track.Id.ToString();
        Name = track.Name;
        Type = "track";
        Link = new(uriString: $"/music/tracks/{Id}", uriKind: UriKind.Relative);
    }

    public CarouselResponseItemDto(MusicGenre genre)
    {
        Id = genre.Id.ToString();
        Name = genre.Name.ToTitleCase();
        Type = "genre";
        Link = new(uriString: $"/music/genres/{Id}", uriKind: UriKind.Relative);

        Tracks = genre.MusicGenreTracks.Count;
    }

    public CarouselResponseItemDto(ArtistCardDto artist)
    {
        ColorPalette = ColorPalette.FromJsonOrNull(json: artist.ColorPalette);
        Cover = artist.Cover ?? artist.ThumbImagePath;
        Cover = Cover is not null
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Disambiguation = artist.Disambiguation;
        Description = artist.Description;
        Folder = artist.Folder.OrEmpty();
        Id = artist.Id.ToString();
        LibraryId = artist.LibraryId;
        Name = artist.Name;
        Type = "artist";
        Link = new(uriString: $"/music/artists/{Id}", uriKind: UriKind.Relative);
        Tracks = artist.TrackCount;
    }

    public CarouselResponseItemDto(AlbumCardDto album)
    {
        ColorPalette = ColorPalette.FromJsonOrNull(json: album.ColorPalette);
        Cover = album.Cover is not null
            ? new Uri(uriString: $"/images/music{album.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Disambiguation = album.Disambiguation;
        Description = album.Description;
        Folder = album.Folder.OrEmpty();
        Id = album.Id.ToString();
        LibraryId = album.LibraryId;
        Name = album.Name;
        Type = "album";
        Link = new(uriString: $"/music/albums/{Id}", uriKind: UriKind.Relative);
        Tracks = album.TrackCount;
    }

    public CarouselResponseItemDto(PlaylistCardDto playlist)
    {
        ColorPalette = ColorPalette.FromJsonOrNull(json: playlist.ColorPalette);
        Cover = playlist.Cover is not null
            ? new Uri(uriString: $"/images/music{playlist.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Description = playlist.Description;
        Id = playlist.Id.ToString();
        Name = playlist.Name;
        Type = "playlist";
        Link = new(uriString: $"/music/playlists/{Id}", uriKind: UriKind.Relative);
        Tracks = playlist.TrackCount;
    }

    public CarouselResponseItemDto(MusicGenreCardDto genre)
    {
        Id = genre.Id.ToString();
        Name = genre.Name.ToTitleCase();
        Type = "genre";
        Link = new(uriString: $"/music/genres/{Id}", uriKind: UriKind.Relative);
        Tracks = genre.TrackCount;
    }
}
