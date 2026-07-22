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

[Trait(name: "Category", value: "Unit")]
public class GenreResponseItemDtoTests
{
    private static MusicGenre BuildGenre(string name, MusicGenreTrack[]? tracks = null)
    {
        MusicGenre genre = new() { Id = Guid.NewGuid(), Name = name };
        foreach (MusicGenreTrack track in tracks ?? [])
            genre.MusicGenreTracks.Add(item: track);
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
        MusicGenre genre = BuildGenre(name: "test genre");

        GenreResponseItemDto dto = new(genre: genre);

        dto.Name.Should().Be(expected: "Test Genre");
    }

    [Fact]
    public void Ctor_IdMatchesGenreId()
    {
        MusicGenre genre = BuildGenre(name: "rock");

        GenreResponseItemDto dto = new(genre: genre);

        dto.Id.Should().Be(expected: genre.Id);
    }

    [Fact]
    public void Ctor_LinkUsesGenreId()
    {
        MusicGenre genre = BuildGenre(name: "rock");

        GenreResponseItemDto dto = new(genre: genre);

        dto.Link.ToString().Should().Be(expected: $"/music/genres/{genre.Id}");
    }

    [Fact]
    public void Ctor_TypeIsGenre()
    {
        MusicGenre genre = BuildGenre(name: "rock");

        GenreResponseItemDto dto = new(genre: genre);

        dto.Type.Should().Be(expected: "genre");
    }

    [Fact]
    public void Ctor_TracksOrderedByDiscThenByTrackNumber()
    {
        MusicGenre genre = BuildGenre(name: "rock");
        Guid genreId = genre.Id;
        MusicGenreTrack discTwoTrackOne = BuildGenreTrack(genreId: genreId, discNumber: 2, trackNumber: 1);
        MusicGenreTrack discOneTrackTwo = BuildGenreTrack(genreId: genreId, discNumber: 1, trackNumber: 2);
        MusicGenreTrack discOneTrackOne = BuildGenreTrack(genreId: genreId, discNumber: 1, trackNumber: 1);
        genre.MusicGenreTracks.Add(item: discTwoTrackOne);
        genre.MusicGenreTracks.Add(item: discOneTrackTwo);
        genre.MusicGenreTracks.Add(item: discOneTrackOne);

        GenreResponseItemDto dto = new(genre: genre);

        dto.Tracks.Select(selector: track => (track.Disc, track.Track))
            .Should()
            .Equal(elements: [(1, 1), (1, 2), (2, 1)]);
    }
}
