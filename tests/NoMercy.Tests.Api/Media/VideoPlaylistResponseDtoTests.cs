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

using NoMercy.Api.DTOs.Media;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using Xunit;

namespace NoMercy.Tests.Api.Media;

[Trait("Category", "Playlist")]
public class VideoPlaylistResponseDtoTests
{
    private static Movie BuildMovieWithCertification(string countryIso, VideoTrack[]? tracks = null)
    {
        Certification certification = new()
        {
            Id = 1,
            Iso31661 = countryIso,
            Rating = "PG-13",
            Meaning = "Parental guidance",
            Order = 1,
        };

        Movie movie = new()
        {
            Id = 42,
            Title = "Test Movie",
            TitleSort = "test movie",
            Overview = "A movie used for DTO tests.",
        };

        movie.CertificationMovies.Add(
            new()
            {
                CertificationId = certification.Id,
                Certification = certification,
                MovieId = movie.Id,
                Movie = movie,
            }
        );

        movie.VideoFiles.Add(
            new()
            {
                Filename = "test-movie.mkv",
                Folder = "/test",
                HostFolder = "/test",
                Languages = "[\"en\"]",
                Quality = "1080p",
                Share = "movies",
                MovieId = movie.Id,
                Movie = movie,
                Tracks = tracks ?? [],
            }
        );

        return movie;
    }

    [Fact]
    public void Ctor_PlainMovie_NoIndex_StillSetsContentRating()
    {
        Movie movie = BuildMovieWithCertification("US");

        VideoPlaylistResponseDto dto = new(movie, "movie", 1, "US");

        Assert.NotNull(dto.ContentRating);
        Assert.Equal("PG-13", dto.ContentRating!.Rating);
    }

    [Fact]
    public void Ctor_CollectionMovie_WithIndex_SetsContentRatingAndCollectionFields()
    {
        Movie movie = BuildMovieWithCertification("US");

        VideoPlaylistResponseDto dto = new(movie, "collection", 1, "US", index: 2);

        Assert.NotNull(dto.ContentRating);
        Assert.Equal("PG-13", dto.ContentRating!.Rating);
        Assert.Equal(0, dto.Season);
        Assert.Equal(2, dto.Episode);
        Assert.Equal("Collection", dto.SeasonName);
    }

    [Fact]
    public void Ctor_SpriteKindTrack_IsExposedAsThumbnails()
    {
        Movie movie = BuildMovieWithCertification(
            "US",
            [new() { File = "/sprite.webp", Kind = "sprite" }]
        );

        VideoPlaylistResponseDto dto = new(movie, "movie", 1, "US");

        Assert.Contains(dto.Tracks, track => track.Kind == "thumbnails");
        Assert.DoesNotContain(dto.Tracks, track => track.Kind == "sprite");
    }

    [Fact]
    public void Ctor_ThumbnailsKindTrack_StaysThumbnails()
    {
        Movie movie = BuildMovieWithCertification(
            "US",
            [new() { File = "/thumbs_320x178.vtt", Kind = "thumbnails" }]
        );

        VideoPlaylistResponseDto dto = new(movie, "movie", 1, "US");

        VideoTrack track = Assert.Single(dto.Tracks, t => t.Kind == "thumbnails");
        Assert.EndsWith(".vtt", track.File);
    }

    [Fact]
    public void Ctor_SpriteAndThumbnailsBothPresent_DedupesToSingleVttThumbnailsTrack()
    {
        Movie movie = BuildMovieWithCertification(
            "US",
            [
                new() { File = "/thumbs_320x178.webp", Kind = "sprite" },
                new() { File = "/thumbs_320x178.vtt", Kind = "thumbnails" },
            ]
        );

        VideoPlaylistResponseDto dto = new(movie, "movie", 1, "US");

        VideoTrack track = Assert.Single(dto.Tracks, t => t.Kind == "thumbnails");
        Assert.EndsWith(".vtt", track.File);
    }
}
