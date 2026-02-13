using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.TvShows;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Characterization")]
public class TvShowRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly TvShowRepository _repository;

    public TvShowRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new(_context);
    }

    [Fact]
    public async Task GetTvAvailableAsync_ReturnsTrue_WhenShowHasVideoFiles()
    {
        bool available = await _repository.GetTvAvailableAsync(SeedConstants.UserId, 1399);

        Assert.True(available);
    }

    [Fact]
    public async Task GetTvAvailableAsync_ReturnsFalse_WhenUserHasNoAccess()
    {
        bool available = await _repository.GetTvAvailableAsync(SeedConstants.OtherUserId, 1399);

        Assert.False(available);
    }

    [Fact]
    public async Task GetTvAvailableAsync_ReturnsFalse_WhenShowDoesNotExist()
    {
        bool available = await _repository.GetTvAvailableAsync(SeedConstants.UserId, 999999);

        Assert.False(available);
    }

    [Fact]
    public async Task GetTvPlaylistAsync_ReturnsShowWithSeasons()
    {
        Tv? playlist = await _repository.GetTvPlaylistAsync(
            SeedConstants.UserId, 1399, "en", "US");

        Assert.NotNull(playlist);
        Assert.Equal(1399, playlist.Id);
        Assert.Equal("Breaking Bad", playlist.Title);
        Assert.NotEmpty(playlist.Seasons);
    }

    [Fact]
    public async Task GetTvPlaylistAsync_ReturnsNull_WhenUserHasNoAccess()
    {
        Tv? playlist = await _repository.GetTvPlaylistAsync(
            SeedConstants.OtherUserId, 1399, "en", "US");

        Assert.Null(playlist);
    }

    [Fact]
    public async Task GetTvPlaylistAsync_IncludesEpisodesWithVideoFiles()
    {
        Tv? playlist = await _repository.GetTvPlaylistAsync(
            SeedConstants.UserId, 1399, "en", "US");

        Assert.NotNull(playlist);
        Season season = Assert.Single(playlist.Seasons);
        Assert.Equal(2, season.Episodes.Count);
        Assert.All(season.Episodes, e => Assert.NotEmpty(e.VideoFiles));
    }

    [Fact]
    public async Task DeleteTvAsync_RemovesShow()
    {
        await _repository.DeleteTvAsync(1399);

        bool available = await _repository.GetTvAvailableAsync(SeedConstants.UserId, 1399);
        Assert.False(available);
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
            SeasonId = 3572
        };
        _context.Episodes.Add(episodeWithoutVideo);
        await _context.SaveChangesAsync();

        IEnumerable<Episode> missing = await _repository.GetMissingLibraryShows(
            SeedConstants.UserId, 1399, "en");

        Assert.Single(missing);
        Assert.Equal(62087, missing.First().Id);
    }

    [Fact]
    public async Task LikeTvAsync_AddsTvUser_WhenLikeIsTrue()
    {
        bool result = await _repository.LikeTvAsync(1399, SeedConstants.UserId, true);

        Assert.True(result);

        TvUser? tvUser = _context.TvUser
            .FirstOrDefault(tu => tu.TvId == 1399 && tu.UserId == SeedConstants.UserId);
        Assert.NotNull(tvUser);
    }

    [Fact]
    public async Task LikeTvAsync_RemovesTvUser_WhenLikeIsFalse()
    {
        await _repository.LikeTvAsync(1399, SeedConstants.UserId, true);
        bool result = await _repository.LikeTvAsync(1399, SeedConstants.UserId, false);

        Assert.True(result);

        TvUser? tvUser = _context.TvUser
            .FirstOrDefault(tu => tu.TvId == 1399 && tu.UserId == SeedConstants.UserId);
        Assert.Null(tvUser);
    }

    #region GetTvAsync — Split Query Tests

    [Fact]
    public async Task GetTvAsync_ReturnsShowWithAllNavigationProperties()
    {
        SeedDetailData(_context);

        Tv? tv = await _repository.GetTvAsync(_context, SeedConstants.UserId, 1399, "en", "US");

        Assert.NotNull(tv);
        Assert.Equal(1399, tv.Id);
        Assert.Equal("Breaking Bad", tv.Title);

        Assert.NotEmpty(tv.Translations);
        Assert.NotEmpty(tv.Images);
        Assert.NotEmpty(tv.GenreTvs);
        Assert.NotEmpty(tv.KeywordTvs);
        Assert.NotEmpty(tv.Cast);
        Assert.NotEmpty(tv.Crew);
        Assert.NotEmpty(tv.Seasons);
        Assert.NotEmpty(tv.RecommendationFrom);
        Assert.NotEmpty(tv.SimilarFrom);
        Assert.NotEmpty(tv.CertificationTvs);
        Assert.NotEmpty(tv.Creators);
    }

    [Fact]
    public async Task GetTvAsync_MergesEpisodeCastCrewFromSplitQuery()
    {
        SeedDetailData(_context);

        Tv? tv = await _repository.GetTvAsync(_context, SeedConstants.UserId, 1399, "en", "US");

        Assert.NotNull(tv);

        // Episode cast/crew should be populated via the second query
        Episode[] allEpisodes = tv.Episodes.ToArray();
        Assert.NotEmpty(allEpisodes);
        Assert.True(allEpisodes.Any(e => e.Cast.Count > 0),
            "Episode-level cast should be populated from split query");
        Assert.True(allEpisodes.Any(e => e.Crew.Count > 0),
            "Episode-level crew should be populated from split query");

        // Verify cast has Person and Role loaded
        Cast episodeCast = allEpisodes.SelectMany(e => e.Cast).First();
        Assert.NotNull(episodeCast.Person);
        Assert.NotNull(episodeCast.Role);

        // Verify crew has Person and Job loaded
        Crew episodeCrew = allEpisodes.SelectMany(e => e.Crew).First();
        Assert.NotNull(episodeCrew.Person);
        Assert.NotNull(episodeCrew.Job);
    }

    [Fact]
    public async Task GetTvAsync_MergesEpisodeCastCrewIntoSeasonEpisodes()
    {
        SeedDetailData(_context);

        Tv? tv = await _repository.GetTvAsync(_context, SeedConstants.UserId, 1399, "en", "US");

        Assert.NotNull(tv);

        // Season episodes should also have cast/crew merged
        Episode[] seasonEpisodes = tv.Seasons.SelectMany(s => s.Episodes).ToArray();
        Assert.NotEmpty(seasonEpisodes);
        Assert.True(seasonEpisodes.Any(e => e.Cast.Count > 0),
            "Season-level episode cast should be populated from split query");
        Assert.True(seasonEpisodes.Any(e => e.Crew.Count > 0),
            "Season-level episode crew should be populated from split query");
    }

    [Fact]
    public async Task GetTvAsync_ReturnsNull_WhenUserHasNoAccess()
    {
        Tv? tv = await _repository.GetTvAsync(_context, SeedConstants.OtherUserId, 1399, "en", "US");

        Assert.Null(tv);
    }

    [Fact]
    public async Task GetTvAsync_ReturnsNull_WhenShowDoesNotExist()
    {
        Tv? tv = await _repository.GetTvAsync(_context, SeedConstants.UserId, 999999, "en", "US");

        Assert.Null(tv);
    }

    [Fact]
    public async Task GetTvAsync_IncludesShowLevelCastAndCrew()
    {
        SeedDetailData(_context);

        Tv? tv = await _repository.GetTvAsync(_context, SeedConstants.UserId, 1399, "en", "US");

        Assert.NotNull(tv);

        // Show-level cast with Person and Role
        Assert.NotEmpty(tv.Cast);
        Cast showCast = tv.Cast.First();
        Assert.NotNull(showCast.Person);
        Assert.NotNull(showCast.Role);

        // Show-level crew with Person and Job
        Assert.NotEmpty(tv.Crew);
        Crew showCrew = tv.Crew.First();
        Assert.NotNull(showCrew.Person);
        Assert.NotNull(showCrew.Job);
    }

    [Fact]
    public async Task GetTvAsync_IncludesSeasonsWithEpisodesAndVideoFiles()
    {
        Tv? tv = await _repository.GetTvAsync(_context, SeedConstants.UserId, 1399, "en", "US");

        Assert.NotNull(tv);
        Assert.NotEmpty(tv.Seasons);
        Season season = tv.Seasons.First();
        Assert.NotEmpty(season.Episodes);
        Assert.All(season.Episodes, e => Assert.NotEmpty(e.VideoFiles));
    }

    [Fact]
    public async Task GetTvAsync_GeneratesSplitQueries()
    {
        (MediaContext ctx, SqlCaptureInterceptor interceptor) =
            TestMediaContextFactory.CreateSeededContextWithInterceptor();
        TvShowRepository repo = new(ctx);
        SeedDetailData(ctx);
        interceptor.Clear();

        await repo.GetTvAsync(ctx, SeedConstants.UserId, 1399, "en", "US");

        // Should generate multiple SQL queries (split query behavior)
        Assert.True(interceptor.CapturedSql.Count > 1,
            $"Expected multiple split queries, got {interceptor.CapturedSql.Count}");

        ctx.Database.EnsureDeleted();
        ctx.Dispose();
    }

    #endregion

    private static void SeedDetailData(MediaContext context)
    {
        // Person
        Person person1 = new() { Id = 17419, Name = "Bryan Cranston", TitleSort = "cranston, bryan" };
        Person person2 = new() { Id = 84497, Name = "Vince Gilligan", TitleSort = "gilligan, vince" };
        context.People.AddRange(person1, person2);

        // Role and Job
        Role role1 = new() { Character = "Walter White", EpisodeCount = 62 };
        Job job1 = new() { CreditId = "crew-1", Task = "Director" };
        context.Roles.Add(role1);
        context.Jobs.Add(job1);
        context.SaveChanges();

        // Show-level Cast and Crew
        context.Casts.Add(new() { CreditId = "cast-tv-1", PersonId = 17419, RoleId = role1.Id, TvId = 1399 });
        context.Crews.Add(new() { CreditId = "crew-tv-1", PersonId = 84497, JobId = job1.Id, TvId = 1399 });

        // Episode-level Cast and Crew
        Role episodeRole = new() { Character = "Walter White", EpisodeCount = 1 };
        Job episodeJob = new() { CreditId = "crew-ep-1", Task = "Writer" };
        context.Roles.Add(episodeRole);
        context.Jobs.Add(episodeJob);
        context.SaveChanges();

        context.Casts.Add(new() { CreditId = "cast-ep-1", PersonId = 17419, RoleId = episodeRole.Id, EpisodeId = 62085 });
        context.Crews.Add(new() { CreditId = "crew-ep-2", PersonId = 84497, JobId = episodeJob.Id, EpisodeId = 62085 });

        // Creator
        context.Creators.Add(new() { PersonId = 84497, TvId = 1399 });

        // Translation
        context.Translations.Add(new()
        {
            Iso6391 = "en",
            Iso31661 = "US",
            Title = "Breaking Bad",
            Overview = "A chemistry teacher diagnosed with lung cancer...",
            TvId = 1399
        });

        // Image
        context.Images.Add(new()
        {
            FilePath = "/logo.png",
            Type = "logo",
            Iso6391 = "en",
            AspectRatio = 1.78,
            VoteAverage = 5.0,
            TvId = 1399
        });
        context.Images.Add(new()
        {
            FilePath = "/backdrop.jpg",
            Type = "backdrop",
            Iso6391 = "en",
            AspectRatio = 1.78,
            VoteAverage = 5.0,
            TvId = 1399
        });

        // Keyword
        Keyword keyword = new() { Id = 10765, Name = "drug dealer" };
        context.Keywords.Add(keyword);
        context.KeywordTv.Add(new() { KeywordId = 10765, TvId = 1399 });

        // Certification
        Certification cert = new() { Iso31661 = "US", Rating = "TV-14", Meaning = "Parents Strongly Cautioned", Order = 3 };
        context.Certifications.Add(cert);
        context.SaveChanges();
        context.CertificationTv.Add(new() { CertificationId = cert.Id, TvId = 1399 });

        // Similar and Recommendation
        context.Similar.Add(new() { MediaId = 9999, TvFromId = 1399, Title = "Better Call Saul" });
        context.Recommendations.Add(new() { MediaId = 9998, TvFromId = 1399, Title = "Ozark" });

        context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
