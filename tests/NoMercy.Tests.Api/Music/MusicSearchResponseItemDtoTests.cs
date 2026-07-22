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

[Trait(name: "Category", value: "Unit")]
public class MusicSearchResponseItemDtoTests
{
    private static Artist BuildArtist(
        string? cover = "/artist-cover.jpg",
        Image[]? images = null,
        ArtistTrack[]? artistTracks = null,
        ColorPalette? colorPalette = null
    )
    {
        Artist artist = new()
        {
            Id = Guid.NewGuid(),
            Name = "Search Artist",
            Description = "Artist description",
            Disambiguation = "The Band",
            Cover = cover,
        };
        foreach (Image image in images ?? [])
            artist.Images.Add(item: image);
        foreach (ArtistTrack artistTrack in artistTracks ?? [])
            artist.ArtistTrack.Add(item: artistTrack);
        if (colorPalette is not null)
            artist.ColorPalette = colorPalette;

        return artist;
    }

    private static Album BuildAlbum(
        string? cover = "/album-cover.jpg",
        Image[]? images = null,
        AlbumTrack[]? albumTracks = null,
        ColorPalette? colorPalette = null
    )
    {
        Album album = new()
        {
            Id = Guid.NewGuid(),
            Name = "Search Album",
            Description = "Album description",
            Disambiguation = "Deluxe",
            Cover = cover,
        };
        foreach (Image image in images ?? [])
            album.Images.Add(item: image);
        foreach (AlbumTrack albumTrack in albumTracks ?? [])
            album.AlbumTrack.Add(item: albumTrack);
        if (colorPalette is not null)
            album.ColorPalette = colorPalette;

        return album;
    }

    private static ArtistTrack BuildArtistTrack(Guid artistId)
    {
        Track track = new() { Id = Guid.NewGuid(), Name = "Track" };
        return new() { ArtistId = artistId, Track = track };
    }

    private static AlbumTrack BuildAlbumTrack(Guid albumId)
    {
        Track track = new() { Id = Guid.NewGuid(), Name = "Track" };
        return new() { AlbumId = albumId, Track = track };
    }

    [Fact]
    public void ArtistCtor_CoverPresent_UsesArtistCover()
    {
        Artist artist = BuildArtist(cover: "/artist-cover.jpg");

        MusicSearchResponseItemDto dto = new(artist: artist);

        dto.Cover.Should().Be(expected: "/images/music/artist-cover.jpg");
    }

    [Fact]
    public void ArtistCtor_CoverNull_FallsBackToFirstImageFilePath()
    {
        Artist artist = BuildArtist(
            cover: null,
            images: [new() { Type = "thumb", FilePath = "/first-artist-image.jpg" }]
        );

        MusicSearchResponseItemDto dto = new(artist: artist);

        dto.Cover.Should().Be(expected: "/images/music/first-artist-image.jpg");
    }

    [Fact]
    public void ArtistCtor_CoverAndImagesEmpty_CoverIsNull()
    {
        Artist artist = BuildArtist(cover: null, images: []);

        MusicSearchResponseItemDto dto = new(artist: artist);

        dto.Cover.Should().BeNull();
    }

    [Fact]
    public void ArtistCtor_TracksCountsArtistTrackEntries()
    {
        Artist artist = BuildArtist();
        Guid artistId = artist.Id;
        artist.ArtistTrack.Add(item: BuildArtistTrack(artistId: artistId));
        artist.ArtistTrack.Add(item: BuildArtistTrack(artistId: artistId));

        MusicSearchResponseItemDto dto = new(artist: artist);

        dto.Tracks.Should().Be(expected: 2);
    }

    [Fact]
    public void ArtistCtor_SetsFieldsCorrectly()
    {
        ColorPalette palette = new() { Cover = new() { Dominant = "#345678" } };
        Artist artist = BuildArtist(colorPalette: palette);
        Guid artistId = artist.Id;

        MusicSearchResponseItemDto dto = new(artist: artist);

        dto.Id.Should().Be(expected: artistId);
        dto.Name.Should().Be(expected: "Search Artist");
        dto.Disambiguation.Should().Be(expected: "The Band");
        dto.Description.Should().Be(expected: "Artist description");
        dto.Type.Should().Be(expected: "artist");
        dto.Link.ToString().Should().Be(expected: $"/music/artists/{artistId}");
        dto.ColorPalette.Should().NotBeNull();
        dto.ColorPalette!.Cover!.Dominant.Should().Be(expected: "#345678");
    }

    [Fact]
    public void AlbumCtor_CoverPresent_UsesAlbumCover()
    {
        Album album = BuildAlbum(cover: "/album-cover.jpg");

        MusicSearchResponseItemDto dto = new(album: album);

        dto.Cover.Should().Be(expected: "/images/music/album-cover.jpg");
    }

    [Fact]
    public void AlbumCtor_CoverNull_FallsBackToFirstImageFilePath()
    {
        Album album = BuildAlbum(
            cover: null,
            images: [new() { Type = "background", FilePath = "/first-album-image.jpg" }]
        );

        MusicSearchResponseItemDto dto = new(album: album);

        dto.Cover.Should().Be(expected: "/images/music/first-album-image.jpg");
    }

    [Fact]
    public void AlbumCtor_TracksCountsAlbumTrackEntries()
    {
        Album album = BuildAlbum();
        Guid albumId = album.Id;
        album.AlbumTrack.Add(item: BuildAlbumTrack(albumId: albumId));
        album.AlbumTrack.Add(item: BuildAlbumTrack(albumId: albumId));
        album.AlbumTrack.Add(item: BuildAlbumTrack(albumId: albumId));

        MusicSearchResponseItemDto dto = new(album: album);

        dto.Tracks.Should().Be(expected: 3);
    }

    [Fact]
    public void AlbumCtor_SetsFieldsCorrectly()
    {
        ColorPalette palette = new() { Cover = new() { Dominant = "#0a0b0c" } };
        Album album = BuildAlbum(colorPalette: palette);
        Guid albumId = album.Id;

        MusicSearchResponseItemDto dto = new(album: album);

        dto.Id.Should().Be(expected: albumId);
        dto.Name.Should().Be(expected: "Search Album");
        dto.Disambiguation.Should().Be(expected: "Deluxe");
        dto.Description.Should().Be(expected: "Album description");
        dto.Type.Should().Be(expected: "album");
        dto.Link.ToString().Should().Be(expected: $"/music/albums/{albumId}");
        dto.ColorPalette.Should().NotBeNull();
        dto.ColorPalette!.Cover!.Dominant.Should().Be(expected: "#0a0b0c");
    }
}
