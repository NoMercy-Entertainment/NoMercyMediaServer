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

using NoMercy.Api.DTOs.Music;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;
using Xunit;

namespace NoMercy.Tests.Api.Music;

[Trait("Category", "Unit")]
public class AlbumResponseItemDtoTests
{
    private static Album BuildAlbum(
        string? cover = "/album-cover.jpg",
        ColorPalette? colorPalette = null,
        AlbumUser[]? albumUsers = null,
        AlbumArtist[]? albumArtists = null,
        AlbumMusicGenre[]? albumGenres = null,
        Image[]? images = null,
        AlbumTrack[]? albumTracks = null,
        Ulid? libraryId = null
    )
    {
        Album album = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Album",
            Description = "Album description",
            Disambiguation = "Deluxe",
            Cover = cover,
            LibraryId = libraryId ?? Ulid.NewUlid(),
        };
        if (colorPalette is not null)
            album.ColorPalette = colorPalette;
        foreach (AlbumUser albumUser in albumUsers ?? [])
            album.AlbumUser.Add(albumUser);
        foreach (AlbumArtist albumArtist in albumArtists ?? [])
        {
            albumArtist.Album = album;
            album.AlbumArtist.Add(albumArtist);
        }
        foreach (AlbumMusicGenre albumGenre in albumGenres ?? [])
            album.AlbumMusicGenre.Add(albumGenre);
        foreach (Image image in images ?? [])
            album.Images.Add(image);
        foreach (AlbumTrack albumTrack in albumTracks ?? [])
            album.AlbumTrack.Add(albumTrack);

        return album;
    }

    private static AlbumArtist BuildAlbumArtist(Guid artistId, string artistName = "Nested Artist")
    {
        Artist artist = new() { Id = artistId, Name = artistName };
        return new() { ArtistId = artistId, Artist = artist };
    }

    private static AlbumMusicGenre BuildAlbumGenre(string name)
    {
        MusicGenre genre = new() { Id = Guid.NewGuid(), Name = name };
        return new() { MusicGenreId = genre.Id, MusicGenre = genre };
    }

    private static AlbumTrack BuildAlbumTrack(Album album)
    {
        Track track = new()
        {
            Id = Guid.NewGuid(),
            Name = "Track",
            Folder = "/folder",
            Filename = "song.flac",
            FolderId = Ulid.NewUlid(),
            Duration = "180",
        };
        return new()
        {
            AlbumId = album.Id,
            Album = album,
            TrackId = track.Id,
            Track = track,
        };
    }

    [Fact]
    public void Ctor_ColorPaletteComesFromAlbum()
    {
        ColorPalette palette = new() { Cover = new() { Dominant = "#010101" } };
        Album album = BuildAlbum(colorPalette: palette);

        AlbumResponseItemDto dto = new(album);

        dto.ColorPalette.Should().NotBeNull();
        dto.ColorPalette!.Cover!.Dominant.Should().Be("#010101");
    }

    [Fact]
    public void Ctor_CoverPresent_SetsCoverUri()
    {
        Album album = BuildAlbum(cover: "/album-cover.jpg");

        AlbumResponseItemDto dto = new(album);

        dto.Cover.Should().Be("/images/music/album-cover.jpg");
    }

    [Fact]
    public void Ctor_CoverNull_CoverIsNull()
    {
        Album album = BuildAlbum(cover: null);

        AlbumResponseItemDto dto = new(album);

        dto.Cover.Should().BeNull();
    }

    [Fact]
    public void Ctor_SetsIdentityFields()
    {
        Ulid libraryId = Ulid.NewUlid();
        Album album = BuildAlbum(libraryId: libraryId);
        Guid albumId = album.Id;

        AlbumResponseItemDto dto = new(album);

        dto.Id.Should().Be(albumId);
        dto.LibraryId.Should().Be(libraryId);
        dto.Name.Should().Be("Test Album");
        dto.Disambiguation.Should().Be("Deluxe");
        dto.Description.Should().Be("Album description");
        dto.Type.Should().Be("album");
        dto.Link.ToString().Should().Be($"/music/albums/{albumId}");
    }

    [Fact]
    public void Ctor_FavoriteTrue_WhenAlbumUserHasEntries()
    {
        AlbumUser albumUser = new(Guid.NewGuid(), Guid.NewGuid());
        Album album = BuildAlbum(albumUsers: [albumUser]);

        AlbumResponseItemDto dto = new(album);

        dto.Favorite.Should().BeTrue();
    }

    [Fact]
    public void Ctor_FavoriteFalse_WhenNoAlbumUserEntries()
    {
        Album album = BuildAlbum(albumUsers: []);

        AlbumResponseItemDto dto = new(album);

        dto.Favorite.Should().BeFalse();
    }

    [Fact]
    public void Ctor_ArtistsDeduplicatedByArtistId()
    {
        Guid sharedArtistId = Guid.NewGuid();
        AlbumArtist duplicateOne = BuildAlbumArtist(sharedArtistId);
        AlbumArtist duplicateTwo = BuildAlbumArtist(sharedArtistId);
        AlbumArtist distinctOther = BuildAlbumArtist(Guid.NewGuid());
        Album album = BuildAlbum(albumArtists: [duplicateOne, duplicateTwo, distinctOther]);

        AlbumResponseItemDto dto = new(album);

        dto.Artists.Should().HaveCount(2);
    }

    [Fact]
    public void Ctor_GenresMappedFromAlbumMusicGenre()
    {
        AlbumMusicGenre genreOne = BuildAlbumGenre("Rock");
        AlbumMusicGenre genreTwo = BuildAlbumGenre("Pop");
        Album album = BuildAlbum(albumGenres: [genreOne, genreTwo]);

        AlbumResponseItemDto dto = new(album);

        dto.Genres.Select(genre => genre.Name).Should().BeEquivalentTo(["Rock", "Pop"]);
    }

    [Fact]
    public void Ctor_ImagesMappedFromAlbumImages()
    {
        Image image = new()
        {
            Type = "poster",
            FilePath = "/poster.jpg",
            Width = 500,
            Height = 750,
        };
        Album album = BuildAlbum(images: [image]);

        AlbumResponseItemDto dto = new(album);

        dto.Images.Should().ContainSingle();
        dto.Images.First().Type.Should().Be("poster");
        dto.Images.First().Width.Should().Be(500);
    }

    [Fact]
    public void Ctor_TracksMappedFromAlbumTrack()
    {
        Album album = BuildAlbum();
        AlbumTrack trackOne = BuildAlbumTrack(album);
        AlbumTrack trackTwo = BuildAlbumTrack(album);
        album.AlbumTrack.Add(trackOne);
        album.AlbumTrack.Add(trackTwo);

        AlbumResponseItemDto dto = new(album);

        dto.Tracks.Should().HaveCount(2);
    }
}
