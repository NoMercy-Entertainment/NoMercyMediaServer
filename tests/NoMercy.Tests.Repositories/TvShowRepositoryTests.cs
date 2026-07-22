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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.TvShows;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Characterization")]
public class TvShowRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly IDbContextFactory<MediaContext> _factory;
    private readonly SqliteConnection _connection;
    private readonly TvShowRepository _repository;

    public TvShowRepositoryTests()
    {
        (_factory, _connection) = TestMediaContextFactory.CreateSeededFactory();
        _context = _factory.CreateDbContext();
        _repository = new(contextFactory: _factory);
    }

    [Fact]
    public async Task GetTvAvailableAsync_ReturnsTrue_WhenShowHasVideoFiles()
    {
        bool available = await _repository.GetTvAvailableAsync(userId: SeedConstants.UserId, id: 1399);

        Assert.True(condition: available);
    }

    [Fact]
    public async Task GetTvAvailableAsync_ReturnsFalse_WhenUserHasNoAccess()
    {
        bool available = await _repository.GetTvAvailableAsync(userId: SeedConstants.OtherUserId, id: 1399);

        Assert.False(condition: available);
    }

    [Fact]
    public async Task GetTvAvailableAsync_ReturnsFalse_WhenShowDoesNotExist()
    {
        bool available = await _repository.GetTvAvailableAsync(userId: SeedConstants.UserId, id: 999999);

        Assert.False(condition: available);
    }

    [Fact]
    public async Task GetTvPlaylistAsync_ReturnsShowWithSeasons()
    {
        Tv? playlist = await _repository.GetPlaylistAsync(userId: SeedConstants.UserId, id: 1399, language: "en", country: "US");

        Assert.NotNull(@object: playlist);
        Assert.Equal(expected: 1399, actual: playlist.Id);
        Assert.Equal(expected: "Breaking Bad", actual: playlist.Title);
        Assert.NotEmpty(collection: playlist.Seasons);
    }

    [Fact]
    public async Task GetTvPlaylistAsync_ReturnsNull_WhenUserHasNoAccess()
    {
        Tv? playlist = await _repository.GetPlaylistAsync(
            userId: SeedConstants.OtherUserId,
            id: 1399,
            language: "en",
            country: "US"
        );

        Assert.Null(@object: playlist);
    }

    [Fact]
    public async Task GetTvPlaylistAsync_IncludesEpisodesWithVideoFiles()
    {
        Tv? playlist = await _repository.GetPlaylistAsync(userId: SeedConstants.UserId, id: 1399, language: "en", country: "US");

        Assert.NotNull(@object: playlist);
        Season season = Assert.Single(collection: playlist.Seasons);
        Assert.Equal(expected: 2, actual: season.Episodes.Count);
        Assert.All(collection: season.Episodes, action: e => Assert.NotEmpty(collection: e.VideoFiles));
    }

    [Fact]
    public async Task DeleteTvAsync_RemovesShow()
    {
        await _repository.DeleteAsync(id: 1399);

        bool available = await _repository.GetTvAvailableAsync(userId: SeedConstants.UserId, id: 1399);
        Assert.False(condition: available);
    }

    [Fact]
    public async Task GetMissingLibraryShows_ReturnsEpisodesWithoutVideoFiles()
    {
        Episode episodeWithoutVideo = new()
        {
            Id = 62087,
            Title = "...And the Bag's in the River",
            EpisodeNumber = 3,
            SeasonNumber = 1,
            TvId = 1399,
            SeasonId = 3572,
        };
        _context.Episodes.Add(entity: episodeWithoutVideo);
        await _context.SaveChangesAsync();

        IEnumerable<Episode> missing = await _repository.GetMissingLibraryShows(
            userId: SeedConstants.UserId,
            id: 1399,
            language: "en"
        );

        Assert.Single(collection: missing);
        Assert.Equal(expected: 62087, actual: missing.First().Id);
    }

    [Fact]
    public async Task LikeTvAsync_AddsTvUser_WhenLikeIsTrue()
    {
        bool result = await _repository.LikeAsync(id: 1399, userId: SeedConstants.UserId, like: true);

        Assert.True(condition: result);

        TvUser? tvUser = _context.TvUser.FirstOrDefault(predicate: tu =>
            tu.TvId == 1399 && tu.UserId == SeedConstants.UserId
        );
        Assert.NotNull(@object: tvUser);
    }

    [Fact]
    public async Task LikeTvAsync_RemovesTvUser_WhenLikeIsFalse()
    {
        await _repository.LikeAsync(id: 1399, userId: SeedConstants.UserId, like: true);
        bool result = await _repository.LikeAsync(id: 1399, userId: SeedConstants.UserId, like: false);

        Assert.True(condition: result);

        TvUser? tvUser = _context.TvUser.FirstOrDefault(predicate: tu =>
            tu.TvId == 1399 && tu.UserId == SeedConstants.UserId
        );
        Assert.Null(@object: tvUser);
    }

    #region GetTvAsync — Split Query Tests

    [Fact]
    public async Task GetTvAsync_ReturnsShowWithAllNavigationProperties()
    {
        SeedDetailData(context: _context);

        Tv? tv = (await _repository.GetTvAsync(userId: SeedConstants.UserId, id: 1399, language: "en", country: "US"))?.Tv;

        Assert.NotNull(@object: tv);
        Assert.Equal(expected: 1399, actual: tv.Id);
        Assert.Equal(expected: "Breaking Bad", actual: tv.Title);

        Assert.NotEmpty(collection: tv.Translations);
        Assert.NotEmpty(collection: tv.Images);
        Assert.NotEmpty(collection: tv.GenreTvs);
        Assert.NotEmpty(collection: tv.KeywordTvs);
        Assert.NotEmpty(collection: tv.Cast);
        Assert.NotEmpty(collection: tv.Crew);
        Assert.NotEmpty(collection: tv.Seasons);
        Assert.NotEmpty(collection: tv.RecommendationFrom);
        Assert.NotEmpty(collection: tv.SimilarFrom);
        Assert.NotEmpty(collection: tv.CertificationTvs);
        Assert.NotEmpty(collection: tv.Creators);
    }

    [Fact]
    public async Task GetTvAsync_MergesEpisodeCastCrewFromSplitQuery()
    {
        SeedDetailData(context: _context);

        Tv? tv = (await _repository.GetTvAsync(userId: SeedConstants.UserId, id: 1399, language: "en", country: "US"))?.Tv;

        Assert.NotNull(@object: tv);

        // Episode cast/crew should be populated via the second query
        Episode[] allEpisodes = tv.Episodes.ToArray();
        Assert.NotEmpty(collection: allEpisodes);
        Assert.True(
            condition: allEpisodes.Any(predicate: e => e.Cast.Count > 0),
            userMessage: "Episode-level cast should be populated from split query"
        );
        Assert.True(
            condition: allEpisodes.Any(predicate: e => e.Crew.Count > 0),
            userMessage: "Episode-level crew should be populated from split query"
        );

        // Verify cast has Person and Role loaded
        Cast episodeCast = allEpisodes.SelectMany(selector: e => e.Cast).First();
        Assert.NotNull(@object: episodeCast.Person);
        Assert.NotNull(@object: episodeCast.Role);

        // Verify crew has Person and Job loaded
        Crew episodeCrew = allEpisodes.SelectMany(selector: e => e.Crew).First();
        Assert.NotNull(@object: episodeCrew.Person);
        Assert.NotNull(@object: episodeCrew.Job);
    }

    [Fact]
    public async Task GetTvAsync_MergesEpisodeCastCrewIntoSeasonEpisodes()
    {
        SeedDetailData(context: _context);

        Tv? tv = (await _repository.GetTvAsync(userId: SeedConstants.UserId, id: 1399, language: "en", country: "US"))?.Tv;

        Assert.NotNull(@object: tv);

        // Season episodes should also have cast/crew merged
        Episode[] seasonEpisodes = tv.Seasons.SelectMany(selector: s => s.Episodes).ToArray();
        Assert.NotEmpty(collection: seasonEpisodes);
        Assert.True(
            condition: seasonEpisodes.Any(predicate: e => e.Cast.Count > 0),
            userMessage: "Season-level episode cast should be populated from split query"
        );
        Assert.True(
            condition: seasonEpisodes.Any(predicate: e => e.Crew.Count > 0),
            userMessage: "Season-level episode crew should be populated from split query"
        );
    }

    [Fact]
    public async Task GetTvAsync_ReturnsNull_WhenUserHasNoAccess()
    {
        TvDetail? detail = await _repository.GetTvAsync(
            userId: SeedConstants.OtherUserId,
            id: 1399,
            language: "en",
            country: "US"
        );

        Assert.Null(@object: detail);
    }

    [Fact]
    public async Task GetTvAsync_ReturnsNull_WhenShowDoesNotExist()
    {
        TvDetail? detail = await _repository.GetTvAsync(userId: SeedConstants.UserId, id: 999999, language: "en", country: "US");

        Assert.Null(@object: detail);
    }

    [Fact]
    public async Task GetTvAsync_IncludesShowLevelCastAndCrew()
    {
        SeedDetailData(context: _context);

        Tv? tv = (await _repository.GetTvAsync(userId: SeedConstants.UserId, id: 1399, language: "en", country: "US"))?.Tv;

        Assert.NotNull(@object: tv);

        // Show-level cast with Person and Role
        Assert.NotEmpty(collection: tv.Cast);
        Cast showCast = tv.Cast.First();
        Assert.NotNull(@object: showCast.Person);
        Assert.NotNull(@object: showCast.Role);

        // Show-level crew with Person and Job
        Assert.NotEmpty(collection: tv.Crew);
        Crew showCrew = tv.Crew.First();
        Assert.NotNull(@object: showCrew.Person);
        Assert.NotNull(@object: showCrew.Job);
    }

    [Fact]
    public async Task GetTvAsync_IncludesSeasonsWithEpisodesAndVideoFiles()
    {
        Tv? tv = (await _repository.GetTvAsync(userId: SeedConstants.UserId, id: 1399, language: "en", country: "US"))?.Tv;

        Assert.NotNull(@object: tv);
        Assert.NotEmpty(collection: tv.Seasons);
        Season season = tv.Seasons.First();
        Assert.NotEmpty(collection: season.Episodes);
        Assert.All(collection: season.Episodes, action: e => Assert.NotEmpty(collection: e.VideoFiles));
    }

    [Fact]
    public async Task GetTvAsync_GeneratesSplitQueries()
    {
        (
            IDbContextFactory<MediaContext> factory,
            SqlCaptureInterceptor interceptor,
            SqliteConnection connection
        ) = TestMediaContextFactory.CreateSeededFactoryWithInterceptor();
        TvShowRepository repo = new(contextFactory: factory);
        using (MediaContext seedCtx = factory.CreateDbContext())
        {
            SeedDetailData(context: seedCtx);
        }
        interceptor.Clear();

        await repo.GetTvAsync(userId: SeedConstants.UserId, id: 1399, language: "en", country: "US");

        // Should generate multiple SQL queries (split query behavior)
        Assert.True(
            condition: interceptor.CapturedSql.Count > 1,
            userMessage: $"Expected multiple split queries, got {interceptor.CapturedSql.Count}"
        );

        connection.Dispose();
    }

    #endregion

    private static void SeedDetailData(MediaContext context)
    {
        // Person
        Person person1 = new()
        {
            Id = 17419,
            Name = "Bryan Cranston",
            TitleSort = "cranston, bryan",
        };
        Person person2 = new()
        {
            Id = 84497,
            Name = "Vince Gilligan",
            TitleSort = "gilligan, vince",
        };
        context.People.AddRange(entities: [person1, person2]);

        // Role and Job
        Role role1 = new() { Character = "Walter White", EpisodeCount = 62 };
        Job job1 = new() { CreditId = "crew-1", Task = "Director" };
        context.Roles.Add(entity: role1);
        context.Jobs.Add(entity: job1);
        context.SaveChanges();

        // Show-level Cast and Crew
        context.Casts.Add(
            entity: new()
            {
                CreditId = "cast-tv-1",
                PersonId = 17419,
                RoleId = role1.Id,
                TvId = 1399,
            }
        );
        context.Crews.Add(
            entity: new()
            {
                CreditId = "crew-tv-1",
                PersonId = 84497,
                JobId = job1.Id,
                TvId = 1399,
            }
        );

        // Episode-level Cast and Crew
        Role episodeRole = new() { Character = "Walter White", EpisodeCount = 1 };
        Job episodeJob = new() { CreditId = "crew-ep-1", Task = "Writer" };
        context.Roles.Add(entity: episodeRole);
        context.Jobs.Add(entity: episodeJob);
        context.SaveChanges();

        context.Casts.Add(
            entity: new()
            {
                CreditId = "cast-ep-1",
                PersonId = 17419,
                RoleId = episodeRole.Id,
                EpisodeId = 62085,
            }
        );
        context.Crews.Add(
            entity: new()
            {
                CreditId = "crew-ep-2",
                PersonId = 84497,
                JobId = episodeJob.Id,
                EpisodeId = 62085,
            }
        );

        // Creator
        context.Creators.Add(entity: new() { PersonId = 84497, TvId = 1399 });

        // Translation
        context.Translations.Add(
            entity: new()
            {
                Iso6391 = "en",
                Iso31661 = "US",
                Title = "Breaking Bad",
                Overview = "A chemistry teacher diagnosed with lung cancer...",
                TvId = 1399,
            }
        );

        // Image
        context.Images.Add(
            entity: new()
            {
                FilePath = "/logo.png",
                Type = "logo",
                Iso6391 = "en",
                AspectRatio = 1.78,
                VoteAverage = 5.0,
                TvId = 1399,
            }
        );
        context.Images.Add(
            entity: new()
            {
                FilePath = "/backdrop.jpg",
                Type = "backdrop",
                Iso6391 = "en",
                AspectRatio = 1.78,
                VoteAverage = 5.0,
                TvId = 1399,
            }
        );

        // Keyword
        Keyword keyword = new() { Id = 10765, Name = "drug dealer" };
        context.Keywords.Add(entity: keyword);
        context.KeywordTv.Add(entity: new() { KeywordId = 10765, TvId = 1399 });

        // Certification
        Certification cert = new()
        {
            Iso31661 = "US",
            Rating = "TV-14",
            Meaning = "Parents Strongly Cautioned",
            Order = 3,
        };
        context.Certifications.Add(entity: cert);
        context.SaveChanges();
        context.CertificationTv.Add(entity: new() { CertificationId = cert.Id, TvId = 1399 });

        // Similar and Recommendation
        context.Similar.Add(
            entity: new()
            {
                MediaId = 9999,
                TvFromId = 1399,
                Title = "Better Call Saul",
            }
        );
        context.Recommendations.Add(
            entity: new()
            {
                MediaId = 9998,
                TvFromId = 1399,
                Title = "Ozark",
            }
        );

        context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _connection.Dispose();
    }
}
