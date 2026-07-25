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

[Trait("Category", "Unit")]
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
            album.Translations.Add(translation);
        foreach (Image image in images ?? [])
            album.Images.Add(image);
        if (colorPalette is not null)
            album.ColorPalette = colorPalette;
        foreach (AlbumTrack albumTrack in albumTracks ?? [])
            album.AlbumTrack.Add(albumTrack);

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

        AlbumsResponseItemDto dto = new(album, "NL");

        dto.Description.Should().Be("Nederlandse beschrijving");
    }

    [Fact]
    public void Ctor_NoTranslationForCountry_FallsBackToAlbumDescription()
    {
        Album album = BuildAlbum(
            translations: [new() { Iso31661 = "DE", Description = "Deutsche Beschreibung" }],
            description: "English description"
        );

        AlbumsResponseItemDto dto = new(album, "NL");

        dto.Description.Should().Be("English description");
    }

    [Fact]
    public void Ctor_TranslationDescriptionEmpty_FallsBackToAlbumDescription()
    {
        Album album = BuildAlbum(
            translations: [new() { Iso31661 = "NL", Description = "" }],
            description: "English description"
        );

        AlbumsResponseItemDto dto = new(album, "NL");

        dto.Description.Should().Be("English description");
    }

    [Fact]
    public void Ctor_BackgroundImagePresent_SetsBackdropUri()
    {
        Album album = BuildAlbum(images: [new() { Type = "background", FilePath = "/bg.jpg" }]);

        AlbumsResponseItemDto dto = new(album);

        dto.Backdrop.Should().Be("/images/music/bg.jpg");
    }

    [Fact]
    public void Ctor_NoBackgroundImage_BackdropIsNull()
    {
        Album album = BuildAlbum(images: [new() { Type = "poster", FilePath = "/poster.jpg" }]);

        AlbumsResponseItemDto dto = new(album);

        dto.Backdrop.Should().BeNull();
    }

    [Fact]
    public void Ctor_CoverPresent_SetsCoverUri()
    {
        Album album = BuildAlbum(cover: "/cover.jpg");

        AlbumsResponseItemDto dto = new(album);

        dto.Cover.Should().Be("/images/music/cover.jpg");
    }

    [Fact]
    public void Ctor_CoverNullOrEmpty_CoverIsNull()
    {
        Album album = BuildAlbum(cover: null);

        AlbumsResponseItemDto dto = new(album);

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

        AlbumsResponseItemDto dto = new(album);

        dto.ColorPalette.Should().NotBeNull();
        dto.ColorPalette!.Backdrop.Should().NotBeNull();
        dto.ColorPalette.Backdrop!.Dominant.Should().Be("#123456");
    }

    [Fact]
    public void Ctor_ColorPaletteNull_DoesNotThrowAndStaysNull()
    {
        Album album = BuildAlbum(colorPalette: null);

        AlbumsResponseItemDto dto = new(album);

        dto.ColorPalette.Should().BeNull();
    }

    [Fact]
    public void Ctor_TracksCount_OnlyCountsTracksWithNonNullDuration()
    {
        Guid albumId = Guid.NewGuid();
        AlbumTrack withDuration = BuildAlbumTrack(albumId, "180");
        AlbumTrack withoutDuration = BuildAlbumTrack(albumId, null);
        Album album = BuildAlbum(albumTracks: [withDuration, withoutDuration]);
        album.Id = albumId;

        AlbumsResponseItemDto dto = new(album);

        dto.Tracks.Should().Be(1);
    }

    [Fact]
    public void Ctor_SetsIdNameTypeLinkDisambiguationFolder()
    {
        Album album = BuildAlbum();
        Guid albumId = album.Id;

        AlbumsResponseItemDto dto = new(album);

        dto.Id.Should().Be(albumId);
        dto.Name.Should().Be("Test Album");
        dto.Type.Should().Be("album");
        dto.Link.ToString().Should().Be($"/music/albums/{albumId}");
        dto.Disambiguation.Should().Be("Deluxe Edition");
        dto.Folder.Should().Be("/music/test-album");
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

        AlbumsResponseItemDto dto = new(card);

        dto.Description.Should().Be("Vertaalde beschrijving");
    }

    [Fact]
    public void CardCtor_TranslatedDescriptionAbsent_FallsBackToDescription()
    {
        AlbumCardDto card = BuildCard(
            translatedDescription: null,
            description: "Original description"
        );

        AlbumsResponseItemDto dto = new(card);

        dto.Description.Should().Be("Original description");
    }

    [Fact]
    public void CardCtor_BackgroundImagePathPresent_SetsBackdrop()
    {
        AlbumCardDto card = BuildCard(backgroundImagePath: "/bg-card.jpg");

        AlbumsResponseItemDto dto = new(card);

        dto.Backdrop.Should().Be("/images/music/bg-card.jpg");
    }

    [Fact]
    public void CardCtor_BackgroundImagePathAbsent_BackdropIsNull()
    {
        AlbumCardDto card = BuildCard(backgroundImagePath: null);

        AlbumsResponseItemDto dto = new(card);

        dto.Backdrop.Should().BeNull();
    }

    [Fact]
    public void CardCtor_CoverPresent_SetsCoverUri()
    {
        AlbumCardDto card = BuildCard(cover: "/card-cover.jpg");

        AlbumsResponseItemDto dto = new(card);

        dto.Cover.Should().Be("/images/music/card-cover.jpg");
    }

    [Fact]
    public void CardCtor_CoverAbsent_CoverIsNull()
    {
        AlbumCardDto card = BuildCard(cover: null);

        AlbumsResponseItemDto dto = new(card);

        dto.Cover.Should().BeNull();
    }

    [Fact]
    public void CardCtor_ColorPaletteJsonPresent_SetsBackdropFromBackgroundPaletteJson()
    {
        AlbumCardDto card = BuildCard(
            colorPaletteJson: """{"cover":{"dominant":"#abcdef"}}""",
            backgroundImageColorPaletteJson: """{"image":{"dominant":"#00ff00"}}"""
        );

        AlbumsResponseItemDto dto = new(card);

        dto.ColorPalette.Should().NotBeNull();
        dto.ColorPalette!.Backdrop.Should().NotBeNull();
        dto.ColorPalette.Backdrop!.Dominant.Should().Be("#00ff00");
    }

    [Fact]
    public void CardCtor_ColorPaletteJsonNull_ColorPaletteIsNull()
    {
        AlbumCardDto card = BuildCard(colorPaletteJson: null);

        AlbumsResponseItemDto dto = new(card);

        dto.ColorPalette.Should().BeNull();
    }

    [Fact]
    public void CardCtor_TracksUsesTrackCount()
    {
        AlbumCardDto card = BuildCard();

        AlbumsResponseItemDto dto = new(card);

        dto.Tracks.Should().Be(11);
    }

    [Fact]
    public void CardCtor_SetsFieldsCorrectly()
    {
        AlbumCardDto card = BuildCard();
        Guid cardId = card.Id;

        AlbumsResponseItemDto dto = new(card);

        dto.Id.Should().Be(cardId);
        dto.Name.Should().Be("Card Album");
        dto.Type.Should().Be("album");
        dto.Link.ToString().Should().Be($"/music/albums/{cardId}");
        dto.Disambiguation.Should().Be("Special Edition");
        dto.Folder.Should().Be("/music/card-album");
    }
}
