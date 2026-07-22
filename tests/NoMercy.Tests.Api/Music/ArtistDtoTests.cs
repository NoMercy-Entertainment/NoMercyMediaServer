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
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;
using Xunit;

namespace NoMercy.Tests.Api.Music;

[Trait(name: "Category", value: "Unit")]
public class ArtistDtoTests
{
    private static AlbumArtist BuildAlbumArtist(
        string artistDescription = "The artist's own bio",
        string albumDescription = "A completely different album blurb",
        Translation[]? translations = null,
        Image[]? images = null,
        string? cover = null
    )
    {
        Guid artistId = Guid.NewGuid();
        Artist artist = new()
        {
            Id = artistId,
            Name = "Test Artist",
            Description = artistDescription,
            Disambiguation = "The Band",
            Cover = cover,
        };
        foreach (Translation translation in translations ?? [])
            artist.Translations.Add(item: translation);
        foreach (Image image in images ?? [])
            artist.Images.Add(item: image);

        Album album = new()
        {
            Id = Guid.NewGuid(),
            Name = "Unrelated Album",
            Description = albumDescription,
        };

        return new()
        {
            ArtistId = artistId,
            Artist = artist,
            AlbumId = album.Id,
            Album = album,
        };
    }

    private static ArtistTrack BuildArtistTrack(
        string artistDescription = "The artist's own bio",
        Translation[]? translations = null,
        Image[]? images = null,
        string? cover = null
    )
    {
        Guid artistId = Guid.NewGuid();
        Artist artist = new()
        {
            Id = artistId,
            Name = "Test Artist",
            Description = artistDescription,
            Disambiguation = "The Band",
            Cover = cover,
        };
        foreach (Translation translation in translations ?? [])
            artist.Translations.Add(item: translation);
        foreach (Image image in images ?? [])
            artist.Images.Add(item: image);

        Track track = new()
        {
            Id = Guid.NewGuid(),
            Name = "Song",
            Folder = "/folder",
            Filename = "song.flac",
            FolderId = Ulid.NewUlid(),
        };

        return new()
        {
            ArtistId = artistId,
            Artist = artist,
            Track = track,
        };
    }

    // =========================================================================
    // Regression: Description must fall back to the ARTIST's own description,
    // never the unrelated ALBUM's description (the AlbumArtist ctor used to
    // compute Description correctly, then immediately overwrite it with a
    // fallback to albumArtist.Album.Description).
    // =========================================================================

    [Fact]
    public void Ctor_AlbumArtist_NoTranslationMatch_DescriptionFallsBackToArtistDescription_NotAlbumDescription()
    {
        AlbumArtist albumArtist = BuildAlbumArtist(
            artistDescription: "The artist's own bio",
            albumDescription: "A completely different album blurb"
        );

        ArtistDto dto = new(albumArtist: albumArtist, country: "NL");

        dto.Description.Should().Be(expected: "The artist's own bio");
        dto.Description.Should().NotBe(unexpected: "A completely different album blurb");
    }

    [Fact]
    public void Ctor_AlbumArtist_MatchingTranslation_DescriptionUsesTranslation_NotAlbumDescription()
    {
        AlbumArtist albumArtist = BuildAlbumArtist(
            artistDescription: "English artist bio",
            albumDescription: "Album blurb that must never leak into Description",
            translations: [new() { Iso31661 = "NL", Description = "Vertaalde artiest bio" }]
        );

        ArtistDto dto = new(albumArtist: albumArtist, country: "NL");

        dto.Description.Should().Be(expected: "Vertaalde artiest bio");
    }

    [Fact]
    public void Ctor_AlbumArtist_TranslationForDifferentCountry_FallsBackToArtistDescription()
    {
        AlbumArtist albumArtist = BuildAlbumArtist(
            artistDescription: "English artist bio",
            albumDescription: "Album blurb",
            translations: [new() { Iso31661 = "DE", Description = "Deutsche Bio" }]
        );

        ArtistDto dto = new(albumArtist: albumArtist, country: "NL");

        dto.Description.Should().Be(expected: "English artist bio");
    }

    // =========================================================================
    // AlbumArtist ctor: remaining fields
    // =========================================================================

    [Fact]
    public void Ctor_AlbumArtist_SetsIdNameDisambiguationLinkAndType()
    {
        AlbumArtist albumArtist = BuildAlbumArtist();
        Guid artistId = albumArtist.ArtistId;

        ArtistDto dto = new(albumArtist: albumArtist, country: "US");

        dto.Id.Should().Be(expected: artistId);
        dto.Name.Should().Be(expected: "Test Artist");
        dto.Disambiguation.Should().Be(expected: "The Band");
        dto.Type.Should().Be(expected: "artist");
        dto.Link.ToString().Should().Be(expected: $"/music/artists/{artistId}");
    }

    [Fact]
    public void Ctor_AlbumArtist_CoverFromArtistCover()
    {
        AlbumArtist albumArtist = BuildAlbumArtist(cover: "/artist-cover.jpg");

        ArtistDto dto = new(albumArtist: albumArtist, country: "US");

        dto.Cover.Should().Be(expected: "/images/music/artist-cover.jpg");
    }

    [Fact]
    public void Ctor_AlbumArtist_CoverNull_WhenArtistHasNoCover()
    {
        AlbumArtist albumArtist = BuildAlbumArtist(cover: null);

        ArtistDto dto = new(albumArtist: albumArtist, country: "US");

        dto.Cover.Should().BeNull();
    }

    [Fact]
    public void Ctor_AlbumArtist_BackdropFromBackgroundImage()
    {
        AlbumArtist albumArtist = BuildAlbumArtist(
            images: [new() { Type = "background", FilePath = "/bg.jpg" }]
        );

        ArtistDto dto = new(albumArtist: albumArtist, country: "US");

        dto.Backdrop.Should().Be(expected: "/images/music/bg.jpg");
    }

    [Fact]
    public void Ctor_AlbumArtist_BackdropNull_WhenNoBackgroundImage()
    {
        AlbumArtist albumArtist = BuildAlbumArtist(
            images: [new() { Type = "thumb", FilePath = "/thumb.jpg" }]
        );

        ArtistDto dto = new(albumArtist: albumArtist, country: "US");

        dto.Backdrop.Should().BeNull();
    }

    // =========================================================================
    // ArtistTrack ctor
    // =========================================================================

    [Fact]
    public void Ctor_ArtistTrack_NoTranslationMatch_DescriptionFallsBackToArtistDescription()
    {
        ArtistTrack artistTrack = BuildArtistTrack(artistDescription: "Track-side artist bio");

        ArtistDto dto = new(artistTrack: artistTrack, country: "NL");

        dto.Description.Should().Be(expected: "Track-side artist bio");
    }

    [Fact]
    public void Ctor_ArtistTrack_SetsIdNameLinkAndType()
    {
        ArtistTrack artistTrack = BuildArtistTrack();
        Guid artistId = artistTrack.ArtistId;

        ArtistDto dto = new(artistTrack: artistTrack, country: "US");

        dto.Id.Should().Be(expected: artistId);
        dto.Name.Should().Be(expected: "Test Artist");
        dto.Type.Should().Be(expected: "artist");
        dto.Link.ToString().Should().Be(expected: $"/music/artists/{artistId}");
    }

    // =========================================================================
    // ForBroadcastQueueEntry: nulls out ColorPalette + Description without
    // mutating the source DTO.
    // =========================================================================

    [Fact]
    public void ForBroadcastQueueEntry_NullsDescriptionAndColorPalette_LeavesSourceUntouched()
    {
        AlbumArtist albumArtist = BuildAlbumArtist(artistDescription: "Full bio");
        ArtistDto original = new(albumArtist: albumArtist, country: "US");

        ArtistDto broadcastCopy = original.ForBroadcastQueueEntry();

        broadcastCopy.Description.Should().BeNull();
        broadcastCopy.ColorPalette.Should().BeNull();
        broadcastCopy.Id.Should().Be(expected: original.Id);
        broadcastCopy.Name.Should().Be(expected: original.Name);

        original.Description.Should().Be(expected: "Full bio");
    }
}
