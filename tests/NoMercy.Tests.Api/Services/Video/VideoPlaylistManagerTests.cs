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

using Moq;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.Services.Video;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Domain;
using Xunit;

namespace NoMercy.Tests.Api.Services.Video;

// VideoPlaylistManager is the orchestration layer behind "start playing this
// item" for movies, tv, collections and specials -- it decides WHICH repository
// to call, WHICH item in the resulting list becomes "the thing to play now"
// (explicit itemId, else most-recently-progressed, else first), and it is the
// place that stamps the dynamic "playlist_id" onto every DTO in the list. None
// of that branching had a single test before this file.
[Trait(name: "Category", value: "Playlist")]
public sealed class VideoPlaylistManagerTests
{
    private readonly Mock<IMovieRepository> _movieRepository = new();
    private readonly Mock<ITvShowRepository> _tvShowRepository = new();
    private readonly Mock<ICollectionRepository> _collectionRepository = new();
    private readonly Mock<ISpecialRepository> _specialRepository = new();

    private VideoPlaylistManager CreateManager()
    {
        return new(
            mediaContext: new MediaContext(),
            movieRepository: _movieRepository.Object,
            collectionRepository: _collectionRepository.Object,
            specialRepository: _specialRepository.Object,
            tvShowRepository: _tvShowRepository.Object
        );
    }

    private static Movie BuildMovie(int id, UserData? userData = null)
    {
        Movie movie = new()
        {
            Id = id,
            Title = $"Movie {id}",
            TitleSort = $"movie {id}",
            Overview = "Overview",
            ReleaseDate = new DateTime(year: 2020, month: 1, day: 1),
        };
        VideoFile file = new()
        {
            Filename = $"movie-{id}.mkv",
            Folder = "/movies",
            HostFolder = "/movies",
            Languages = "[\"en\"]",
            Quality = "1080p",
            Share = "movies",
            MovieId = id,
            Movie = movie,
        };
        if (userData is not null)
            file.UserData.Add(item: userData);
        movie.VideoFiles.Add(item: file);
        return movie;
    }

    private static Episode BuildEpisode(int id, Tv tv, Season season, UserData? userData = null)
    {
        Episode episode = new()
        {
            Id = id,
            Title = $"Episode {id}",
            EpisodeNumber = id,
            SeasonNumber = season.SeasonNumber,
            TvId = tv.Id,
            Tv = tv,
            SeasonId = season.Id,
            Season = season,
        };
        VideoFile file = new()
        {
            Filename = $"episode-{id}.mkv",
            Folder = "/tv",
            HostFolder = "/tv",
            Languages = "[\"en\"]",
            Quality = "1080p",
            Share = "tv",
            EpisodeId = id,
            Episode = episode,
        };
        if (userData is not null)
            file.UserData.Add(item: userData);
        episode.VideoFiles.Add(item: file);
        return episode;
    }

    private static UserData ProgressAt(DateTime playedAt)
    {
        return new()
        {
            Type = "progress",
            Time = 120,
            LastPlayedDate = playedAt.ToString(format: "O"),
        };
    }

    [Fact]
    public async Task GetPlaylist_UnknownType_ThrowsArgumentException()
    {
        VideoPlaylistManager manager = CreateManager();

        Func<Task> act = async () =>
            await manager.GetPlaylist(userId: Guid.NewGuid(), type: "not-a-real-type", listId: "1", itemId: null, language: "en", country: "US");

        (await act.Should().ThrowAsync<ArgumentException>()).WithParameterName(paramName: "type");
    }

    [Fact]
    public async Task GetPlaylist_MovieType_ParsesStringListId_AndStampsIntPlaylistId()
    {
        Movie movie = BuildMovie(id: 42);
        _movieRepository
            .Setup(expression: r => r.GetMoviePlaylistAsync(It.IsAny<Guid>(), 42, "en", "US", default))
            .ReturnsAsync(value: [movie]);
        VideoPlaylistManager manager = CreateManager();

        (VideoPlaylistResponseDto? item, List<VideoPlaylistResponseDto> playlist) =
            await manager.GetPlaylist(userId: Guid.NewGuid(), type: "movie", listId: "42", itemId: null, language: "en", country: "US");

        playlist.Should().ContainSingle();
        item.Should().NotBeNull();
        item!.Id.Should().Be(expected: 42);
        // The manager re-parses the string listId to an int before handing it to
        // the DTO as the dynamic "playlist_id" -- movies serialize that field as
        // a JSON number, unlike tv/collection/special (see tests below).
        ((object)item.PlaylistId)
            .Should()
            .Be(expected: 42);
    }

    [Fact]
    public async Task GetPlaylist_MovieType_WithNonStringListId_ThrowsRuntimeBinderException()
    {
        // GetPlaylist's listId is `dynamic` precisely so SignalR/JSON callers can
        // hand it through untouched, but every branch immediately calls
        // int.Parse/Ulid.Parse(listId) against it. If a caller ever passes an
        // already-numeric dynamic (e.g. a raw int from a different transport),
        // dynamic overload resolution has no int.Parse(int) to bind to and blows
        // up at the call site instead of the type mismatch being caught early.
        // Locking this in so a future refactor of the parse call doesn't
        // silently start accepting ints while pretending strings still work.
        _movieRepository
            .Setup(expression: r =>
                r.GetMoviePlaylistAsync(It.IsAny<Guid>(), It.IsAny<int>(), "en", "US", default)
            )
            .ReturnsAsync(value: []);
        VideoPlaylistManager manager = CreateManager();
        dynamic nonStringListId = 42;

        Func<Task> act = async () =>
            await manager.GetPlaylist(userId: Guid.NewGuid(), type: "movie", listId: nonStringListId, itemId: null, language: "en", country: "US");

        await act.Should().ThrowAsync<Microsoft.CSharp.RuntimeBinder.RuntimeBinderException>();
    }

    [Fact]
    public async Task GetPlaylist_TvType_PreservesOriginalListIdOnPlaylistId()
    {
        Tv tv = new()
        {
            Id = 1399,
            Title = "Breaking Bad",
            TitleSort = "breaking bad",
            FirstAirDate = new DateTime(year: 2008, month: 1, day: 20),
        };
        Season season = new()
        {
            Id = 1,
            SeasonNumber = 1,
            TvId = tv.Id,
            Tv = tv,
        };
        tv.Seasons.Add(item: season);
        Episode episode = BuildEpisode(id: 1, tv: tv, season: season);
        season.Episodes.Add(item: episode);

        _tvShowRepository
            .Setup(expression: r => r.GetPlaylistAsync(It.IsAny<Guid>(), 1399, "en", "US", default))
            .ReturnsAsync(value: tv);
        VideoPlaylistManager manager = CreateManager();

        (VideoPlaylistResponseDto? item, List<VideoPlaylistResponseDto> playlist) =
            await manager.GetPlaylist(userId: Guid.NewGuid(), type: "tv", listId: "1399", itemId: null, language: "en", country: "US");

        playlist.Should().ContainSingle();
        item.Should().NotBeNull();
        // Unlike movie, tv/collection/special hand the ORIGINAL dynamic value
        // straight through as playlist_id -- here that is still the string.
        ((object)item!.PlaylistId)
            .Should()
            .Be(expected: "1399");
    }

    [Fact]
    public async Task GetPlaylist_TvType_OrdersRegularSeasonsBeforeSpecialsSeason()
    {
        Tv tv = new()
        {
            Id = 5,
            Title = "Show",
            TitleSort = "show",
        };
        Season regular = new()
        {
            Id = 1,
            SeasonNumber = 1,
            TvId = tv.Id,
            Tv = tv,
        };
        Season extras = new()
        {
            Id = 2,
            SeasonNumber = 0,
            TvId = tv.Id,
            Tv = tv,
        };
        Episode regularEpisode = BuildEpisode(id: 10, tv: tv, season: regular);
        Episode extraEpisode = BuildEpisode(id: 11, tv: tv, season: extras);
        regular.Episodes.Add(item: regularEpisode);
        extras.Episodes.Add(item: extraEpisode);
        // Extras season deliberately added to Seasons BEFORE the regular season
        // to prove the split is by SeasonNumber, not by insertion order.
        tv.Seasons.Add(item: extras);
        tv.Seasons.Add(item: regular);

        _tvShowRepository
            .Setup(expression: r => r.GetPlaylistAsync(It.IsAny<Guid>(), 5, "en", "US", default))
            .ReturnsAsync(value: tv);
        VideoPlaylistManager manager = CreateManager();

        (_, List<VideoPlaylistResponseDto> playlist) = await manager.GetPlaylist(
            userId: Guid.NewGuid(),
            type: "tv",
            listId: "5",
            itemId: null,
            language: "en",
            country: "US"
        );

        playlist.Select(selector: p => p.Id).Should().Equal(elements: [10, 11]);
    }

    [Fact]
    public async Task GetPlaylist_TvType_ItemIdMatch_SelectsThatEpisodeRegardlessOfProgress()
    {
        Tv tv = new()
        {
            Id = 7,
            Title = "Show",
            TitleSort = "show",
        };
        Season season = new()
        {
            Id = 1,
            SeasonNumber = 1,
            TvId = tv.Id,
            Tv = tv,
        };
        Episode first = BuildEpisode(id: 20, tv: tv, season: season, userData: ProgressAt(playedAt: new DateTime(year: 2026, month: 1, day: 1)));
        Episode second = BuildEpisode(id: 21, tv: tv, season: season);
        season.Episodes.Add(item: first);
        season.Episodes.Add(item: second);
        tv.Seasons.Add(item: season);

        _tvShowRepository
            .Setup(expression: r => r.GetPlaylistAsync(It.IsAny<Guid>(), 7, "en", "US", default))
            .ReturnsAsync(value: tv);
        VideoPlaylistManager manager = CreateManager();

        (VideoPlaylistResponseDto? item, _) = await manager.GetPlaylist(
            userId: Guid.NewGuid(),
            type: "tv",
            listId: "7",
            itemId: 21,
            language: "en",
            country: "US"
        );

        item.Should().NotBeNull();
        item!.Id.Should().Be(expected: 21);
    }

    [Fact]
    public async Task GetPlaylist_TvType_NoItemIdMatch_FallsBackToMostRecentProgress()
    {
        Tv tv = new()
        {
            Id = 8,
            Title = "Show",
            TitleSort = "show",
        };
        Season season = new()
        {
            Id = 1,
            SeasonNumber = 1,
            TvId = tv.Id,
            Tv = tv,
        };
        Episode older = BuildEpisode(id: 30, tv: tv, season: season, userData: ProgressAt(playedAt: new DateTime(year: 2025, month: 1, day: 1)));
        Episode newer = BuildEpisode(id: 31, tv: tv, season: season, userData: ProgressAt(playedAt: new DateTime(year: 2026, month: 6, day: 1)));
        Episode noProgress = BuildEpisode(id: 32, tv: tv, season: season);
        season.Episodes.Add(item: older);
        season.Episodes.Add(item: newer);
        season.Episodes.Add(item: noProgress);
        tv.Seasons.Add(item: season);

        _tvShowRepository
            .Setup(expression: r => r.GetPlaylistAsync(It.IsAny<Guid>(), 8, "en", "US", default))
            .ReturnsAsync(value: tv);
        VideoPlaylistManager manager = CreateManager();

        (VideoPlaylistResponseDto? item, _) = await manager.GetPlaylist(
            userId: Guid.NewGuid(),
            type: "tv",
            listId: "8",
            itemId: 999,
            language: "en",
            country: "US"
        );

        item.Should().NotBeNull();
        item!.Id.Should().Be(expected: 31, because: "the episode with the latest LastPlayedDate must win");
    }

    [Fact]
    public async Task GetPlaylist_TvType_NoItemIdMatchAndNoProgress_FallsBackToFirst()
    {
        Tv tv = new()
        {
            Id = 9,
            Title = "Show",
            TitleSort = "show",
        };
        Season season = new()
        {
            Id = 1,
            SeasonNumber = 1,
            TvId = tv.Id,
            Tv = tv,
        };
        Episode first = BuildEpisode(id: 40, tv: tv, season: season);
        Episode second = BuildEpisode(id: 41, tv: tv, season: season);
        season.Episodes.Add(item: first);
        season.Episodes.Add(item: second);
        tv.Seasons.Add(item: season);

        _tvShowRepository
            .Setup(expression: r => r.GetPlaylistAsync(It.IsAny<Guid>(), 9, "en", "US", default))
            .ReturnsAsync(value: tv);
        VideoPlaylistManager manager = CreateManager();

        (VideoPlaylistResponseDto? item, _) = await manager.GetPlaylist(
            userId: Guid.NewGuid(),
            type: "tv",
            listId: "9",
            itemId: 999,
            language: "en",
            country: "US"
        );

        item.Should().NotBeNull();
        item!.Id.Should().Be(expected: 40);
    }

    [Fact]
    public async Task GetPlaylist_TvType_RepositoryReturnsNull_YieldsEmptyPlaylistAndNullItem()
    {
        _tvShowRepository
            .Setup(expression: r => r.GetPlaylistAsync(It.IsAny<Guid>(), 123, "en", "US", default))
            .ReturnsAsync(value: (Tv?)null);
        VideoPlaylistManager manager = CreateManager();

        (VideoPlaylistResponseDto? item, List<VideoPlaylistResponseDto> playlist) =
            await manager.GetPlaylist(userId: Guid.NewGuid(), type: "tv", listId: "123", itemId: null, language: "en", country: "US");

        item.Should().BeNull();
        playlist.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPlaylist_CollectionType_MapsMoviesWithOneBasedIndexAndCollectionId()
    {
        Collection collection = new() { Id = 500, Title = "Franchise" };
        Movie movieA = BuildMovie(id: 1);
        Movie movieB = BuildMovie(id: 2);
        collection.CollectionMovies.Add(item: new() { Collection = collection, Movie = movieA });
        collection.CollectionMovies.Add(item: new() { Collection = collection, Movie = movieB });

        _collectionRepository
            .Setup(expression: r => r.GetCollectionPlaylistAsync(It.IsAny<Guid>(), 500, "en", "US", default))
            .ReturnsAsync(value: collection);
        VideoPlaylistManager manager = CreateManager();

        (_, List<VideoPlaylistResponseDto> playlist) = await manager.GetPlaylist(
            userId: Guid.NewGuid(),
            type: "collection",
            listId: "500",
            itemId: null,
            language: "en",
            country: "US"
        );

        playlist.Should().HaveCount(expected: 2);
        playlist.Select(selector: p => p.Episode).Should().Equal(elements: [1, 2]);
        playlist.Select(selector: p => p.TmdbId).Should().AllBeEquivalentTo(expectation: 500);
        ((object)playlist[index: 0].PlaylistId).Should().Be(expected: "500");
    }

    [Fact]
    public async Task GetPlaylist_CollectionType_NoItemIdMatch_FallsBackToMostRecentProgress()
    {
        Collection collection = new() { Id = 501, Title = "Franchise" };
        Movie noProgress = BuildMovie(id: 100);
        Movie older = BuildMovie(id: 101, userData: ProgressAt(playedAt: new DateTime(year: 2025, month: 1, day: 1)));
        Movie newer = BuildMovie(id: 102, userData: ProgressAt(playedAt: new DateTime(year: 2026, month: 6, day: 1)));
        collection.CollectionMovies.Add(item: new() { Collection = collection, Movie = noProgress });
        collection.CollectionMovies.Add(item: new() { Collection = collection, Movie = older });
        collection.CollectionMovies.Add(item: new() { Collection = collection, Movie = newer });

        _collectionRepository
            .Setup(expression: r => r.GetCollectionPlaylistAsync(It.IsAny<Guid>(), 501, "en", "US", default))
            .ReturnsAsync(value: collection);
        VideoPlaylistManager manager = CreateManager();

        (VideoPlaylistResponseDto? item, _) = await manager.GetPlaylist(
            userId: Guid.NewGuid(),
            type: "collection",
            listId: "501",
            itemId: 999,
            language: "en",
            country: "US"
        );

        item.Should().NotBeNull();
        item!.Id.Should().Be(expected: 102, because: "the movie with the latest LastPlayedDate must win");
    }

    [Fact]
    public async Task GetPlaylist_SpecialType_EpisodeItem_UsesEpisodeConstructorBranch()
    {
        Ulid specialId = Ulid.NewUlid();
        Special special = new() { Id = specialId, Title = "Special" };
        Tv tv = new()
        {
            Id = 60,
            Title = "Show",
            TitleSort = "show",
        };
        Season season = new()
        {
            Id = 1,
            SeasonNumber = 1,
            TvId = tv.Id,
            Tv = tv,
        };
        Episode episode = BuildEpisode(id: 70, tv: tv, season: season);
        SpecialItem specialItem = new()
        {
            Order = 0,
            SpecialId = specialId,
            Special = special,
            EpisodeId = episode.Id,
            Episode = episode,
        };
        special.Items.Add(item: specialItem);

        _specialRepository
            .Setup(expression: r => r.GetSpecialPlaylistAsync(It.IsAny<Guid>(), specialId, "en", "US", default))
            .ReturnsAsync(value: special);
        VideoPlaylistManager manager = CreateManager();

        (VideoPlaylistResponseDto? item, List<VideoPlaylistResponseDto> playlist) =
            await manager.GetPlaylist(
                userId: Guid.NewGuid(),
                type: "specials",
                listId: specialId.ToString(),
                itemId: null,
                language: "en",
                country: "US"
            );

        playlist.Should().ContainSingle();
        item.Should().NotBeNull();
        item!.Id.Should().Be(expected: 70);
        item.VideoType.Should().Be(expected: "tv");
    }

    [Fact]
    public async Task GetPlaylist_SpecialType_MovieItem_UsesMovieConstructorBranch()
    {
        Ulid specialId = Ulid.NewUlid();
        Special special = new() { Id = specialId, Title = "Special" };
        Movie movie = BuildMovie(id: 80);
        SpecialItem specialItem = new()
        {
            Order = 0,
            SpecialId = specialId,
            Special = special,
            MovieId = movie.Id,
            Movie = movie,
        };
        special.Items.Add(item: specialItem);

        _specialRepository
            .Setup(expression: r => r.GetSpecialPlaylistAsync(It.IsAny<Guid>(), specialId, "en", "US", default))
            .ReturnsAsync(value: special);
        VideoPlaylistManager manager = CreateManager();

        (VideoPlaylistResponseDto? item, List<VideoPlaylistResponseDto> playlist) =
            await manager.GetPlaylist(
                userId: Guid.NewGuid(),
                type: "specials",
                listId: specialId.ToString(),
                itemId: null,
                language: "en",
                country: "US"
            );

        playlist.Should().ContainSingle();
        item.Should().NotBeNull();
        item!.Id.Should().Be(expected: 80);
        item.VideoType.Should().Be(expected: "movie");
    }

    [Fact]
    public async Task GetPlaylist_SpecialType_NoItemIdMatch_FallsBackToMostRecentProgress()
    {
        Ulid specialId = Ulid.NewUlid();
        Special special = new() { Id = specialId, Title = "Special" };
        Movie noProgress = BuildMovie(id: 200);
        Movie older = BuildMovie(id: 201, userData: ProgressAt(playedAt: new DateTime(year: 2025, month: 1, day: 1)));
        Movie newer = BuildMovie(id: 202, userData: ProgressAt(playedAt: new DateTime(year: 2026, month: 6, day: 1)));
        special.Items.Add(
            item: new()
            {
                Order = 0,
                SpecialId = specialId,
                Special = special,
                MovieId = noProgress.Id,
                Movie = noProgress,
            }
        );
        special.Items.Add(
            item: new()
            {
                Order = 1,
                SpecialId = specialId,
                Special = special,
                MovieId = older.Id,
                Movie = older,
            }
        );
        special.Items.Add(
            item: new()
            {
                Order = 2,
                SpecialId = specialId,
                Special = special,
                MovieId = newer.Id,
                Movie = newer,
            }
        );

        _specialRepository
            .Setup(expression: r => r.GetSpecialPlaylistAsync(It.IsAny<Guid>(), specialId, "en", "US", default))
            .ReturnsAsync(value: special);
        VideoPlaylistManager manager = CreateManager();

        (VideoPlaylistResponseDto? item, _) = await manager.GetPlaylist(
            userId: Guid.NewGuid(),
            type: "specials",
            listId: specialId.ToString(),
            itemId: 999,
            language: "en",
            country: "US"
        );

        item.Should().NotBeNull();
        item!.Id.Should().Be(expected: 202, because: "the item with the latest LastPlayedDate must win");
    }

    [Fact]
    public async Task GetPlaylist_SpecialType_OrdersItemsByOrderField()
    {
        Ulid specialId = Ulid.NewUlid();
        Special special = new() { Id = specialId, Title = "Special" };
        Movie first = BuildMovie(id: 90);
        Movie second = BuildMovie(id: 91);
        // Deliberately added out of Order sequence.
        special.Items.Add(
            item: new()
            {
                Order = 1,
                SpecialId = specialId,
                Special = special,
                MovieId = second.Id,
                Movie = second,
            }
        );
        special.Items.Add(
            item: new()
            {
                Order = 0,
                SpecialId = specialId,
                Special = special,
                MovieId = first.Id,
                Movie = first,
            }
        );

        _specialRepository
            .Setup(expression: r => r.GetSpecialPlaylistAsync(It.IsAny<Guid>(), specialId, "en", "US", default))
            .ReturnsAsync(value: special);
        VideoPlaylistManager manager = CreateManager();

        (_, List<VideoPlaylistResponseDto> playlist) = await manager.GetPlaylist(
            userId: Guid.NewGuid(),
            type: "specials",
            listId: specialId.ToString(),
            itemId: null,
            language: "en",
            country: "US"
        );

        playlist.Select(selector: p => p.Id).Should().Equal(elements: [90, 91]);
    }

    [Theory]
    [InlineData(data: [0, new int[] { }, new int[] { 21, 22 }])]
    [InlineData(data: [1, new int[] { 20 }, new int[] { 22 }])]
    [InlineData(data: [2, new int[] { 20, 21 }, new int[] { }])]
    public void SplitPlaylist_SplitsAroundTheCurrentTrack(
        int currentIndex,
        int[] expectedBefore,
        int[] expectedAfter
    )
    {
        VideoPlaylistManager manager = CreateManager();
        List<VideoPlaylistResponseDto> playlist =
        [
            new() { Id = 20 },
            new() { Id = 21 },
            new() { Id = 22 },
        ];

        (List<VideoPlaylistResponseDto> before, List<VideoPlaylistResponseDto> after) =
            manager.SplitPlaylist(playlist: playlist, currentTrackId: playlist[index: currentIndex].Id);

        before.Select(selector: p => p.Id).Should().Equal(elements: expectedBefore);
        after.Select(selector: p => p.Id).Should().Equal(elements: expectedAfter);
    }

    [Fact]
    public void SplitPlaylist_CurrentTrackNotInPlaylist_ReturnsEmptyBeforeAndFullAfter()
    {
        VideoPlaylistManager manager = CreateManager();
        List<VideoPlaylistResponseDto> playlist = [new() { Id = 1 }, new() { Id = 2 }];

        (List<VideoPlaylistResponseDto> before, List<VideoPlaylistResponseDto> after) =
            manager.SplitPlaylist(playlist: playlist, currentTrackId: 999);

        before.Should().BeEmpty();
        after.Should().BeEquivalentTo(expectation: playlist, config: opts => opts.WithStrictOrdering());
    }
}
