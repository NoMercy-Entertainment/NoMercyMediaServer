using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;

namespace NoMercy.MediaProcessing.Images.Palettes;

/// <summary>
/// Reads and writes the backfill cursor and completion flag in the
/// <see cref="AppDbContext"/> Configuration table. Keys are prefixed with
/// "palette_backfill_" so they are isolated from other configuration entries.
/// </summary>
public class PaletteBackfillState
{
    private const string CompleteKey = "palette_backfill_complete";

    private static string CursorKey(string entityType) => $"palette_backfill_cursor_{entityType}";

    public static async Task<bool> IsCompleteAsync(AppDbContext db, CancellationToken ct)
    {
        string? value = await db
            .Configuration.Where(c => c.Key == CompleteKey)
            .Select(c => c.Value)
            .FirstOrDefaultAsync(ct);
        return value == "true";
    }

    public static async Task SetCompleteAsync(AppDbContext db, CancellationToken ct)
    {
        await UpsertConfigAsync(db, CompleteKey, "true", ct);
    }

    public static async Task<long> GetCursorAsync(
        AppDbContext db,
        string entityType,
        CancellationToken ct
    )
    {
        string key = CursorKey(entityType);
        string? value = await db
            .Configuration.Where(c => c.Key == key)
            .Select(c => c.Value)
            .FirstOrDefaultAsync(ct);
        return value is null ? 0L : long.Parse(value);
    }

    public static async Task SetCursorAsync(
        AppDbContext db,
        string entityType,
        long cursor,
        CancellationToken ct
    )
    {
        await UpsertConfigAsync(db, CursorKey(entityType), cursor.ToString(), ct);
    }

    private static async Task UpsertConfigAsync(
        AppDbContext db,
        string key,
        string value,
        CancellationToken ct
    )
    {
        Configuration? existing = await db.Configuration.FirstOrDefaultAsync(c => c.Key == key, ct);
        if (existing is null)
        {
            db.Configuration.Add(new Configuration { Key = key, Value = value });
        }
        else
        {
            existing.Value = value;
        }
        await db.SaveChangesAsync(ct);
    }
}
