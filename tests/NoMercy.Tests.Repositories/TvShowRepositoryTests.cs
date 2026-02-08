using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

public class TvShowRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly TvShowRepository _repository;

    public TvShowRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new TvShowRepository(_context);
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

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
