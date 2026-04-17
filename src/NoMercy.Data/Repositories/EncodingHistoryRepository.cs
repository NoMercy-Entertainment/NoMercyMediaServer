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

    public async Task<bool> DeleteAsync(Ulid id)
    {
        EncodingHistory? existing = await context.EncodingHistory.FirstOrDefaultAsync(h =>
            h.Id == id
        );
        if (existing is null)
            return false;

        context.EncodingHistory.Remove(existing);
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Bulk purge every row strictly older than <paramref name="olderThan"/>.
    /// Users clean up the dashboard view; the orchestrator keeps writing
    /// fresh rows. Returns the removed-row count so the API response can
    /// surface it.
    /// </summary>
    public Task<int> DeleteOlderThanAsync(DateTime olderThan) =>
        context.EncodingHistory.Where(h => h.CreatedAt < olderThan).ExecuteDeleteAsync();

    public Task<int> DeleteAllAsync() => context.EncodingHistory.ExecuteDeleteAsync();
}
