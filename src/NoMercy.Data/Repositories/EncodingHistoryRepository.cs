using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;

namespace NoMercy.Data.Repositories;

/// <summary>
/// Append-only repository for encoding history entries. The encoder writes
/// one row per successful encode; the dashboard reads it paginated.
/// </summary>
public class EncodingHistoryRepository(MediaContext context)
{
    public Task AddAsync(EncodingHistory entry)
    {
        context.EncodingHistory.Add(entry);
        return context.SaveChangesAsync();
    }

    public Task<List<EncodingHistory>> GetRecentAsync(int pageSize = 50, int pageIndex = 0)
    {
        if (pageSize <= 0)
            pageSize = 50;
        if (pageIndex < 0)
            pageIndex = 0;

        return context
            .EncodingHistory.AsNoTracking()
            .OrderByDescending(h => h.CreatedAt)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<int> GetTotalCountAsync() => context.EncodingHistory.CountAsync();

    public Task<EncodingHistory?> GetByIdAsync(Ulid id) =>
        context.EncodingHistory.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id);
}
