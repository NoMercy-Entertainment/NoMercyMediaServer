using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Users;

namespace NoMercy.Data.Repositories;

public interface IHomeRepository
{
    Task<List<Genre>> GetHome(
        MediaContext mediaContext,
        Guid userId,
        string? language,
        int take,
        int page = 0
    );

    Task<List<HomeTvCardDto>> GetHomeTvs(
        MediaContext mediaContext,
        List<int> tvIds,
        string? language,
        string country,
        CancellationToken ct = default
    );

    Task<List<HomeMovieCardDto>> GetHomeMovies(
        MediaContext mediaContext,
        List<int> movieIds,
        string? language,
        string country,
        CancellationToken ct = default
    );

    Task<HashSet<UserData>> GetContinueWatchingAsync(
        MediaContext mediaContext,
        Guid userId,
        string language,
        string country,
        CancellationToken ct = default
    );

    Task<HashSet<Image>> GetScreensaverImagesAsync(
        MediaContext mediaContext,
        Guid userId,
        CancellationToken ct = default
    );

    Task<List<Library>> GetLibrariesAsync(
        MediaContext mediaContext,
        Guid userId,
        CancellationToken ct = default
    );

    Task<int> GetAnimeCountAsync(
        MediaContext mediaContext,
        Guid userId,
        CancellationToken ct = default
    );

    Task<int> GetMovieCountAsync(
        MediaContext mediaContext,
        Guid userId,
        CancellationToken ct = default
    );

    Task<int> GetTvCountAsync(
        MediaContext mediaContext,
        Guid userId,
        CancellationToken ct = default
    );

    Task<List<GenreHomeDto>> GetHomeGenresAsync(
        MediaContext mediaContext,
        Guid userId,
        string? language,
        int take,
        int page = 0,
        CancellationToken ct = default
    );
}
