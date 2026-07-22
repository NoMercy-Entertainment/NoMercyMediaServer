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
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;
using Xunit;

namespace NoMercy.Tests.Api.Music;

[Trait(name: "Category", value: "Unit")]
public class AlbumsResponseItemDtoTests
{
    private static Album BuildAlbum(
        Translation[]? translations = null,
        Image[]? images = null,
        string? cover = "/cover.jpg",
        string? description = "Album description",
        ColorPalette? colorPalette = null,
        AlbumTrack[]? albumTracks = null
    )
    {
        Album album = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Album",
            Description = description,
            Cover = cover,
            Disambiguation = "Deluxe Edition",
            Folder = "/music/test-album",
        };
        foreach (Translation translation in translations ?? [])
            album.Translations.Add(item: translation);
        foreach (Image image in images ?? [])
            album.Images.Add(item: image);
        if (colorPalette is not null)
            album.ColorPalette = colorPalette;
        foreach (AlbumTrack albumTrack in albumTracks ?? [])
            album.AlbumTrack.Add(item: albumTrack);

        return album;
    }

    private static AlbumTrack BuildAlbumTrack(Guid albumId, string? duration)
    {
        Track track = new()
        {
            Id = Guid.NewGuid(),
            Name = "Track",
            Duration = duration!,
        };
        return new() { AlbumId = albumId, Track = track };
    }

    [Fact]
    public void Ctor_TranslationMatchesCountry_UsesTranslationDescription()
    {
        Album album = BuildAlbum(
            translations: [new() { Iso31661 = "NL", Description = "Nederlandse beschrijving" }],
            description: "English description"
        );

        AlbumsResponseItemDto dto = new(album: album, country: "NL");

        dto.Description.Should().Be(expected: "Nederlandse beschrijving");
    }

    [Fact]
    public void Ctor_NoTranslationForCountry_FallsBackToAlbumDescription()
    {
        Album album = BuildAlbum(
            translations: [new() { Iso31661 = "DE", Description = "Deutsche Beschreibung" }],
            description: "English description"
        );

        AlbumsResponseItemDto dto = new(album: album, country: "NL");

        dto.Description.Should().Be(expected: "English description");
    }

    [Fact]
    public void Ctor_TranslationDescriptionEmpty_FallsBackToAlbumDescription()
    {
        Album album = BuildAlbum(
            translations: [new() { Iso31661 = "NL", Description = "" }],
            description: "English description"
        );

        AlbumsResponseItemDto dto = new(album: album, country: "NL");

        dto.Description.Should().Be(expected: "English description");
    }

    [Fact]
    public void Ctor_BackgroundImagePresent_SetsBackdropUri()
    {
        Album album = BuildAlbum(images: [new() { Type = "background", FilePath = "/bg.jpg" }]);

        AlbumsResponseItemDto dto = new(album: album);

        dto.Backdrop.Should().Be(expected: "/images/music/bg.jpg");
    }

    [Fact]
    public void Ctor_NoBackgroundImage_BackdropIsNull()
    {
        Album album = BuildAlbum(images: [new() { Type = "poster", FilePath = "/poster.jpg" }]);

        AlbumsResponseItemDto dto = new(album: album);

        dto.Backdrop.Should().BeNull();
    }

    [Fact]
    public void Ctor_CoverPresent_SetsCoverUri()
    {
        Album album = BuildAlbum(cover: "/cover.jpg");

        AlbumsResponseItemDto dto = new(album: album);

        dto.Cover.Should().Be(expected: "/images/music/cover.jpg");
    }

    [Fact]
    public void Ctor_CoverNullOrEmpty_CoverIsNull()
    {
        Album album = BuildAlbum(cover: null);

        AlbumsResponseItemDto dto = new(album: album);

        dto.Cover.Should().BeNull();
    }

    [Fact]
    public void Ctor_ColorPaletteNotNull_BackdropSetFromBackgroundImagePalette()
    {
        Image background = new()
        {
            Type = "background",
            FilePath = "/bg.jpg",
            ColorPalette = new() { Image = new() { Dominant = "#123456" } },
        };
        Album album = BuildAlbum(
            images: [background],
            colorPalette: new() { Cover = new() { Dominant = "#abcdef" } }
        );

        AlbumsResponseItemDto dto = new(album: album);

        dto.ColorPalette.Should().NotBeNull();
        dto.ColorPalette!.Backdrop.Should().NotBeNull();
        dto.ColorPalette.Backdrop!.Dominant.Should().Be(expected: "#123456");
    }

    [Fact]
    public void Ctor_ColorPaletteNull_DoesNotThrowAndStaysNull()
    {
        Album album = BuildAlbum(colorPalette: null);

        AlbumsResponseItemDto dto = new(album: album);

        dto.ColorPalette.Should().BeNull();
    }

    [Fact]
    public void Ctor_TracksCount_OnlyCountsTracksWithNonNullDuration()
    {
        Guid albumId = Guid.NewGuid();
        AlbumTrack withDuration = BuildAlbumTrack(albumId: albumId, duration: "180");
        AlbumTrack withoutDuration = BuildAlbumTrack(albumId: albumId, duration: null);
        Album album = BuildAlbum(albumTracks: [withDuration, withoutDuration]);
        album.Id = albumId;

        AlbumsResponseItemDto dto = new(album: album);

        dto.Tracks.Should().Be(expected: 1);
    }

    [Fact]
    public void Ctor_SetsIdNameTypeLinkDisambiguationFolder()
    {
        Album album = BuildAlbum();
        Guid albumId = album.Id;

        AlbumsResponseItemDto dto = new(album: album);

        dto.Id.Should().Be(expected: albumId);
        dto.Name.Should().Be(expected: "Test Album");
        dto.Type.Should().Be(expected: "album");
        dto.Link.ToString().Should().Be(expected: $"/music/albums/{albumId}");
        dto.Disambiguation.Should().Be(expected: "Deluxe Edition");
        dto.Folder.Should().Be(expected: "/music/test-album");
    }

    private static AlbumCardDto BuildCard(
        string? translatedDescription = null,
        string? description = "Card description",
        string? backgroundImagePath = null,
        string? cover = "/card-cover.jpg",
        string? colorPaletteJson = "",
        string? backgroundImageColorPaletteJson = null
    )
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Name = "Card Album",
            Cover = cover,
            Disambiguation = "Special Edition",
            Description = description,
            TranslatedDescription = translatedDescription,
            ColorPalette = colorPaletteJson ?? string.Empty,
            LibraryId = Ulid.NewUlid(),
            Folder = "/music/card-album",
            Year = 2020,
            TrackCount = 11,
            BackgroundImagePath = backgroundImagePath,
            BackgroundImageColorPalette = backgroundImageColorPaletteJson,
        };
    }

    [Fact]
    public void CardCtor_TranslatedDescriptionPresent_UsesIt()
    {
        AlbumCardDto card = BuildCard(
            translatedDescription: "Vertaalde beschrijving",
            description: "Original description"
        );

        AlbumsResponseItemDto dto = new(album: card);

        dto.Description.Should().Be(expected: "Vertaalde beschrijving");
    }

    [Fact]
    public void CardCtor_TranslatedDescriptionAbsent_FallsBackToDescription()
    {
        AlbumCardDto card = BuildCard(
            translatedDescription: null,
            description: "Original description"
        );

        AlbumsResponseItemDto dto = new(album: card);

        dto.Description.Should().Be(expected: "Original description");
    }

    [Fact]
    public void CardCtor_BackgroundImagePathPresent_SetsBackdrop()
    {
        AlbumCardDto card = BuildCard(backgroundImagePath: "/bg-card.jpg");

        AlbumsResponseItemDto dto = new(album: card);

        dto.Backdrop.Should().Be(expected: "/images/music/bg-card.jpg");
    }

    [Fact]
    public void CardCtor_BackgroundImagePathAbsent_BackdropIsNull()
    {
        AlbumCardDto card = BuildCard(backgroundImagePath: null);

        AlbumsResponseItemDto dto = new(album: card);

        dto.Backdrop.Should().BeNull();
    }

    [Fact]
    public void CardCtor_CoverPresent_SetsCoverUri()
    {
        AlbumCardDto card = BuildCard(cover: "/card-cover.jpg");

        AlbumsResponseItemDto dto = new(album: card);

        dto.Cover.Should().Be(expected: "/images/music/card-cover.jpg");
    }

    [Fact]
    public void CardCtor_CoverAbsent_CoverIsNull()
    {
        AlbumCardDto card = BuildCard(cover: null);

        AlbumsResponseItemDto dto = new(album: card);

        dto.Cover.Should().BeNull();
    }

    [Fact]
    public void CardCtor_ColorPaletteJsonPresent_SetsBackdropFromBackgroundPaletteJson()
    {
        AlbumCardDto card = BuildCard(
            colorPaletteJson: """{"cover":{"dominant":"#abcdef"}}""",
            backgroundImageColorPaletteJson: """{"image":{"dominant":"#00ff00"}}"""
        );

        AlbumsResponseItemDto dto = new(album: card);

        dto.ColorPalette.Should().NotBeNull();
        dto.ColorPalette!.Backdrop.Should().NotBeNull();
        dto.ColorPalette.Backdrop!.Dominant.Should().Be(expected: "#00ff00");
    }

    [Fact]
    public void CardCtor_ColorPaletteJsonNull_ColorPaletteIsNull()
    {
        AlbumCardDto card = BuildCard(colorPaletteJson: null);

        AlbumsResponseItemDto dto = new(album: card);

        dto.ColorPalette.Should().BeNull();
    }

    [Fact]
    public void CardCtor_TracksUsesTrackCount()
    {
        AlbumCardDto card = BuildCard();

        AlbumsResponseItemDto dto = new(album: card);

        dto.Tracks.Should().Be(expected: 11);
    }

    [Fact]
    public void CardCtor_SetsFieldsCorrectly()
    {
        AlbumCardDto card = BuildCard();
        Guid cardId = card.Id;

        AlbumsResponseItemDto dto = new(album: card);

        dto.Id.Should().Be(expected: cardId);
        dto.Name.Should().Be(expected: "Card Album");
        dto.Type.Should().Be(expected: "album");
        dto.Link.ToString().Should().Be(expected: $"/music/albums/{cardId}");
        dto.Disambiguation.Should().Be(expected: "Special Edition");
        dto.Folder.Should().Be(expected: "/music/card-album");
    }
}
