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
using CarouselResponseItemDtoRepository = NoMercy.Data.DTOs.CarouselResponseItemDto;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Data for NMMusicCard component - music album/artist card.
/// </summary>
public record MusicCardData
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = null!;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = null!;

    [JsonProperty(propertyName: "link")]
    public string Link { get; set; } = null!;

    [JsonProperty(propertyName: "type")]
    public string? Type { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "logo")]
    public string? Logo { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "disambiguation")]
    public string? Disambiguation { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool? Favorite { get; set; }

    [JsonProperty(propertyName: "folder")]
    public string? Folder { get; set; }

    [JsonProperty(propertyName: "libraryID")]
    public string? LibraryId { get; set; }

    [JsonProperty(propertyName: "trackID")]
    public string? TrackId { get; set; }

    [JsonProperty(propertyName: "tracks")]
    public long? Tracks { get; set; }

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    public MusicCardData() { }

    public MusicCardData(Album album)
    {
        Id = album.Id.ToString();
        Name = album.Name;
        Cover = MusicCover.Url(cover: album.Cover);
        Type = "album";
        Link = $"/music/albums/{album.Id}";
        ColorPalette = album.ColorPalette;
        Year = album.Year;
        Tracks = album.AlbumTrack.Count;
        LibraryId = album.LibraryId.ToString();
    }

    public MusicCardData(Artist artist)
    {
        Id = artist.Id.ToString();
        Name = artist.Name;
        Cover = MusicCover.Url(cover: artist.Cover);
        Type = "artist";
        Link = $"/music/artists/{artist.Id}";
        ColorPalette = artist.ColorPalette;
        Disambiguation = artist.Disambiguation;
        Description = artist.Description;
        Tracks = artist.ArtistTrack.Count;
        LibraryId = artist.LibraryId.ToString();
    }

    public MusicCardData(Playlist playlist)
    {
        Id = playlist.Id.ToString();
        Name = playlist.Name;
        Cover = MusicCover.Url(cover: playlist.Cover);
        Type = "playlist";
        Link = $"/music/playlists/{playlist.Id}";
        Tracks = playlist.Tracks.Count;
    }

    public MusicCardData(Track track)
    {
        Id = track.Id.ToString();
        Name = track.Name;
        Type = "track";
        Link = $"/music/tracks/{track.Id}";
        Tracks = 1;
        TrackId = track.Id.ToString();
    }

    public MusicCardData(CarouselResponseItemDto carousel)
    {
        Id = carousel.Id;
        Name = carousel.Name;
        Cover = MusicCover.Url(cover: carousel.Cover);
        Type = carousel.Type;
        Link = carousel.Link.ToString();
        Tracks = carousel.Tracks;
        ColorPalette = carousel.ColorPalette;
    }

    public MusicCardData(CarouselResponseItemDtoRepository carousel)
    {
        Id = carousel.Id;
        Name = carousel.Name.ToTitleCase();
        Cover = MusicCover.Url(cover: carousel.Cover);
        Type = carousel.Type;
        Link = carousel.Link.ToString();
        Tracks = carousel.Tracks;
        ColorPalette = carousel.ColorPalette;
    }

    public MusicCardData(ArtistCardDto artist)
    {
        Id = artist.Id.ToString();
        Name = artist.Name;
        Cover = MusicCover.Url(cover: artist.Cover);
        Type = "artist";
        Link = $"/music/artists/{artist.Id}";
        ColorPalette = ColorPalette.FromJsonOrNull(json: artist.ColorPalette);
        Disambiguation = artist.Disambiguation;
        Description = artist.Description;
        Tracks = artist.TrackCount;
        LibraryId = artist.LibraryId?.ToString();
    }

    public MusicCardData(AlbumCardDto album)
    {
        Id = album.Id.ToString();
        Name = album.Name;
        Cover = MusicCover.Url(cover: album.Cover);
        Type = "album";
        Link = $"/music/albums/{album.Id}";
        ColorPalette = ColorPalette.FromJsonOrNull(json: album.ColorPalette);
        Year = album.Year;
        Tracks = album.TrackCount;
        LibraryId = album.LibraryId.ToString();
    }

    public MusicCardData(PlaylistCardDto playlist)
    {
        Id = playlist.Id.ToString();
        Name = playlist.Name;
        Cover = MusicCover.Url(cover: playlist.Cover);
        Type = "playlist";
        Link = $"/music/playlists/{playlist.Id}";
        ColorPalette = ColorPalette.FromJsonOrNull(json: playlist.ColorPalette);
        Tracks = playlist.TrackCount;
    }

    public MusicCardData(MusicGenreCardDto genre)
    {
        Id = genre.Id.ToString();
        Name = genre.Name.ToTitleCase();
        Type = "genre";
        Link = $"/music/genres/{genre.Id}";
        Tracks = genre.TrackCount;
    }
}

/// <summary>
/// Data for NMMusicHomeCard component - music home featured card (similar to MusicCardData).
/// </summary>
public record MusicHomeCardData
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = null!;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = null!;

    [JsonProperty(propertyName: "link")]
    public string Link { get; set; } = null!;

    [JsonProperty(propertyName: "type")]
    public string? Type { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "disambiguation")]
    public string? Disambiguation { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool? Favorite { get; set; }

    [JsonProperty(propertyName: "folder")]
    public string? Folder { get; set; }

    [JsonProperty(propertyName: "libraryID")]
    public string? LibraryId { get; set; }

    [JsonProperty(propertyName: "trackID")]
    public string? TrackId { get; set; }

    [JsonProperty(propertyName: "tracks")]
    public long? Tracks { get; set; }

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    public MusicHomeCardData() { }

    public MusicHomeCardData(Album album)
    {
        Id = album.Id.ToString();
        Name = album.Name;
        Cover = MusicCover.Url(cover: album.Cover);
        Type = "album";
        Link = $"/music/albums/{album.Id}";
        ColorPalette = album.ColorPalette;
        Year = album.Year;
        Tracks = album.AlbumTrack.Count;
        LibraryId = album.LibraryId.ToString();
    }

    public MusicHomeCardData(Artist artist)
    {
        Id = artist.Id.ToString();
        Name = artist.Name;
        Cover = MusicCover.Url(cover: artist.Cover);
        Type = "artist";
        Link = $"/music/artists/{artist.Id}";
        ColorPalette = artist.ColorPalette;
        Disambiguation = artist.Disambiguation;
        Description = artist.Description;
        Tracks = artist.ArtistTrack.Count;
        LibraryId = artist.LibraryId.ToString();
    }

    public MusicHomeCardData(TopMusicDto topMusic)
    {
        Id = topMusic.Id;
        Name = topMusic.Name;
        Cover = MusicCover.Url(cover: topMusic.Cover);
        Type = topMusic.Type;
        Link = topMusic.Link.ToString();
        ColorPalette = topMusic.ColorPalette;
    }
}

file static class MusicCover
{
    public static string? Url(string? cover) =>
        string.IsNullOrEmpty(value: cover) ? null : $"/images/music{cover}";
}
