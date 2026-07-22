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
using Newtonsoft.Json.Linq;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Music;

public record ArtistResponseItemDto
{
    [JsonProperty(propertyName: "color_palette")]
    public JToken? ColorPalette { get; set; }

    [JsonProperty(propertyName: "country")]
    public string? Country { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

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
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "library_id")]
    public Ulid? LibraryId { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    [JsonProperty(propertyName: "playlists")]
    public IEnumerable<AlbumDto> Playlists { get; set; } = [];

    [JsonProperty(propertyName: "tracks")]
    public IEnumerable<ArtistTrackDto> Tracks { get; set; } = [];

    [JsonProperty(propertyName: "favorite_tracks")]
    public List<FavoriteTrackDto> FavoriteTracks { get; set; } = [];

    [JsonProperty(propertyName: "images")]
    public IEnumerable<ImageDto> Images { get; set; } = [];

    [JsonProperty(propertyName: "genres")]
    public IEnumerable<GenreDto> Genres { get; set; } = [];

    [JsonProperty(propertyName: "albums")]
    public IEnumerable<AlbumDto> Albums { get; set; } = [];

    [JsonProperty(propertyName: "featured")]
    public List<AlbumDto> Featured { get; set; } = [];

    public ArtistResponseItemDto(Artist artist, Guid userId, string? country = "US")
    {
        string? description =
            artist
                .Translations.FirstOrDefault(predicate: translation => translation.Iso31661 == country)
                ?.Description
            ?? artist.Description;

        Image? thumb = artist
            .Images.OrderByDescending(keySelector: i => i.VoteAverage)
            .FirstOrDefault(predicate: i => i.Type == "thumb");
        Image? background = artist.Images.FirstOrDefault(predicate: image => image.Type == "background");

        Backdrop = background?.FilePath is not null
            ? new Uri(uriString: $"/images/music{background.FilePath}", uriKind: UriKind.Relative).ToString()
            : null;

        JToken? palette = artist._colorPalette.ToRaw() ?? thumb?._colorPalette.ToRaw();

        Cover = artist.Cover ?? thumb?.FilePath;
        Cover = Cover is not null
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;

        ColorPalette = palette;
        Disambiguation = artist.Disambiguation;
        Description = description;
        Favorite = artist.ArtistUser.Count != 0;
        Folder = artist.Folder;
        Id = artist.Id;
        LibraryId = artist.LibraryId;
        Name = artist.Name;
        Type = "artist";
        Link = new(uriString: $"/music/artists/{Id}", uriKind: UriKind.Relative);

        Genres = artist
            .ArtistMusicGenre.Select(selector: artistMusicGenre => new GenreDto(artistMusicGenre: artistMusicGenre))
            .ToList();

        Images = artist.Images.Select(selector: image => new ImageDto(media: image)).ToList();

        // Materialized: Featured below calls Albums.All(...) per candidate. Left lazy,
        // that re-ran this whole AlbumArtist projection for every featured album, which
        // for an artist with many track-albums was tens of seconds of recomputation.
        Albums = artist
            .AlbumArtist.Select(selector: album => new AlbumDto(albumArtist: album, country: country!))
            .GroupBy(keySelector: album => album.Id)
            .Select(selector: album => album.First())
            .OrderBy(keySelector: artistTrack => artistTrack.Year)
            .ToList();

        Featured = artist
            .ArtistTrack.Select(selector: artistTrack => artistTrack.Track.AlbumTrack.FirstOrDefault()?.Album)
            .Where(predicate: album => album != null)
            .GroupBy(keySelector: album => album!.Name.RemoveNonAlphaNumericCharacters())
            .Select(selector: album => album.First()!)
            .OrderBy(keySelector: album => album.Year)
            .Where(predicate: album => Albums.All(predicate: albumDto => albumDto.Id != album.Id))
            .Select(selector: album => new AlbumDto(album: album, country: country!))
            .OrderBy(keySelector: artistTrack => artistTrack.Year)
            .ToList();

        Playlists = artist
            .AlbumArtist.DistinctBy(keySelector: albumArtist => albumArtist.AlbumId)
            .Where(predicate: album => album.Album.AlbumUser.Any(predicate: user => user.UserId.Equals(g: userId)))
            .Select(selector: trackAlbum => new AlbumDto(albumArtist: trackAlbum, country: country!))
            .OrderBy(keySelector: album => album.Year)
            .ToList();

        Tracks = artist
            .ArtistTrack.Select(selector: artistTrack => new ArtistTrackDto(artistTrack: artistTrack, country: country!))
            .DistinctBy(keySelector: artistTrack => artistTrack.Id)
            .OrderBy(keySelector: artistTrack => artistTrack.AlbumName)
            .ThenBy(keySelector: artistTrack => artistTrack.Disc)
            .ThenBy(keySelector: artistTrack => artistTrack.Track)
            .ToList();

        // Use the per-user TrackUser "liked" relation instead of MusicPlays.
        // TrackUser is already included (filtered by userId) in the repo query,
        // so no extra fan-out. MusicPlays would re-introduce the Ed Sheeran
        // timeout via O(tracks × plays) explosion.
        FavoriteTracks = artist
            .ArtistTrack.Where(predicate: artistTrack => artistTrack.Track.TrackUser.Count > 0)
            .Select(selector: artistTrack => new FavoriteTrackDto(artistTrack: artistTrack, country: country!))
            .ToList();
    }
}
