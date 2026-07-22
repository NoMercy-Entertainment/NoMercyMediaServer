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
    private const string VersionKey = "palette_backfill_version";

    private static string CursorKey(string entityType) => $"palette_backfill_cursor_{entityType}";

    /// <summary>
    /// Re-opens the one-shot backfill when a new entity type has been added since
    /// the library last drained. If the stored schema version is behind
    /// <paramref name="currentVersion"/>, the completion flag and every per-type
    /// cursor are reset so the drain walks all tables again — already-filled rows
    /// are skipped by the pending-palette filter, so only the new type does work.
    /// </summary>
    /// <returns><c>true</c> when the stored version was behind and the drain was
    /// re-opened, <c>false</c> when nothing changed.</returns>
    public static async Task<bool> EnsureVersionAsync(
        AppDbContext db,
        int currentVersion,
        IEnumerable<string> entityTypes,
        CancellationToken ct
    )
    {
        int stored = await GetVersionAsync(db: db, ct: ct);
        if (stored >= currentVersion)
            return false;

        await UpsertConfigAsync(db: db, key: CompleteKey, value: "false", ct: ct);
        foreach (string entityType in entityTypes)
            await UpsertConfigAsync(db: db, key: CursorKey(entityType: entityType), value: "0", ct: ct);
        await UpsertConfigAsync(db: db, key: VersionKey, value: currentVersion.ToString(), ct: ct);
        return true;
    }

    private static async Task<int> GetVersionAsync(AppDbContext db, CancellationToken ct)
    {
        string? value = await db
            .Configuration.Where(predicate: c => c.Key == VersionKey)
            .Select(selector: c => c.Value)
            .FirstOrDefaultAsync(cancellationToken: ct);
        return value is null ? 1 : int.Parse(s: value);
    }

    public static async Task<bool> IsCompleteAsync(AppDbContext db, CancellationToken ct)
    {
        string? value = await db
            .Configuration.Where(predicate: c => c.Key == CompleteKey)
            .Select(selector: c => c.Value)
            .FirstOrDefaultAsync(cancellationToken: ct);
        return value == "true";
    }

    public static async Task SetCompleteAsync(AppDbContext db, CancellationToken ct)
    {
        await UpsertConfigAsync(db: db, key: CompleteKey, value: "true", ct: ct);
    }

    public static async Task<long> GetCursorAsync(
        AppDbContext db,
        string entityType,
        CancellationToken ct
    )
    {
        string key = CursorKey(entityType: entityType);
        string? value = await db
            .Configuration.Where(predicate: c => c.Key == key)
            .Select(selector: c => c.Value)
            .FirstOrDefaultAsync(cancellationToken: ct);
        return value is null ? 0L : long.Parse(s: value);
    }

    public static async Task SetCursorAsync(
        AppDbContext db,
        string entityType,
        long cursor,
        CancellationToken ct
    )
    {
        await UpsertConfigAsync(db: db, key: CursorKey(entityType: entityType), value: cursor.ToString(), ct: ct);
    }

    private static async Task UpsertConfigAsync(
        AppDbContext db,
        string key,
        string value,
        CancellationToken ct
    )
    {
        Configuration? existing = await db.Configuration.FirstOrDefaultAsync(predicate: c => c.Key == key, cancellationToken: ct);
        if (existing is null)
        {
            db.Configuration.Add(entity: new() { Key = key, Value = value });
        }
        else
        {
            existing.Value = value;
        }
        await db.SaveChangesAsync(cancellationToken: ct);
    }
}
