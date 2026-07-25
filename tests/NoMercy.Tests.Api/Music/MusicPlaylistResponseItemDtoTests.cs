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
public class MusicPlaylistResponseItemDtoTests
{
    private static Playlist BuildPlaylist(
        string? cover = "/playlist-cover.jpg",
        ColorPalette? colorPalette = null,
        PlaylistTrack[]? tracks = null,
        DateTime? createdAt = null,
        DateTime? updatedAt = null
    )
    {
        Playlist playlist = new()
        {
            Id = Guid.NewGuid(),
            Name = "Road Trip",
            Description = "Songs for the road",
            Cover = cover,
            CreatedAt = createdAt ?? new(2023, 1, 1),
            UpdatedAt = updatedAt ?? new(2024, 6, 15),
        };
        if (colorPalette is not null)
            playlist.ColorPalette = colorPalette;
        foreach (PlaylistTrack track in tracks ?? [])
            playlist.Tracks.Add(track);

        return playlist;
    }

    [Fact]
    public void Ctor_SetsIdNameAndDescription()
    {
        Playlist playlist = BuildPlaylist();

        MusicPlaylistResponseItemDto dto = new(playlist);

        dto.Id.Should().Be(playlist.Id);
        dto.Name.Should().Be("Road Trip");
        dto.Description.Should().Be("Songs for the road");
    }

    [Fact]
    public void Ctor_CoverPresent_SetsCoverUri()
    {
        Playlist playlist = BuildPlaylist(cover: "/playlist-cover.jpg");

        MusicPlaylistResponseItemDto dto = new(playlist);

        dto.Cover.Should().Be("/images/music/playlist-cover.jpg");
    }

    [Fact]
    public void Ctor_CoverNull_CoverIsNull()
    {
        Playlist playlist = BuildPlaylist(cover: null);

        MusicPlaylistResponseItemDto dto = new(playlist);

        dto.Cover.Should().BeNull();
    }

    [Fact]
    public void Ctor_ColorPaletteComesFromPlaylist()
    {
        ColorPalette palette = new() { Cover = new() { Dominant = "#abc123" } };
        Playlist playlist = BuildPlaylist(colorPalette: palette);

        MusicPlaylistResponseItemDto dto = new(playlist);

        dto.ColorPalette.Should().NotBeNull();
        dto.ColorPalette!.Cover!.Dominant.Should().Be("#abc123");
    }

    [Fact]
    public void Ctor_CreatedAtMappedFromPlaylist()
    {
        DateTime createdAt = new(2022, 3, 4);
        Playlist playlist = BuildPlaylist(createdAt: createdAt);

        MusicPlaylistResponseItemDto dto = new(playlist);

        dto.CreatedAt.Should().Be(createdAt);
    }

    // Regression test: the constructor previously left UpdatedAt at its
    // default(DateTime) — every playlist response reported "0001-01-01" for
    // updated_at regardless of the real last-modified time. Fixed in
    // MusicPlaylistResponseItemDto's constructor by mapping playlist.UpdatedAt.
    [Fact]
    public void Ctor_UpdatedAtMappedFromPlaylist()
    {
        DateTime updatedAt = new(2024, 6, 15, 8, 30, 0);
        Playlist playlist = BuildPlaylist(updatedAt: updatedAt);

        MusicPlaylistResponseItemDto dto = new(playlist);

        dto.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void Ctor_TracksPassedThroughFromPlaylist()
    {
        Track track = new() { Id = Guid.NewGuid(), Name = "Track" };
        PlaylistTrack playlistTrack = new() { TrackId = track.Id, Track = track };
        Playlist playlist = BuildPlaylist(tracks: [playlistTrack]);

        MusicPlaylistResponseItemDto dto = new(playlist);

        dto.Tracks.Should().ContainSingle().Which.Should().BeSameAs(playlistTrack);
    }

    [Fact]
    public void Ctor_TypeIsPlaylist()
    {
        Playlist playlist = BuildPlaylist();

        MusicPlaylistResponseItemDto dto = new(playlist);

        dto.Type.Should().Be("playlist");
    }

    [Fact]
    public void Ctor_LinkUsesPlaylistId()
    {
        Playlist playlist = BuildPlaylist();

        MusicPlaylistResponseItemDto dto = new(playlist);

        dto.Link.ToString().Should().Be($"/music/playlists/{playlist.Id}");
    }
}
