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
using NoMercy.Database.Models.Music;
using Xunit;

namespace NoMercy.Tests.Api.Music;

[Trait("Category", "Unit")]
public class GenreResponseItemDtoTests
{
    private static MusicGenre BuildGenre(string name, MusicGenreTrack[]? tracks = null)
    {
        MusicGenre genre = new() { Id = Guid.NewGuid(), Name = name };
        foreach (MusicGenreTrack track in tracks ?? [])
            genre.MusicGenreTracks.Add(track);
        return genre;
    }

    private static MusicGenreTrack BuildGenreTrack(Guid genreId, int discNumber, int trackNumber)
    {
        Track track = new()
        {
            Id = Guid.NewGuid(),
            Name = "Track",
            Folder = "/folder",
            Filename = "song.flac",
            FolderId = Ulid.NewUlid(),
            DiscNumber = discNumber,
            TrackNumber = trackNumber,
            Duration = "200",
        };
        return new()
        {
            GenreId = genreId,
            TrackId = track.Id,
            Track = track,
        };
    }

    [Fact]
    public void Ctor_NameIsTitleCased()
    {
        MusicGenre genre = BuildGenre("test genre");

        GenreResponseItemDto dto = new(genre);

        dto.Name.Should().Be("Test Genre");
    }

    [Fact]
    public void Ctor_IdMatchesGenreId()
    {
        MusicGenre genre = BuildGenre("rock");

        GenreResponseItemDto dto = new(genre);

        dto.Id.Should().Be(genre.Id);
    }

    [Fact]
    public void Ctor_LinkUsesGenreId()
    {
        MusicGenre genre = BuildGenre("rock");

        GenreResponseItemDto dto = new(genre);

        dto.Link.ToString().Should().Be($"/music/genres/{genre.Id}");
    }

    [Fact]
    public void Ctor_TypeIsGenre()
    {
        MusicGenre genre = BuildGenre("rock");

        GenreResponseItemDto dto = new(genre);

        dto.Type.Should().Be("genre");
    }

    [Fact]
    public void Ctor_TracksOrderedByDiscThenByTrackNumber()
    {
        MusicGenre genre = BuildGenre("rock");
        Guid genreId = genre.Id;
        MusicGenreTrack discTwoTrackOne = BuildGenreTrack(genreId, 2, 1);
        MusicGenreTrack discOneTrackTwo = BuildGenreTrack(genreId, 1, 2);
        MusicGenreTrack discOneTrackOne = BuildGenreTrack(genreId, 1, 1);
        genre.MusicGenreTracks.Add(discTwoTrackOne);
        genre.MusicGenreTracks.Add(discOneTrackTwo);
        genre.MusicGenreTracks.Add(discOneTrackOne);

        GenreResponseItemDto dto = new(genre);

        dto.Tracks.Select(track => (track.Disc, track.Track))
            .Should()
            .Equal((1, 1), (1, 2), (2, 1));
    }
}
