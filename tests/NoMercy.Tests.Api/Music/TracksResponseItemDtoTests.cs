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
using NoMercy.Database.Models.Music;
using Xunit;

namespace NoMercy.Tests.Api.Music;

[Trait("Category", "Unit")]
public class TracksResponseItemDtoTests
{
    private static Track BuildTrack(
        string? cover = "/track-cover.jpg",
        ColorPalette? colorPalette = null,
        TrackUser[]? trackUsers = null,
        int artistTrackCount = 0,
        int albumTrackCount = 0
    )
    {
        Track track = new()
        {
            Id = Guid.NewGuid(),
            Name = "Track Name",
            Folder = "/folder",
            Filename = "song.flac",
            FolderId = Ulid.NewUlid(),
            Cover = cover,
            Duration = "180",
        };
        if (colorPalette is not null)
            track.ColorPalette = colorPalette;
        foreach (TrackUser trackUser in trackUsers ?? [])
            track.TrackUser.Add(trackUser);

        for (int i = 0; i < artistTrackCount; i++)
        {
            Artist artist = new() { Id = Guid.NewGuid(), Name = $"Artist {i}" };
            track.ArtistTrack.Add(
                new()
                {
                    ArtistId = artist.Id,
                    Artist = artist,
                    Track = track,
                }
            );
        }

        for (int i = 0; i < albumTrackCount; i++)
        {
            Album album = new() { Id = Guid.NewGuid(), Name = $"Album {i}" };
            track.AlbumTrack.Add(
                new()
                {
                    AlbumId = album.Id,
                    Album = album,
                    Track = track,
                }
            );
        }

        return track;
    }

    [Fact]
    public void ParameterlessCtor_LeavesFieldsAtDefaults()
    {
        TracksResponseItemDto dto = new();

        dto.Name.Should().Be(string.Empty);
        dto.Type.Should().Be(string.Empty);
        dto.Link.Should().BeNull();
        dto.Artists.Should().BeEmpty();
        dto.Albums.Should().BeEmpty();
        dto.Tracks.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_SetsIdNameAndLink()
    {
        Track track = BuildTrack();

        TracksResponseItemDto dto = new(track, "US");

        dto.Id.Should().Be(track.Id);
        dto.Name.Should().Be("Track Name");
        dto.Link.ToString().Should().Be($"/music/tracks/{track.Id}");
    }

    [Fact]
    public void Ctor_CoverPresent_SetsCoverUri()
    {
        Track track = BuildTrack(cover: "/track-cover.jpg");

        TracksResponseItemDto dto = new(track, "US");

        dto.Cover.Should().NotBeNull();
        dto.Cover!.ToString().Should().Be("/images/music/track-cover.jpg");
    }

    [Fact]
    public void Ctor_CoverNull_CoverIsNull()
    {
        Track track = BuildTrack(cover: null);

        TracksResponseItemDto dto = new(track, "US");

        dto.Cover.Should().BeNull();
    }

    [Fact]
    public void Ctor_ColorPaletteComesFromTrack()
    {
        ColorPalette palette = new() { Cover = new() { Dominant = "#0f0f0f" } };
        Track track = BuildTrack(colorPalette: palette);

        TracksResponseItemDto dto = new(track, "US");

        dto.ColorPalette.Should().NotBeNull();
        dto.ColorPalette!.Cover!.Dominant.Should().Be("#0f0f0f");
    }

    [Fact]
    public void Ctor_TypeIsHardcodedFavorites()
    {
        Track track = BuildTrack();

        TracksResponseItemDto dto = new(track, "US");

        dto.Type.Should().Be("favorites");
    }

    [Fact]
    public void Ctor_FavoriteTrue_WhenTrackHasTrackUserEntries()
    {
        TrackUser trackUser = new(Guid.NewGuid(), Guid.NewGuid());
        Track track = BuildTrack(trackUsers: [trackUser]);

        TracksResponseItemDto dto = new(track, "US");

        dto.Favorite.Should().BeTrue();
    }

    [Fact]
    public void Ctor_FavoriteFalse_WhenNoTrackUserEntries()
    {
        Track track = BuildTrack(trackUsers: []);

        TracksResponseItemDto dto = new(track, "US");

        dto.Favorite.Should().BeFalse();
    }

    [Fact]
    public void Ctor_ArtistsMappedFromArtistTrack()
    {
        Track track = BuildTrack(artistTrackCount: 3);

        TracksResponseItemDto dto = new(track, "US");

        dto.Artists.Should().HaveCount(3);
    }

    [Fact]
    public void Ctor_AlbumsMappedFromAlbumTrack()
    {
        Track track = BuildTrack(albumTrackCount: 2);

        TracksResponseItemDto dto = new(track, "US");

        dto.Albums.Should().HaveCount(2);
    }

    [Fact]
    public void Ctor_TracksMappedFromArtistTrackAsWell()
    {
        Track track = BuildTrack(artistTrackCount: 2);

        TracksResponseItemDto dto = new(track, "US");

        dto.Tracks.Should().HaveCount(2);
    }
}
