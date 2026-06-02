using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Setup.Maintenance;

/// <summary>
/// Fills the music <c>TitleSort</c> column for rows imported before the column
/// existed. Runs deferred after boot, processes in batches, and is idempotent:
/// it only touches rows where <c>TitleSort</c> is still null and always writes a
/// non-null value, so a finished library is a single no-op query on later boots.
/// </summary>
public static class TitleSortBackfill
{
    private const int BatchSize = 1000;

    public static async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            int artists = await BackfillArtistsAsync(ct);
            int albums = await BackfillAlbumsAsync(ct);

            if (artists > 0 || albums > 0)
                Logger.Setup(
                    $"TitleSort backfill complete: {artists} artists, {albums} albums",
                    LogEventLevel.Information
                );
        }
        catch (Exception e)
        {
            Logger.Setup($"TitleSort backfill failed: {e.Message}", LogEventLevel.Warning);
        }
    }

    private static async Task<int> BackfillArtistsAsync(CancellationToken ct)
    {
        int total = 0;
        while (!ct.IsCancellationRequested)
        {
            await using MediaContext context = new();
            List<Artist> batch = await context
                .Artists.Where(artist => artist.TitleSort == null)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
                break;

            foreach (Artist artist in batch)
                artist.TitleSort = artist.Name.TitleSort();

            await context.SaveChangesAsync(ct);
            total += batch.Count;
        }

        return total;
    }

    private static async Task<int> BackfillAlbumsAsync(CancellationToken ct)
    {
        int total = 0;
        while (!ct.IsCancellationRequested)
        {
            await using MediaContext context = new();
            List<Album> batch = await context
                .Albums.Where(album => album.TitleSort == null)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
                break;

            foreach (Album album in batch)
                album.TitleSort = album.Name.TitleSort();

            await context.SaveChangesAsync(ct);
            total += batch.Count;
        }

        return total;
    }
}
