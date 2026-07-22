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
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Music;

public record PlaylistTrackDto
{
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

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

    [JsonProperty(propertyName: "track")]
    public int? Track { get; set; }

    [JsonProperty(propertyName: "duration")]
    public string Duration { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool Favorite { get; set; }

    [JsonProperty(propertyName: "quality")]
    public int? Quality { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    [JsonProperty(propertyName: "album_name")]
    public string? AlbumName { get; set; }

    [JsonProperty(propertyName: "lyrics")]
    public Lyric[]? Lyrics { get; set; }

    [JsonProperty(propertyName: "album_track")]
    public List<AlbumDto> Album { get; set; }

    [JsonProperty(propertyName: "artist_track")]
    public List<ArtistDto> Artist { get; set; }

    public PlaylistTrackDto(Track track, string country)
    {
        Image? img = track
            .AlbumTrack.FirstOrDefault()
            ?.Album.AlbumArtist.FirstOrDefault()
            ?.Artist.Images.FirstOrDefault(predicate: image => image.Type == "background");
        Id = track.Id;
        Name = track.Name;
        Backdrop = img?.FilePath is not null
            ? new Uri(uriString: $"/images/music{img?.FilePath}", uriKind: UriKind.Relative).ToString()
            : null;
        Cover = track.AlbumTrack.FirstOrDefault()?.Album.Cover ?? track.Cover;
        Cover = Cover is not null
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Path = new Uri(
            uriString: $"/{track.FolderId}{track.Folder}{track.Filename}",
            uriKind: UriKind.Relative
        ).ToString();
        Link = new(uriString: $"/music/tracks/{track.Id}", uriKind: UriKind.Relative);
        ColorPalette = track.AlbumTrack.FirstOrDefault()?.Album.ColorPalette;
        if (ColorPalette is not null)
            ColorPalette.Backdrop = img?.ColorPalette?.Image;
        Date = track.Date;
        Disc = track.DiscNumber;
        Track = track.TrackNumber;
        Duration = track.Duration;
        Favorite = track.TrackUser.Count != 0;
        Quality = track.Quality;
        Lyrics = track.Lyrics;
        Type = "track";
        AlbumName = track.AlbumTrack.FirstOrDefault()?.Album.Name;

        Album = track
            .AlbumTrack.DistinctBy(keySelector: trackAlbum => trackAlbum.AlbumId)
            .Select(selector: albumTrack => new AlbumDto(albumTrack: albumTrack, country: country))
            .ToList();

        Artist = track
            .ArtistTrack.Select(selector: artistTrack => new ArtistDto(artistTrack: artistTrack, country: country))
            .ToList();
    }

    public PlaylistTrackDto(ArtistTrack artistTrack, string country)
    {
        Image? img = artistTrack.Artist.Images.FirstOrDefault(predicate: image => image.Type == "background");
        Id = artistTrack.Track.Id;
        Name = artistTrack.Track.Name;
        Backdrop = img?.FilePath is not null
            ? new Uri(uriString: $"/images/music{img?.FilePath}", uriKind: UriKind.Relative).ToString()
            : null;
        Cover =
            artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Cover ?? artistTrack.Track.Cover;
        Cover = Cover is not null
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Path = new Uri(
            uriString: $"/{artistTrack.Track.FolderId}{artistTrack.Track.Folder}{artistTrack.Track.Filename}",
            uriKind: UriKind.Relative
        ).ToString();
        Link = new(uriString: $"/music/tracks/{artistTrack.Track.Id}", uriKind: UriKind.Relative);

        ColorPalette = artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.ColorPalette;
        if (ColorPalette is not null)
            ColorPalette.Backdrop = img?.ColorPalette?.Image;
        Date = artistTrack.Track.Date;
        Disc = artistTrack.Track.DiscNumber;
        Track = artistTrack.Track.TrackNumber;
        Duration = artistTrack.Track.Duration;
        Favorite = artistTrack.Track.TrackUser.Count != 0;
        Quality = artistTrack.Track.Quality;
        Lyrics = artistTrack.Track.Lyrics;
        Type = "track";
        AlbumName = artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Name;

        Album = artistTrack
            .Track.AlbumTrack!.DistinctBy(keySelector: trackAlbum => trackAlbum.AlbumId)
            .Select(selector: albumTrack => new AlbumDto(albumTrack: albumTrack, country: country))
            .ToList();

        Artist = artistTrack
            .Track.ArtistTrack.Where(predicate: at => at.TrackId == artistTrack.TrackId)
            .Select(selector: at => new ArtistDto(artistTrack: at, country: country))
            .ToList();
    }

    public PlaylistTrackDto(PlaylistTrack trackTrack, string country)
    {
        Image? img = trackTrack
            .Track.AlbumTrack.FirstOrDefault()
            ?.Album.AlbumArtist.FirstOrDefault()
            ?.Artist.Images.FirstOrDefault(predicate: image => image.Type == "background");
        Id = trackTrack.Track.Id;
        Name = trackTrack.Track.Name;
        Backdrop = img?.FilePath is not null
            ? new Uri(uriString: $"/images/music{img?.FilePath}", uriKind: UriKind.Relative).ToString()
            : null;
        Cover = trackTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Cover ?? trackTrack.Track.Cover;
        Cover = Cover is not null
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Path = new Uri(
            uriString: $"/{trackTrack.Track.FolderId}{trackTrack.Track.Folder}{trackTrack.Track.Filename}",
            uriKind: UriKind.Relative
        ).ToString();
        Link = new(uriString: $"/music/tracks/{trackTrack.Track.Id}", uriKind: UriKind.Relative);
        ColorPalette = trackTrack.Track.AlbumTrack.FirstOrDefault()?.Album.ColorPalette;
        if (ColorPalette is not null)
            ColorPalette.Backdrop = img?.ColorPalette?.Image;
        Date = trackTrack.Track.Date;
        Disc = trackTrack.Track.DiscNumber;
        Track = trackTrack.Track.TrackNumber;
        Duration = trackTrack.Track.Duration;
        Favorite = trackTrack.Track.TrackUser.Count != 0;
        Quality = trackTrack.Track.Quality;
        Lyrics = trackTrack.Track.Lyrics;
        Type = "track";
        AlbumName = trackTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Name;

        Album = trackTrack
            .Track.AlbumTrack.DistinctBy(keySelector: trackAlbum => trackAlbum.AlbumId)
            .Select(selector: albumTrack => new AlbumDto(albumTrack: albumTrack, country: country))
            .ToList();

        Artist = trackTrack
            .Track.ArtistTrack.Select(selector: albumTrack => new ArtistDto(artistTrack: albumTrack, country: country))
            .ToList();
    }

    public PlaylistTrackDto(AlbumTrack artistTrack, string country)
    {
        Image? img = artistTrack
            .Track.AlbumTrack.FirstOrDefault()
            ?.Album.Images.FirstOrDefault(predicate: image => image.Type == "background");
        Id = artistTrack.Track.Id;
        Name = artistTrack.Track.Name;
        Backdrop = img?.FilePath is not null
            ? new Uri(uriString: $"/images/music{img?.FilePath}", uriKind: UriKind.Relative).ToString()
            : null;
        Cover =
            artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Cover ?? artistTrack.Track.Cover;
        Cover = Cover is not null
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Path = new Uri(
            uriString: $"/{artistTrack.Track.FolderId}{artistTrack.Track.Folder}{artistTrack.Track.Filename}",
            uriKind: UriKind.Relative
        ).ToString();
        Link = new(uriString: $"/music/tracks/{artistTrack.Track.Id}", uriKind: UriKind.Relative);

        ColorPalette = artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.ColorPalette;
        if (ColorPalette is not null)
            ColorPalette.Backdrop = img?.ColorPalette?.Image;
        Date = artistTrack.Track.Date;
        Disc = artistTrack.Track.DiscNumber;
        Track = artistTrack.Track.TrackNumber;
        Duration = artistTrack.Track.Duration;
        Favorite = artistTrack.Track.TrackUser.Count != 0;
        Quality = artistTrack.Track.Quality;
        Lyrics = artistTrack.Track.Lyrics;
        Type = "track";
        AlbumName = artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Name;

        Album = artistTrack
            .Track.AlbumTrack.DistinctBy(keySelector: trackAlbum => trackAlbum.AlbumId)
            .Select(selector: albumTrack => new AlbumDto(albumTrack: albumTrack, country: country))
            .ToList();

        Artist = artistTrack
            .Track.ArtistTrack.Select(selector: albumTrack => new ArtistDto(artistTrack: albumTrack, country: country))
            .ToList();
    }

    public PlaylistTrackDto(MusicGenreTrack genreTrack, string country)
    {
        Image? img = genreTrack
            .Track.AlbumTrack.FirstOrDefault()
            ?.Album.AlbumArtist.FirstOrDefault()
            ?.Artist.Images.FirstOrDefault(predicate: image => image.Type == "background");
        Id = genreTrack.Track.Id;
        Name = genreTrack.Track.Name.ToTitleCase();
        Backdrop = img?.FilePath is not null
            ? new Uri(uriString: $"/images/music{img?.FilePath}", uriKind: UriKind.Relative).ToString()
            : null;
        Cover = genreTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Cover ?? genreTrack.Track.Cover;
        Cover = Cover is not null
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Path = new Uri(
            uriString: $"/{genreTrack.Track.FolderId}{genreTrack.Track.Folder}{genreTrack.Track.Filename}",
            uriKind: UriKind.Relative
        ).ToString();
        Link = new(uriString: $"/music/tracks/{genreTrack.Track.Id}", uriKind: UriKind.Relative);
        ColorPalette = genreTrack.Track.AlbumTrack.FirstOrDefault()?.Album.ColorPalette;
        if (ColorPalette is not null)
            ColorPalette.Backdrop = img?.ColorPalette?.Image;
        Date = genreTrack.Track.Date;
        Disc = genreTrack.Track.DiscNumber;
        Track = genreTrack.Track.TrackNumber;
        Duration = genreTrack.Track.Duration;
        Favorite = genreTrack.Track.TrackUser.Count != 0;
        Quality = genreTrack.Track.Quality;
        Lyrics = genreTrack.Track.Lyrics;
        Type = "track";
        AlbumName = genreTrack.Track.AlbumTrack.FirstOrDefault()?.Album.Name;

        Album = genreTrack
            .Track.AlbumTrack.DistinctBy(keySelector: trackAlbum => trackAlbum.AlbumId)
            .Select(selector: albumTrack => new AlbumDto(albumTrack: albumTrack, country: country))
            .ToList();

        Artist = genreTrack
            .Track.ArtistTrack.Select(selector: artistTrack => new ArtistDto(artistTrack: artistTrack, country: country))
            .ToList();
    }
}
