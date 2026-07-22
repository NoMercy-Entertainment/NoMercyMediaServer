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

using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Data.DTOs.Specials;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.TvShows;
using Xunit;

namespace NoMercy.Tests.Api.Media;

[Trait(name: "Category", value: "Unit")]
public class SpecialItemsDtoTests
{
    private static Cast BuildCast(
        int personId,
        string name,
        string character,
        int order,
        string? colorPaletteJson = null
    )
    {
        return new()
        {
            Person = new()
            {
                Id = personId,
                Name = name,
                Profile = $"/person-{personId}.jpg",
                KnownForDepartment = "Acting",
                Gender = "Female",
                _colorPalette = colorPaletteJson,
            },
            Role = new() { Character = character, Order = order },
        };
    }

    private static Crew BuildCrew(int personId, string name, string task, int order)
    {
        return new()
        {
            Person = new()
            {
                Id = personId,
                Name = name,
                Profile = $"/person-{personId}.jpg",
                KnownForDepartment = "Directing",
                Gender = "Male",
            },
            Job = new() { Task = task, Order = order },
        };
    }

    private static Movie BuildMovie()
    {
        Movie movie = new()
        {
            Id = 501,
            Title = "Movie Title",
            Overview = "Movie overview text.",
            Backdrop = "/movie-backdrop.jpg",
            Poster = "/movie-poster.jpg",
            ReleaseDate = new(year: 2015, month: 6, day: 1),
            Runtime = 125,
            VoteAverage = 7.8,
            Video = "yt-key-1",
            ColorPalette = new() { Poster = new() { Dominant = "#111111" } },
        };

        movie.Images.Add(item: new() { Type = "logo", FilePath = "/logo.png" });
        movie.Images.Add(
            item: new()
            {
                Type = "backdrop",
                FilePath = "/bd1.jpg",
                Site = "https://image.tmdb.org/t/p/",
                Width = 100,
                Height = 50,
                Iso6391 = "en",
                VoteAverage = 1,
                VoteCount = 1,
            }
        );
        movie.Images.Add(
            item: new()
            {
                Type = "backdrop",
                FilePath = "/bd2.jpg",
                Site = "local",
                Width = 200,
                Height = 100,
            }
        );
        movie.Images.Add(
            item: new()
            {
                Type = "poster",
                FilePath = "/p1.jpg",
                Site = "https://image.tmdb.org/t/p/",
            }
        );
        movie.Images.Add(
            item: new()
            {
                Type = "poster",
                FilePath = "/p2.jpg",
                Site = "local",
            }
        );

        Genre actionGenre = new() { Id = 1, Name = "Action" };
        Genre dramaGenre = new() { Id = 2, Name = "Drama" };
        movie.GenreMovies.Add(
            item: new()
            {
                GenreId = 1,
                Genre = actionGenre,
                MovieId = movie.Id,
            }
        );
        movie.GenreMovies.Add(
            item: new()
            {
                GenreId = 2,
                Genre = dramaGenre,
                MovieId = movie.Id,
            }
        );

        Certification certification = new()
        {
            Id = 9,
            Iso31661 = "US",
            Rating = "R",
            Meaning = "Restricted",
            Order = 5,
        };
        movie.CertificationMovies.Add(
            item: new()
            {
                CertificationId = certification.Id,
                Certification = certification,
                MovieId = movie.Id,
            }
        );

        movie.VideoFiles.Add(item: new() { Filename = "movie.mkv", HostFolder = "/x" });

        movie.Cast.Add(item: BuildCast(personId: 1, name: "Actor One", character: "Hero", order: 1));
        movie.Cast.Add(item: BuildCast(personId: 2, name: "Actor Two", character: "Sidekick", order: 2));

        movie.Crew.Add(item: BuildCrew(personId: 3, name: "Director One", task: "Director", order: 1));
        movie.Crew.Add(item: BuildCrew(personId: 4, name: "Writer One", task: "Writer", order: 2));

        return movie;
    }

    [Fact]
    public void Ctor_Movie_MapsCoreFieldsAndComputesDuration()
    {
        Movie movie = BuildMovie();

        SpecialItemsDto dto = new(movie: movie);

        Assert.Equal(expected: 501, actual: dto.Id);
        Assert.Empty(collection: dto.EpisodeIds);
        Assert.Equal(expected: "Movie Title", actual: dto.Title);
        Assert.Equal(expected: "Movie overview text.", actual: dto.Overview);
        Assert.Equal(expected: "/movie-backdrop.jpg", actual: dto.Backdrop);
        Assert.Equal(expected: "/logo.png", actual: dto.Logo);
        Assert.Equal(expected: "movie", actual: dto.MediaType);
        Assert.Equal(expected: "movie", actual: dto.Type);
        Assert.Equal(expected: "/movie/501", actual: dto.Link.ToString());
        Assert.Equal(expected: 2015, actual: dto.Year);
        Assert.Equal(expected: 7500, actual: dto.Duration);
        Assert.Equal(expected: 7500, actual: dto.TotalDuration);
        Assert.Equal(expected: 7.8, actual: dto.VoteAverage);
        Assert.Equal(expected: 1, actual: dto.NumberOfItems);
        Assert.Equal(expected: 1, actual: dto.HaveItems);
        Assert.Equal(expected: "yt-key-1", actual: dto.VideoId);
        dto.ColorPalette!.Poster!.Dominant.Should().Be(expected: "#111111");

        dto.Genres.Should().HaveCount(expected: 2);
        Assert.Equal(expected: 1, actual: (int)dto.Genres.First().Id);
        Assert.Equal(expected: "Action", actual: dto.Genres.First().Name);
        Assert.Equal(expected: "/genres/1", actual: dto.Genres.First().Link.ToString());

        Assert.Equal(expected: "R", actual: dto.Rating.Rating);
        Assert.Equal(expected: "US", actual: dto.Rating.Iso31661);
        Assert.Equal(expected: "Restricted", actual: dto.Rating.Meaning);

        dto.Backdrops.Should().HaveCount(expected: 2);
        ImageDto[] backdrops = dto.Backdrops.ToArray();
        Assert.Equal(expected: "/bd1.jpg", actual: backdrops[0].Src);
        Assert.Equal(expected: "/images/music/bd2.jpg", actual: backdrops[1].Src);

        dto.Posters.Should().HaveCount(expected: 2);
        ImageDto[] posters = dto.Posters.ToArray();
        Assert.Equal(expected: "/p1.jpg", actual: posters[0].Src);
        Assert.Equal(expected: "/images/music/p2.jpg", actual: posters[1].Src);

        dto.Cast.Should().HaveCount(expected: 2);
        dto.Crew.Should().HaveCount(expected: 2);
        Assert.Equal(expected: "Hero", actual: dto.Cast.First().Character);
        Assert.Equal(expected: "Director", actual: dto.Crew.First().Job);
    }

    [Fact]
    public void Ctor_Movie_TruncatesBackdropsPostersCastCrew_ToConfiguredLimits()
    {
        Movie movie = BuildMovie();
        movie.Images.Add(item: new() { Type = "backdrop", FilePath = "/bd3.jpg" });
        movie.Images.Add(item: new() { Type = "poster", FilePath = "/p3.jpg" });

        for (int i = 5; i <= 20; i++)
            movie.Cast.Add(item: BuildCast(personId: i, name: $"Actor {i}", character: "Extra", order: i));
        for (int i = 5; i <= 20; i++)
            movie.Crew.Add(item: BuildCrew(personId: i, name: $"Crew {i}", task: "Grip", order: i));

        SpecialItemsDto dto = new(movie: movie);

        dto.Backdrops.Should().HaveCount(expected: 2);
        dto.Posters.Should().HaveCount(expected: 2);
        dto.Cast.Should().HaveCount(expected: 15);
        dto.Crew.Should().HaveCount(expected: 15);
    }

    [Fact]
    public void Ctor_Movie_NoRuntimeNoVideoFilesNoCertifications_ZerosOutFields()
    {
        Movie movie = new()
        {
            Id = 502,
            Title = "Bare Movie",
            Runtime = null,
        };

        SpecialItemsDto dto = new(movie: movie);

        Assert.Equal(expected: 0, actual: dto.Duration);
        Assert.Equal(expected: 0, actual: dto.TotalDuration);
        Assert.Equal(expected: 0, actual: dto.HaveItems);
        dto.Rating.Should().BeEquivalentTo(expectation: new Certification());
        dto.Backdrops.Should().BeEmpty();
        dto.Posters.Should().BeEmpty();
        Assert.Null(@object: dto.Logo);
    }

    private static Episode BuildEpisode(
        int id,
        int seasonNumber,
        string? duration,
        bool hasVideoFile
    )
    {
        Episode episode = new() { Id = id, SeasonNumber = seasonNumber };

        if (hasVideoFile)
            episode.VideoFiles.Add(
                item: new()
                {
                    Filename = $"ep{id}.mkv",
                    HostFolder = "/x",
                    Duration = duration,
                }
            );

        return episode;
    }

    [Fact]
    public void Ctor_Tv_MapsCoreFieldsAndFiltersSeasonsForCounts()
    {
        Tv tv = new()
        {
            Id = 900,
            Title = "Show Title",
            Overview = "Show overview",
            Backdrop = "/tv-backdrop.jpg",
            Poster = "/tv-poster.jpg",
            FirstAirDate = new(year: 2018, month: 3, day: 1),
            Duration = 10,
            VoteAverage = 8.2,
            Trailer = "yt-trailer-1",
        };

        Episode episodeA = BuildEpisode(id: 1, seasonNumber: 1, duration: "00:10:00", hasVideoFile: true);
        Episode episodeB = BuildEpisode(id: 2, seasonNumber: 1, duration: null, hasVideoFile: false);
        Episode episodeC = BuildEpisode(id: 3, seasonNumber: 0, duration: "00:05:00", hasVideoFile: true);
        Episode episodeE = BuildEpisode(id: 4, seasonNumber: 1, duration: null, hasVideoFile: true);

        tv.Episodes.Add(item: episodeA);
        tv.Episodes.Add(item: episodeB);
        tv.Episodes.Add(item: episodeC);
        tv.Episodes.Add(item: episodeE);

        Genre genre = new() { Id = 7, Name = "Sci-Fi" };
        tv.GenreTvs.Add(
            item: new()
            {
                GenreId = 7,
                Genre = genre,
                TvId = tv.Id,
            }
        );

        Certification certification = new()
        {
            Id = 3,
            Iso31661 = "NL",
            Rating = "12",
            Meaning = "Twelve and up",
        };
        tv.CertificationTvs.Add(
            item: new()
            {
                CertificationId = certification.Id,
                Certification = certification,
                TvId = tv.Id,
            }
        );

        tv.Cast.Add(item: BuildCast(personId: 10, name: "TV Actor", character: "Lead", order: 1));
        tv.Crew.Add(item: BuildCrew(personId: 11, name: "TV Director", task: "Director", order: 1));

        SpecialItemsDto dto = new(tv: tv);

        Assert.Equal(expected: 900, actual: dto.Id);
        Assert.Equal(expectedSpan: [1, 2, 3, 4], actualArray: dto.EpisodeIds);
        Assert.Equal(expected: "Show Title", actual: dto.Title);
        Assert.Equal(expected: "tv", actual: dto.MediaType);
        Assert.Equal(expected: "tv", actual: dto.Type);
        Assert.Equal(expected: "/tv/900", actual: dto.Link.ToString());
        Assert.Equal(expected: 2018, actual: dto.Year);
        Assert.Equal(expected: 8.2, actual: dto.VoteAverage);
        Assert.Equal(expected: "yt-trailer-1", actual: dto.VideoId);

        // NumberOfItems / HaveItems only count SeasonNumber > 0 episodes (A, B, E).
        Assert.Equal(expected: 3, actual: dto.NumberOfItems);
        // Only A and E have video files among the season > 0 episodes.
        Assert.Equal(expected: 2, actual: dto.HaveItems);
        Assert.Equal(expected: 1200, actual: dto.Duration); // tv.Duration(10) * have(2) * 60

        // TotalDuration sums over ALL episodes regardless of season, null-safe.
        Assert.Equal(expected: 900, actual: dto.TotalDuration); // 600 (A) + 0 (B, no file) + 300 (C) + 0 (E, null duration)

        Assert.Equal(expected: "12", actual: dto.Rating.Rating);
        Assert.Equal(expected: "NL", actual: dto.Rating.Iso31661);

        dto.Genres.Should().ContainSingle();
        dto.Cast.Should().ContainSingle();
        dto.Crew.Should().ContainSingle();
    }

    [Fact]
    public void Ctor_Tv_NoCertificationsAndNullDuration_FallsBackToDefaults()
    {
        Tv tv = new()
        {
            Id = 901,
            Title = "Bare Show",
            Duration = null,
        };
        tv.Episodes.Add(item: BuildEpisode(id: 1, seasonNumber: 1, duration: "00:01:00", hasVideoFile: true));

        SpecialItemsDto dto = new(tv: tv);

        Assert.Equal(expected: 0, actual: dto.Duration);
        dto.Rating.Should().BeEquivalentTo(expectation: new Certification());
    }

    private static SpecialMovieProjection BuildMovieProjection()
    {
        string colorPaletteJson = JsonConvert.SerializeObject(
            value: new ColorPalette { Poster = new() { Dominant = "#abcdef" } }
        );

        return new()
        {
            Id = 10,
            Title = "Proj Movie",
            Overview = "proj overview",
            Backdrop = "/pmb.jpg",
            Poster = "/pmp.jpg",
            Logo = "/logo-proj.png",
            ColorPalette = colorPaletteJson,
            ReleaseDate = new(year: 2010, month: 1, day: 1),
            Runtime = 100,
            VoteAverage = 6.5,
            Video = "v-key",
            VideoFileCount = 2,
            CertificationRating = "PG",
            CertificationCountry = "US",
            Genres = [new() { Id = 5, Name = "Sci-Fi" }],
            Backdrops =
            [
                new()
                {
                    Id = 1,
                    Site = "https://image.tmdb.org/t/p/",
                    FilePath = "/bd-tmdb.jpg",
                    Width = 200,
                    Height = 100,
                    Type = "backdrop",
                    Iso6391 = "en",
                    VoteAverage = 5,
                    VoteCount = 10,
                    ColorPalette = colorPaletteJson,
                },
                new()
                {
                    Id = 2,
                    Site = "local",
                    FilePath = "/bd-local.jpg",
                    Width = 300,
                    Height = 150,
                    Type = "backdrop",
                },
            ],
            Posters =
            [
                new()
                {
                    Id = 3,
                    Site = "https://image.tmdb.org/t/p/",
                    FilePath = "/p-tmdb.jpg",
                    Type = "poster",
                },
                new()
                {
                    Id = 4,
                    Site = "local",
                    FilePath = "/p-local.jpg",
                    Type = "poster",
                },
            ],
            Cast =
            [
                new()
                {
                    PersonId = 1,
                    PersonName = "Actor One",
                    PersonProfile = "/actor1.jpg",
                    PersonKnownForDepartment = "Acting",
                    PersonColorPalette = colorPaletteJson,
                    PersonGender = "Female",
                    Character = "Hero",
                    Order = 1,
                },
            ],
            Crew =
            [
                new()
                {
                    PersonId = 2,
                    PersonName = "Crew One",
                    PersonProfile = "/crew1.jpg",
                    PersonKnownForDepartment = "Directing",
                    PersonGender = "Male",
                    Task = "Director",
                    Order = 1,
                },
            ],
        };
    }

    [Fact]
    public void Ctor_SpecialMovieProjection_MapsAllFieldsWithImageSiteBranching()
    {
        SpecialMovieProjection projection = BuildMovieProjection();

        SpecialItemsDto dto = new(movie: projection);

        Assert.Equal(expected: 10, actual: dto.Id);
        Assert.Empty(collection: dto.EpisodeIds);
        Assert.Equal(expected: "Proj Movie", actual: dto.Title);
        Assert.Equal(expected: "/logo-proj.png", actual: dto.Logo);
        Assert.Equal(expected: "movie", actual: dto.MediaType);
        Assert.Equal(expected: "/movie/10", actual: dto.Link.ToString());
        Assert.Equal(expected: 2010, actual: dto.Year);
        Assert.Equal(expected: 6000, actual: dto.Duration);
        Assert.Equal(expected: 6000, actual: dto.TotalDuration);
        Assert.Equal(expected: 6.5, actual: dto.VoteAverage);
        Assert.Equal(expected: 1, actual: dto.NumberOfItems);
        Assert.Equal(expected: 1, actual: dto.HaveItems);
        Assert.Equal(expected: "v-key", actual: dto.VideoId);
        dto.ColorPalette!.Poster!.Dominant.Should().Be(expected: "#abcdef");

        Assert.Equal(expected: "PG", actual: dto.Rating.Rating);
        Assert.Equal(expected: "US", actual: dto.Rating.Iso31661);

        dto.Genres.Should().ContainSingle();
        Assert.Equal(expected: 5, actual: (int)dto.Genres.First().Id);
        Assert.Equal(expected: "/genres/5", actual: dto.Genres.First().Link.ToString());

        ImageDto[] backdrops = dto.Backdrops.ToArray();
        Assert.Equal(expected: "/bd-tmdb.jpg", actual: backdrops[0].Src);
        Assert.Equal(expected: "/images/music/bd-local.jpg", actual: backdrops[1].Src);
        backdrops[0].ColorPalette!.Poster!.Dominant.Should().Be(expected: "#abcdef");

        ImageDto[] posters = dto.Posters.ToArray();
        Assert.Equal(expected: "/p-tmdb.jpg", actual: posters[0].Src);
        Assert.Equal(expected: "/images/music/p-local.jpg", actual: posters[1].Src);

        PeopleDto cast = dto.Cast.Single();
        Assert.Equal(expected: 1, actual: cast.Id);
        Assert.Equal(expected: "Actor One", actual: cast.Name);
        Assert.Equal(expected: "Hero", actual: cast.Character);
        Assert.Equal(expected: "/person/1", actual: cast.Link.ToString());
        Assert.Empty(collection: cast.Translations);

        PeopleDto crew = dto.Crew.Single();
        Assert.Equal(expected: "Director", actual: crew.Job);
        Assert.Equal(expected: "/person/2", actual: crew.Link.ToString());
    }

    [Fact]
    public void Ctor_SpecialMovieProjection_NullsFallBackToEmptyAndZero()
    {
        SpecialMovieProjection projection = BuildMovieProjection();
        projection.CertificationRating = null;
        projection.CertificationCountry = null;
        projection.Runtime = null;
        projection.VideoFileCount = 0;
        projection.ColorPalette = string.Empty;

        SpecialItemsDto dto = new(movie: projection);

        Assert.Equal(expected: string.Empty, actual: dto.Rating.Rating);
        Assert.Equal(expected: string.Empty, actual: dto.Rating.Iso31661);
        Assert.Equal(expected: 0, actual: dto.Duration);
        Assert.Equal(expected: 0, actual: dto.HaveItems);
        Assert.Null(@object: dto.ColorPalette);
    }

    private static SpecialTvProjection BuildTvProjection()
    {
        string colorPaletteJson = JsonConvert.SerializeObject(
            value: new ColorPalette { Poster = new() { Dominant = "#fedcba" } }
        );

        return new()
        {
            Id = 20,
            Title = "Proj Show",
            Overview = "proj tv overview",
            Backdrop = "/ptb.jpg",
            Poster = "/ptp.jpg",
            Logo = "/logo-proj-tv.png",
            ColorPalette = colorPaletteJson,
            FirstAirDate = new(year: 2012, month: 4, day: 1),
            Duration = 20,
            VoteAverage = 7.1,
            Trailer = "tv-key",
            NumberOfEpisodes = 10,
            HaveEpisodes = 3,
            EpisodeIds = [101, 102, 103],
            EpisodeDurations = ["00:20:00", null, "00:10:00"],
            CertificationRating = "16",
            CertificationCountry = "NL",
            Genres = [new() { Id = 6, Name = "Drama" }],
            Backdrops =
            [
                new()
                {
                    Id = 1,
                    Site = "https://image.tmdb.org/t/p/",
                    FilePath = "/tv-bd-tmdb.jpg",
                    Type = "backdrop",
                },
                new()
                {
                    Id = 2,
                    Site = "local",
                    FilePath = "/tv-bd-local.jpg",
                    Type = "backdrop",
                },
            ],
            Posters =
            [
                new()
                {
                    Id = 3,
                    Site = "https://image.tmdb.org/t/p/",
                    FilePath = "/tv-p-tmdb.jpg",
                    Type = "poster",
                },
                new()
                {
                    Id = 4,
                    Site = "local",
                    FilePath = "/tv-p-local.jpg",
                    Type = "poster",
                },
            ],
            Cast =
            [
                new()
                {
                    PersonId = 5,
                    PersonName = "TV Actor",
                    PersonGender = "Female",
                    Character = "Lead",
                    Order = 1,
                },
            ],
            Crew =
            [
                new()
                {
                    PersonId = 6,
                    PersonName = "TV Crew",
                    PersonGender = "Male",
                    Task = "Showrunner",
                    Order = 1,
                },
            ],
        };
    }

    [Fact]
    public void Ctor_SpecialTvProjection_MapsAllFieldsDirectlyFromProjection()
    {
        SpecialTvProjection projection = BuildTvProjection();

        SpecialItemsDto dto = new(tv: projection);

        Assert.Equal(expected: 20, actual: dto.Id);
        Assert.Equal(expectedSpan: [101, 102, 103], actualArray: dto.EpisodeIds);
        Assert.Equal(expected: "Proj Show", actual: dto.Title);
        Assert.Equal(expected: "tv", actual: dto.MediaType);
        Assert.Equal(expected: "/tv/20", actual: dto.Link.ToString());
        Assert.Equal(expected: 2012, actual: dto.Year);
        Assert.Equal(expected: 7.1, actual: dto.VoteAverage);
        Assert.Equal(expected: "tv-key", actual: dto.VideoId);
        Assert.Equal(expected: 10, actual: dto.NumberOfItems);
        Assert.Equal(expected: 3, actual: dto.HaveItems);
        Assert.Equal(expected: 3600, actual: dto.Duration); // 20 * 3 * 60

        // EpisodeDurations: 1200 + 0 (null) + 600 = 1800, null-safe sum.
        Assert.Equal(expected: 1800, actual: dto.TotalDuration);

        Assert.Equal(expected: "16", actual: dto.Rating.Rating);
        Assert.Equal(expected: "NL", actual: dto.Rating.Iso31661);

        ImageDto[] backdrops = dto.Backdrops.ToArray();
        Assert.Equal(expected: "/tv-bd-tmdb.jpg", actual: backdrops[0].Src);
        Assert.Equal(expected: "/images/music/tv-bd-local.jpg", actual: backdrops[1].Src);

        ImageDto[] posters = dto.Posters.ToArray();
        Assert.Equal(expected: "/tv-p-tmdb.jpg", actual: posters[0].Src);
        Assert.Equal(expected: "/images/music/tv-p-local.jpg", actual: posters[1].Src);

        Assert.Equal(expected: "Lead", actual: dto.Cast.Single().Character);
        Assert.Equal(expected: "Showrunner", actual: dto.Crew.Single().Job);
    }

    [Fact]
    public void Ctor_SpecialTvProjection_NullDurationAndCertifications_FallsBackToDefaults()
    {
        SpecialTvProjection projection = BuildTvProjection();
        projection.Duration = null;
        projection.CertificationRating = null;
        projection.CertificationCountry = null;
        projection.EpisodeDurations = [null, null];

        SpecialItemsDto dto = new(tv: projection);

        Assert.Equal(expected: 0, actual: dto.Duration);
        Assert.Equal(expected: 0, actual: dto.TotalDuration);
        Assert.Equal(expected: string.Empty, actual: dto.Rating.Rating);
        Assert.Equal(expected: string.Empty, actual: dto.Rating.Iso31661);
    }
}
