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
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Extensions;
using Xunit;
using MediaEntity = NoMercy.Database.Models.Media.Media;

namespace NoMercy.Tests.Api.Media;

[Trait(name: "Category", value: "Unit")]
public class LibraryResponseItemDtoTests
{
    private static Movie BuildMovie(int id = 1)
    {
        Movie movie = new()
        {
            Id = id,
            Title = "Test Movie",
            Backdrop = "/movie-backdrop.jpg",
            Overview = "Movie overview",
            Poster = "/movie-poster.jpg",
            ReleaseDate = new(year: 2020, month: 5, day: 1),
            Video = "movie-video-key",
        };

        movie.Images.Add(item: new() { Type = "logo", FilePath = "/movie-logo.png" });

        Genre genre = new() { Id = 1, Name = "Action" };
        movie.GenreMovies.Add(
            item: new()
            {
                GenreId = 1,
                Genre = genre,
                MovieId = id,
            }
        );

        movie.VideoFiles.Add(
            item: new()
            {
                Filename = "movie.mkv",
                HostFolder = "/x",
                Folder = "/x",
            }
        );

        movie.Media.Add(
            item: new()
            {
                Site = "YouTube",
                Src = "yt-key",
                Name = "Trailer",
            }
        );
        movie.Media.Add(
            item: new()
            {
                Site = "Vimeo",
                Src = "vimeo-key",
                Name = "Other",
            }
        );

        return movie;
    }

    [Fact]
    public void Ctor_LibraryMovie_MapsFieldsFromNestedMovieAndFiltersYouTubeVideos()
    {
        Movie movie = BuildMovie();
        LibraryMovie libraryMovie = new() { Movie = movie };

        LibraryResponseItemDto dto = new(movie: libraryMovie);

        Assert.Equal(expected: "1", actual: dto.Id);
        Assert.Equal(expected: "/movie-backdrop.jpg", actual: dto.Backdrop);
        Assert.Equal(expected: "/movie-logo.png", actual: dto.Logo);
        Assert.Equal(expected: "movie", actual: dto.MediaType);
        Assert.Equal(expected: "movie", actual: dto.Type);
        Assert.Equal(expected: 2020, actual: dto.Year);
        Assert.Equal(expected: "Movie overview", actual: dto.Overview);
        Assert.Equal(expected: "/movie-poster.jpg", actual: dto.Poster);
        Assert.Equal(expected: "Test Movie", actual: dto.Title);
        Assert.Equal(expected: "Test Movie".TitleSort(date: movie.ReleaseDate), actual: dto.TitleSort);
        Assert.Equal(expected: "/movie/1", actual: dto.Link.ToString());
        Assert.Equal(expected: "movie-video-key", actual: dto.VideoId);

        dto.Genres.Should().ContainSingle();
        Assert.Equal(expected: "Action", actual: dto.Genres!.First().Name);

        dto.Videos.Should().ContainSingle();
        Assert.Equal(expected: "yt-key", actual: dto.Videos.First().Src);
    }

    [Fact]
    public void Ctor_LibraryMovie_NoLogoImage_LeavesLogoNull()
    {
        Movie movie = BuildMovie();
        movie.Images.Clear();
        LibraryMovie libraryMovie = new() { Movie = movie };

        LibraryResponseItemDto dto = new(movie: libraryMovie);

        Assert.Null(@object: dto.Logo);
    }

    private static Tv BuildTv(int id = 2)
    {
        Tv tv = new()
        {
            Id = id,
            Title = "Test Show",
            Backdrop = "/tv-backdrop.jpg",
            Overview = "Tv overview",
            Poster = "/tv-poster.jpg",
            FirstAirDate = new(year: 2019, month: 8, day: 1),
            Trailer = "tv-trailer-key",
            NumberOfEpisodes = 5,
        };

        tv.Images.Add(item: new() { Type = "logo", FilePath = "/tv-logo.png" });

        Genre genre = new() { Id = 2, Name = "Drama" };
        tv.GenreTvs.Add(
            item: new()
            {
                GenreId = 2,
                Genre = genre,
                TvId = id,
            }
        );

        Episode episodeWithFile = new() { Id = 10 };
        episodeWithFile.VideoFiles.Add(
            item: new()
            {
                Filename = "e1.mkv",
                HostFolder = "/x",
                Folder = "/x",
            }
        );

        Episode episodeWithoutFolder = new() { Id = 11 };
        episodeWithoutFolder.VideoFiles.Add(
            item: new()
            {
                Filename = "e2.mkv",
                HostFolder = "/x",
                Folder = null,
            }
        );

        tv.Episodes.Add(item: episodeWithFile);
        tv.Episodes.Add(item: episodeWithoutFolder);

        tv.Media.Add(
            item: new()
            {
                Site = "YouTube",
                Src = "tv-yt-key",
                Name = "Trailer",
            }
        );

        return tv;
    }

    [Fact]
    public void Ctor_LibraryTv_MapsFieldsAndCountsEpisodesWithFolder()
    {
        Tv tv = BuildTv();
        LibraryTv libraryTv = new() { Tv = tv };

        LibraryResponseItemDto dto = new(tv: libraryTv);

        Assert.Equal(expected: "2", actual: dto.Id);
        Assert.Equal(expected: "/tv-backdrop.jpg", actual: dto.Backdrop);
        Assert.Equal(expected: "/tv-logo.png", actual: dto.Logo);
        Assert.Equal(expected: "tv", actual: dto.MediaType);
        Assert.Equal(expected: "tv", actual: dto.Type);
        Assert.Equal(expected: 2019, actual: dto.Year);
        Assert.Equal(expected: "Tv overview", actual: dto.Overview);
        Assert.Equal(expected: "Test Show", actual: dto.Title);
        Assert.Equal(expected: "Test Show".TitleSort(date: tv.FirstAirDate), actual: dto.TitleSort);
        Assert.Equal(expected: "/tv/2", actual: dto.Link.ToString());
        Assert.Equal(expected: "tv-trailer-key", actual: dto.VideoId);
        Assert.Equal(expected: 5, actual: dto.NumberOfItems);
        // Only the episode whose video file has a non-null Folder counts.
        Assert.Equal(expected: 1, actual: dto.HaveItems);

        dto.Genres.Should().ContainSingle();
        dto.Videos.Should().ContainSingle();
        Assert.Equal(expected: "tv-yt-key", actual: dto.Videos.First().Src);
    }

    [Fact]
    public void Ctor_MovieDirect_MapsFieldsAndCountsFilesWithFolder()
    {
        Movie movie = BuildMovie(id: 3);
        movie.VideoFiles.Add(
            item: new()
            {
                Filename = "extra.mkv",
                HostFolder = "/x",
                Folder = null,
            }
        );

        LibraryResponseItemDto dto = new(movie: movie);

        Assert.Equal(expected: "3", actual: dto.Id);
        Assert.Equal(expected: 1, actual: dto.NumberOfItems);
        // Only the first video file has a non-null Folder; the extra one doesn't count.
        Assert.Equal(expected: 1, actual: dto.HaveItems);
        Assert.Equal(expected: "movie", actual: dto.Type);
        Assert.Equal(expected: "movie-video-key", actual: dto.VideoId);
        dto.Videos.Should().ContainSingle();
    }

    [Fact]
    public void Ctor_TvDirect_MapsFieldsAndCountsEpisodesWithFolder()
    {
        Tv tv = BuildTv(id: 4);

        LibraryResponseItemDto dto = new(tv: tv);

        Assert.Equal(expected: "4", actual: dto.Id);
        Assert.Equal(expected: 5, actual: dto.NumberOfItems);
        Assert.Equal(expected: 1, actual: dto.HaveItems);
        Assert.Equal(expected: "tv", actual: dto.Type);
        Assert.Equal(expected: "tv", actual: dto.MediaType);
        Assert.Equal(expected: "tv-trailer-key", actual: dto.VideoId);
    }

    [Fact]
    public void Ctor_CollectionMovie_MapsFieldsFromNestedMovie()
    {
        Movie movie = BuildMovie(id: 5);
        CollectionMovie collectionMovie = new() { Movie = movie };

        LibraryResponseItemDto dto = new(movie: collectionMovie);

        Assert.Equal(expected: "5", actual: dto.Id);
        Assert.Equal(expected: "movie", actual: dto.Type);
        Assert.Equal(expected: 1, actual: dto.NumberOfItems);
        Assert.Equal(expected: 1, actual: dto.HaveItems);
        Assert.Equal(expected: "movie-video-key", actual: dto.VideoId);
        dto.Genres.Should().ContainSingle();
    }

    private static Collection BuildCollection()
    {
        Collection collection = new()
        {
            Id = 100,
            Title = "The Original Collection",
            Backdrop = "/collection-backdrop.jpg",
            Poster = "/collection-poster.jpg",
            Parts = 2,
        };

        collection.Images.Add(item: new() { Type = "logo", FilePath = "/collection-logo.png" });

        Movie earlyMovie = new()
        {
            Id = 6,
            Title = "Early Movie",
            ReleaseDate = new(year: 2001, month: 1, day: 1),
            Video = "early-video-key",
        };
        earlyMovie.VideoFiles.Add(
            item: new()
            {
                Filename = "early.mkv",
                HostFolder = "/x",
                Folder = "/x",
            }
        );

        Genre genre = new() { Id = 3, Name = "Sci-Fi" };
        earlyMovie.GenreMovies.Add(
            item: new()
            {
                GenreId = 3,
                Genre = genre,
                MovieId = 6,
            }
        );

        Movie laterMovie = new()
        {
            Id = 7,
            Title = "Later Movie",
            ReleaseDate = new(year: 2010, month: 1, day: 1),
            Video = "later-video-key",
        };

        // Inserted out of date order on purpose: VideoId comes from FirstOrDefault
        // (insertion order), while Year/TitleSort come from MinBy (release date order).
        collection.CollectionMovies.Add(item: new() { CollectionId = 100, Movie = laterMovie });
        collection.CollectionMovies.Add(item: new() { CollectionId = 100, Movie = earlyMovie });

        return collection;
    }

    [Fact]
    public void Ctor_Collection_UsesEarliestMovieForYearAndTitleSort_NoTranslation()
    {
        Collection collection = BuildCollection();

        LibraryResponseItemDto dto = new(collection: collection);

        Assert.Equal(expected: "100", actual: dto.Id);
        Assert.Equal(expected: "The Original Collection", actual: dto.Title);
        Assert.Equal(expected: string.Empty, actual: dto.Overview);
        Assert.Equal(expected: "/collection-backdrop.jpg", actual: dto.Backdrop);
        Assert.Equal(expected: "/collection-logo.png", actual: dto.Logo);
        Assert.Equal(expected: 2001, actual: dto.Year); // earliest release date among CollectionMovies
        Assert.Equal(expected: "/collection-poster.jpg", actual: dto.Poster);
        Assert.Equal(expected: "The Original Collection".TitleSort(parseYear: 2001), actual: dto.TitleSort);
        Assert.Equal(expected: "specials", actual: dto.Type);
        Assert.Equal(expected: "specials", actual: dto.MediaType);
        Assert.Equal(expected: "/collection/100", actual: dto.Link.ToString());
        Assert.Equal(expected: 2, actual: dto.NumberOfItems);
        // Only earlyMovie has a video file with a non-null Folder.
        Assert.Equal(expected: 1, actual: dto.HaveItems);
        // VideoId comes from FirstOrDefault() (insertion order = laterMovie), not
        // from the release-date-earliest movie used for Year/TitleSort.
        Assert.Equal(expected: "later-video-key", actual: dto.VideoId);

        dto.Genres.Should().ContainSingle();
    }

    [Fact]
    public void Ctor_Collection_UsesTranslationTitleAndOverview_WhenPresent()
    {
        Collection collection = BuildCollection();
        collection.Translations.Add(
            item: new() { Title = "De Originele Collectie", Overview = "Nederlandse samenvatting." }
        );

        LibraryResponseItemDto dto = new(collection: collection);

        Assert.Equal(expected: "De Originele Collectie", actual: dto.Title);
        Assert.Equal(expected: "Nederlandse samenvatting.", actual: dto.Overview);
    }

    [Fact]
    public void Ctor_Collection_EmptyCollectionMovies_YearIsNull()
    {
        Collection collection = new()
        {
            Id = 101,
            Title = "Empty Collection",
            Parts = 0,
        };

        LibraryResponseItemDto dto = new(collection: collection);

        Assert.Null(value: dto.Year);
        Assert.Equal(expected: "Empty Collection".TitleSort(parseYear: (int?)null), actual: dto.TitleSort);
        Assert.Null(@object: dto.VideoId);
    }

    [Fact]
    public void Ctor_Special_MapsMinimalFieldsIntoNameNotTitle()
    {
        Special special = new()
        {
            Id = Ulid.NewUlid(),
            Title = "A Special Event",
            Overview = "special overview",
            Backdrop = "/special-backdrop.jpg",
            Poster = "/special-poster.jpg",
        };

        LibraryResponseItemDto dto = new(special: special);

        Assert.Equal(expected: special.Id.ToString(), actual: dto.Id);
        Assert.Equal(expected: "A Special Event", actual: dto.Name);
        Assert.Equal(expected: string.Empty, actual: dto.Title); // Title is never set for Special, only Name
        Assert.Equal(expected: "special overview", actual: dto.Overview);
        Assert.Equal(expected: "/special-backdrop.jpg", actual: dto.Backdrop);
        Assert.Equal(expected: "specials", actual: dto.MediaType);
        Assert.Equal(expected: "specials", actual: dto.Type);
        Assert.Equal(expected: $"/specials/{special.Id}", actual: dto.Link.ToString());
        Assert.Equal(expected: "/special-poster.jpg", actual: dto.Poster);
        Assert.Equal(expected: "A Special Event".TitleSort(), actual: dto.TitleSort);
    }

    [Fact]
    public void Ctor_Special_NullTitleAndOverview_FallBackToEmpty()
    {
        Special special = new()
        {
            Id = Ulid.NewUlid(),
            Title = null,
            Overview = null,
        };

        LibraryResponseItemDto dto = new(special: special);

        Assert.Equal(expected: string.Empty, actual: dto.Name);
        Assert.Equal(expected: string.Empty, actual: dto.Overview);
    }

    [Fact]
    public void Ctor_Person_UsesTranslationTitleAndBiography_WhenPresent()
    {
        Person person = new()
        {
            Id = 50,
            Name = "Original Name",
            Biography = "Original biography.",
            Profile = "/person-profile.jpg",
            Gender = "Female",
        };
        person.Translations.Add(
            item: new() { Title = "Vertaalde Naam", Biography = "Vertaalde biografie." }
        );

        LibraryResponseItemDto dto = new(person: person);

        Assert.Equal(expected: "50", actual: dto.Id);
        Assert.Equal(expected: "Vertaalde Naam", actual: dto.Name);
        Assert.Equal(expected: "Vertaalde biografie.", actual: dto.Overview);
        Assert.Equal(expected: "person", actual: dto.MediaType);
        Assert.Equal(expected: "person", actual: dto.Type);
        Assert.Equal(expected: "/person/50", actual: dto.Link.ToString());
        // TitleSort always normalizes the raw (untranslated) Person.Name via the
        // shared TitleSort() helper, same as every sibling constructor in this file
        // and required by AlphaBucket's "normalized TitleSort, never raw title" contract.
        Assert.Equal(expected: "Original Name".TitleSort(), actual: dto.TitleSort);
        Assert.Equal(expected: "/person-profile.jpg", actual: dto.Poster);
    }

    [Fact]
    public void Ctor_Person_NoTranslation_FallsBackToOwnNameAndBiography()
    {
        Person person = new()
        {
            Id = 51,
            Name = "The Plain Name",
            Biography = null,
        };

        LibraryResponseItemDto dto = new(person: person);

        Assert.Equal(expected: "The Plain Name", actual: dto.Name);
        Assert.Equal(expected: string.Empty, actual: dto.Overview);
        // Leading article is stripped by the shared TitleSort() helper.
        Assert.Equal(expected: "plain.name", actual: dto.TitleSort);
    }

    [Fact]
    public void Ctor_Movie_TitleWithColon_UsesReleaseDateInTitleSort()
    {
        Movie movie = BuildMovie(id: 8);
        movie.Title = "Se7en: Director's Cut";
        movie.ReleaseDate = new(year: 1995, month: 1, day: 1);

        LibraryResponseItemDto dto = new(movie: movie);

        // Pin the DTO's forwarding of (title, release date) to the shared TitleSort
        // helper, rather than re-deriving that helper's own regex behaviour here.
        Assert.Equal(expected: "Se7en: Director's Cut".TitleSort(date: movie.ReleaseDate), actual: dto.TitleSort);
    }

    [Fact]
    public void Ctor_LibraryMovie_NoYouTubeMedia_VideosEmpty()
    {
        Movie movie = BuildMovie(id: 9);
        movie.Media.Clear();
        movie.Media.Add(item: new MediaEntity { Site = "Vimeo", Src = "vimeo-only" });
        LibraryMovie libraryMovie = new() { Movie = movie };

        LibraryResponseItemDto dto = new(movie: libraryMovie);

        Assert.Empty(collection: dto.Videos);
    }
}
