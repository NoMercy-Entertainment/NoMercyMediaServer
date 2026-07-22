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

[Trait(name: "Category", value: "Unit")]
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
            Date = new(year: 2020, month: 5, day: 1),
            DiscNumber = 2,
            TrackNumber = 7,
            Duration = "215",
            Quality = 320,
        };
        if (lyrics is not null)
            track.Lyrics = lyrics;
        foreach (TrackUser trackUser in trackUsers ?? [])
            track.TrackUser.Add(item: trackUser);
        foreach (ArtistTrack artistTrack in artistTracks ?? [])
            track.ArtistTrack.Add(item: artistTrack);
        foreach (AlbumTrack albumTrack in albumTracks ?? [])
            track.AlbumTrack.Add(item: albumTrack);
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
        Album album = BuildAlbum(id: albumId, cover: albumCover, colorPalette: albumColorPalette);
        Track track = BuildTrack(id: trackId, trackUsers: trackUsers, artistTracks: artistTracks, albumTracks: nestedAlbumTracks, lyrics: lyrics);

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

        AlbumTrackDto dto = new(albumTrack: subject, country: "US");

        dto.Id.Should().Be(expected: subject.Track.Id);
        dto.Name.Should().Be(expected: "Track Name");
    }

    [Fact]
    public void Ctor_AlbumCoverPresent_SetsCoverUri()
    {
        AlbumTrack subject = BuildSubject(albumCover: "/album-cover.jpg");

        AlbumTrackDto dto = new(albumTrack: subject, country: "US");

        dto.Cover.Should().Be(expected: "/images/music/album-cover.jpg");
    }

    [Fact]
    public void Ctor_AlbumCoverNull_CoverIsNull()
    {
        AlbumTrack subject = BuildSubject(albumCover: null);

        AlbumTrackDto dto = new(albumTrack: subject, country: "US");

        dto.Cover.Should().BeNull();
    }

    [Fact]
    public void Ctor_PathBuiltFromFolderIdFolderAndFilename()
    {
        AlbumTrack subject = BuildSubject();

        AlbumTrackDto dto = new(albumTrack: subject, country: "US");

        dto.Path.Should()
            .Be(expected: $"/{subject.Track.FolderId}{subject.Track.Folder}{subject.Track.Filename}");
    }

    [Fact]
    public void Ctor_TypeIsTrack()
    {
        AlbumTrack subject = BuildSubject();

        AlbumTrackDto dto = new(albumTrack: subject, country: "US");

        dto.Type.Should().Be(expected: "track");
    }

    [Fact]
    public void Ctor_ColorPaletteComesFromAlbum()
    {
        ColorPalette palette = new() { Cover = new() { Dominant = "#654321" } };
        AlbumTrack subject = BuildSubject(albumColorPalette: palette);

        AlbumTrackDto dto = new(albumTrack: subject, country: "US");

        dto.ColorPalette.Should().NotBeNull();
        dto.ColorPalette!.Cover!.Dominant.Should().Be(expected: "#654321");
    }

    [Fact]
    public void Ctor_DateDiscDurationQualityTrackMappedFromTrack()
    {
        AlbumTrack subject = BuildSubject();

        AlbumTrackDto dto = new(albumTrack: subject, country: "US");

        dto.Date.Should().Be(expected: subject.Track.Date);
        dto.Disc.Should().Be(expected: 2);
        dto.Duration.Should().Be(expected: "215");
        dto.Quality.Should().Be(expected: 320);
        dto.Track.Should().Be(expected: 7);
    }

    [Fact]
    public void Ctor_FavoriteTrue_WhenTrackHasTrackUserEntries()
    {
        TrackUser trackUser = new(trackId: Guid.NewGuid(), userId: Guid.NewGuid());
        AlbumTrack subject = BuildSubject(trackUsers: [trackUser]);

        AlbumTrackDto dto = new(albumTrack: subject, country: "US");

        dto.Favorite.Should().BeTrue();
    }

    [Fact]
    public void Ctor_FavoriteFalse_WhenNoTrackUserEntries()
    {
        AlbumTrack subject = BuildSubject(trackUsers: []);

        AlbumTrackDto dto = new(albumTrack: subject, country: "US");

        dto.Favorite.Should().BeFalse();
    }

    [Fact]
    public void Ctor_LyricsPassedThroughFromTrack()
    {
        Lyric[] lyrics = [new() { Text = "La la la" }];
        AlbumTrack subject = BuildSubject(lyrics: lyrics);

        AlbumTrackDto dto = new(albumTrack: subject, country: "US");

        dto.Lyrics.Should().NotBeNull();
        dto.Lyrics!.Should().ContainSingle(predicate: lyric => lyric.Text == "La la la");
    }

    [Fact]
    public void Ctor_LinkUsesTrackId()
    {
        AlbumTrack subject = BuildSubject();

        AlbumTrackDto dto = new(albumTrack: subject, country: "US");

        dto.Link.ToString().Should().Be(expected: $"/music/tracks/{subject.Track.Id}");
    }

    [Fact]
    public void Ctor_ArtistsMappedFromTrackArtistTrack()
    {
        ArtistTrack artistTrackOne = BuildArtistTrack();
        ArtistTrack artistTrackTwo = BuildArtistTrack();
        AlbumTrack subject = BuildSubject(artistTracks: [artistTrackOne, artistTrackTwo]);

        AlbumTrackDto dto = new(albumTrack: subject, country: "US");

        dto.Artists.Should().HaveCount(expected: 2);
    }

    [Fact]
    public void Ctor_AlbumsMappedFromTrackAlbumTrack()
    {
        AlbumTrack nestedOne = BuildNestedAlbumTrack();
        AlbumTrack nestedTwo = BuildNestedAlbumTrack();
        AlbumTrack subject = BuildSubject(nestedAlbumTracks: [nestedOne, nestedTwo]);

        AlbumTrackDto dto = new(albumTrack: subject, country: "US");

        dto.Albums.Should().HaveCount(expected: 2);
    }
}
