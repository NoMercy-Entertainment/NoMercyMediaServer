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
public class AlbumTrackDtoTests
{
    private static Album BuildAlbum(Guid id, string? cover, ColorPalette? colorPalette = null)
    {
        Album album = new()
        {
            Id = id,
            Name = "Album Name",
            Cover = cover,
        };
        if (colorPalette is not null)
            album.ColorPalette = colorPalette;
        return album;
    }

    private static Track BuildTrack(
        Guid id,
        TrackUser[]? trackUsers = null,
        ArtistTrack[]? artistTracks = null,
        AlbumTrack[]? albumTracks = null,
        Lyric[]? lyrics = null
    )
    {
        Track track = new()
        {
            Id = id,
            Name = "Track Name",
            Folder = "/music-folder",
            Filename = "song.flac",
            FolderId = Ulid.NewUlid(),
            Date = new(2020, 5, 1),
            DiscNumber = 2,
            TrackNumber = 7,
            Duration = "215",
            Quality = 320,
        };
        if (lyrics is not null)
            track.Lyrics = lyrics;
        foreach (TrackUser trackUser in trackUsers ?? [])
            track.TrackUser.Add(trackUser);
        foreach (ArtistTrack artistTrack in artistTracks ?? [])
            track.ArtistTrack.Add(artistTrack);
        foreach (AlbumTrack albumTrack in albumTracks ?? [])
            track.AlbumTrack.Add(albumTrack);
        return track;
    }

    private static AlbumTrack BuildSubject(
        string? albumCover = "/album-cover.jpg",
        ColorPalette? albumColorPalette = null,
        TrackUser[]? trackUsers = null,
        ArtistTrack[]? artistTracks = null,
        AlbumTrack[]? nestedAlbumTracks = null,
        Lyric[]? lyrics = null
    )
    {
        Guid albumId = Guid.NewGuid();
        Guid trackId = Guid.NewGuid();
        Album album = BuildAlbum(albumId, albumCover, albumColorPalette);
        Track track = BuildTrack(trackId, trackUsers, artistTracks, nestedAlbumTracks, lyrics);

        return new()
        {
            AlbumId = albumId,
            Album = album,
            TrackId = trackId,
            Track = track,
        };
    }

    private static ArtistTrack BuildArtistTrack()
    {
        Artist artist = new() { Id = Guid.NewGuid(), Name = "Nested Artist" };
        return new() { ArtistId = artist.Id, Artist = artist };
    }

    private static AlbumTrack BuildNestedAlbumTrack()
    {
        Album album = new() { Id = Guid.NewGuid(), Name = "Nested Album" };
        return new() { AlbumId = album.Id, Album = album };
    }

    [Fact]
    public void Ctor_SetsIdAndNameFromTrack()
    {
        AlbumTrack subject = BuildSubject();

        AlbumTrackDto dto = new(subject, "US");

        dto.Id.Should().Be(subject.Track.Id);
        dto.Name.Should().Be("Track Name");
    }

    [Fact]
    public void Ctor_AlbumCoverPresent_SetsCoverUri()
    {
        AlbumTrack subject = BuildSubject(albumCover: "/album-cover.jpg");

        AlbumTrackDto dto = new(subject, "US");

        dto.Cover.Should().Be("/images/music/album-cover.jpg");
    }

    [Fact]
    public void Ctor_AlbumCoverNull_CoverIsNull()
    {
        AlbumTrack subject = BuildSubject(albumCover: null);

        AlbumTrackDto dto = new(subject, "US");

        dto.Cover.Should().BeNull();
    }

    [Fact]
    public void Ctor_PathBuiltFromFolderIdFolderAndFilename()
    {
        AlbumTrack subject = BuildSubject();

        AlbumTrackDto dto = new(subject, "US");

        dto.Path.Should()
            .Be($"/{subject.Track.FolderId}{subject.Track.Folder}{subject.Track.Filename}");
    }

    [Fact]
    public void Ctor_TypeIsTrack()
    {
        AlbumTrack subject = BuildSubject();

        AlbumTrackDto dto = new(subject, "US");

        dto.Type.Should().Be("track");
    }

    [Fact]
    public void Ctor_ColorPaletteComesFromAlbum()
    {
        ColorPalette palette = new() { Cover = new() { Dominant = "#654321" } };
        AlbumTrack subject = BuildSubject(albumColorPalette: palette);

        AlbumTrackDto dto = new(subject, "US");

        dto.ColorPalette.Should().NotBeNull();
        dto.ColorPalette!.Cover!.Dominant.Should().Be("#654321");
    }

    [Fact]
    public void Ctor_DateDiscDurationQualityTrackMappedFromTrack()
    {
        AlbumTrack subject = BuildSubject();

        AlbumTrackDto dto = new(subject, "US");

        dto.Date.Should().Be(subject.Track.Date);
        dto.Disc.Should().Be(2);
        dto.Duration.Should().Be("215");
        dto.Quality.Should().Be(320);
        dto.Track.Should().Be(7);
    }

    [Fact]
    public void Ctor_FavoriteTrue_WhenTrackHasTrackUserEntries()
    {
        TrackUser trackUser = new(Guid.NewGuid(), Guid.NewGuid());
        AlbumTrack subject = BuildSubject(trackUsers: [trackUser]);

        AlbumTrackDto dto = new(subject, "US");

        dto.Favorite.Should().BeTrue();
    }

    [Fact]
    public void Ctor_FavoriteFalse_WhenNoTrackUserEntries()
    {
        AlbumTrack subject = BuildSubject(trackUsers: []);

        AlbumTrackDto dto = new(subject, "US");

        dto.Favorite.Should().BeFalse();
    }

    [Fact]
    public void Ctor_LyricsPassedThroughFromTrack()
    {
        Lyric[] lyrics = [new() { Text = "La la la" }];
        AlbumTrack subject = BuildSubject(lyrics: lyrics);

        AlbumTrackDto dto = new(subject, "US");

        dto.Lyrics.Should().NotBeNull();
        dto.Lyrics!.Should().ContainSingle(lyric => lyric.Text == "La la la");
    }

    [Fact]
    public void Ctor_LinkUsesTrackId()
    {
        AlbumTrack subject = BuildSubject();

        AlbumTrackDto dto = new(subject, "US");

        dto.Link.ToString().Should().Be($"/music/tracks/{subject.Track.Id}");
    }

    [Fact]
    public void Ctor_ArtistsMappedFromTrackArtistTrack()
    {
        ArtistTrack artistTrackOne = BuildArtistTrack();
        ArtistTrack artistTrackTwo = BuildArtistTrack();
        AlbumTrack subject = BuildSubject(artistTracks: [artistTrackOne, artistTrackTwo]);

        AlbumTrackDto dto = new(subject, "US");

        dto.Artists.Should().HaveCount(2);
    }

    [Fact]
    public void Ctor_AlbumsMappedFromTrackAlbumTrack()
    {
        AlbumTrack nestedOne = BuildNestedAlbumTrack();
        AlbumTrack nestedTwo = BuildNestedAlbumTrack();
        AlbumTrack subject = BuildSubject(nestedAlbumTracks: [nestedOne, nestedTwo]);

        AlbumTrackDto dto = new(subject, "US");

        dto.Albums.Should().HaveCount(2);
    }
}
