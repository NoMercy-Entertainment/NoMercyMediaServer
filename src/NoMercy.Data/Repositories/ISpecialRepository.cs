using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;

namespace NoMercy.Data.Repositories;

public interface ISpecialRepository
{
    Task<List<Special>> GetSpecialsAsync(
        Guid userId,
        string language,
        int take,
        int page,
        CancellationToken ct = default
    );

    Task<List<SpecialCardDto>> GetSpecialCardsAsync(
        Guid userId,
        string language,
        int take,
        int page,
        CancellationToken ct = default
    );

    Task<List<SpecialCardDto>> GetSpecialItemCardsAsync(
        Guid userId,
        string language,
        string country,
        int take = 1,
        int page = 0,
        CancellationToken ct = default
    );

    Task<SpecialDetailDto?> GetSpecialDetailAsync(
        Guid userId,
        Ulid id,
        CancellationToken ct = default
    );

    Task<Special?> GetSpecialAsync(Guid userId, Ulid id, CancellationToken ct = default);

    Task<Special?> GetSpecialAvailableAsync(Guid userId, Ulid id);

    Task<List<Special>> GetSpecialItems(
        Guid userId,
        string? language,
        string country,
        int take = 1,
        int page = 0,
        CancellationToken ct = default
    );

    Task<Special?> GetSpecialPlaylistAsync(
        Guid userId,
        Ulid id,
        string language,
        string country,
        CancellationToken ct = default
    );

    Task<bool> AddToWatchListAsync(
        Ulid specialId,
        Guid userId,
        bool add = true,
        CancellationToken ct = default
    );
}
