using NoMercy.Database.Models.Media;

namespace NoMercy.Data.Repositories;

public interface IEncodingHistoryRepository
{
    Task AddAsync(EncodingHistory entry);

    Task<List<EncodingHistory>> GetRecentAsync(int pageSize = 50, int pageIndex = 0);

    Task<int> GetTotalCountAsync();

    Task<EncodingHistory?> GetByIdAsync(Ulid id);

    Task<bool> DeleteAsync(Ulid id);

    Task<int> DeleteOlderThanAsync(DateTime olderThan);

    Task<int> DeleteAllAsync();

    Task<EncodingHistoryStats> GetAggregateStatsAsync();
}
