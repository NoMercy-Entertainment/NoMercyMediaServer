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

using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;

namespace NoMercy.Tests.Database;

/// <summary>
/// Proves the AddPlaylistItem migration replays cleanly against a brand-new,
/// on-disk SQLite database (the full chain, not just this one migration) and
/// leaves behind exactly the additive shape the mixed-media playlist slice
/// depends on: the PlaylistItems table, its six foreign keys (cascade on
/// Playlist/Movie/Tv/Episode, restrict on Track/Special), and its indexes
/// (five partial sparse-FK indexes plus the PlaylistId+Order composite).
/// </summary>
public class PlaylistItemMigrationTests : IDisposable
{
    private readonly string _dbPath;

    public PlaylistItemMigrationTests()
    {
        _dbPath = Path.Combine(
            Path.GetTempPath(),
            $"nm_playlistitem_migration_{Guid.NewGuid():N}.db"
        );
    }

    [Fact]
    public void FreshDatabase_MigratesCleanly_AndCreatesPlaylistItemsTable()
    {
        DbContextOptionsBuilder<MediaContext> builder = new();
        builder.UseSqlite($"Data Source={_dbPath}");

        using MediaContext context = new(builder.Options);
        context.Database.Migrate();

        DbConnection connection = context.Database.GetDbConnection();
        connection.Open();

        using (DbCommand tableCmd = connection.CreateCommand())
        {
            tableCmd.CommandText =
                "SELECT name FROM sqlite_master WHERE type='table' AND name='PlaylistItems';";
            object? tableName = tableCmd.ExecuteScalar();
            Assert.Equal("PlaylistItems", tableName);
        }
    }

    [Fact]
    public void FreshDatabase_PlaylistItems_HasExpectedForeignKeys()
    {
        DbContextOptionsBuilder<MediaContext> builder = new();
        builder.UseSqlite($"Data Source={_dbPath}");

        using MediaContext context = new(builder.Options);
        context.Database.Migrate();

        DbConnection connection = context.Database.GetDbConnection();
        connection.Open();

        Dictionary<string, (string Table, string OnDelete)> foreignKeysByColumn = new();
        using (DbCommand fkCmd = connection.CreateCommand())
        {
            fkCmd.CommandText = "PRAGMA foreign_key_list('PlaylistItems');";
            using DbDataReader reader = fkCmd.ExecuteReader();
            while (reader.Read())
            {
                string table = reader.GetString(reader.GetOrdinal("table"));
                string from = reader.GetString(reader.GetOrdinal("from"));
                string onDelete = reader.GetString(reader.GetOrdinal("on_delete"));
                foreignKeysByColumn[from] = (table, onDelete);
            }
        }

        Assert.Equal(6, foreignKeysByColumn.Count);

        Assert.Equal(("Playlists", "CASCADE"), foreignKeysByColumn["PlaylistId"]);
        Assert.Equal(("Movies", "CASCADE"), foreignKeysByColumn["MovieId"]);
        Assert.Equal(("Tvs", "CASCADE"), foreignKeysByColumn["TvId"]);
        Assert.Equal(("Episodes", "CASCADE"), foreignKeysByColumn["EpisodeId"]);
        Assert.Equal(("Tracks", "RESTRICT"), foreignKeysByColumn["TrackId"]);
        Assert.Equal(("Specials", "RESTRICT"), foreignKeysByColumn["SpecialId"]);
    }

    [Fact]
    public void FreshDatabase_PlaylistItems_HasExpectedIndexes()
    {
        DbContextOptionsBuilder<MediaContext> builder = new();
        builder.UseSqlite($"Data Source={_dbPath}");

        using MediaContext context = new(builder.Options);
        context.Database.Migrate();

        DbConnection connection = context.Database.GetDbConnection();
        connection.Open();

        List<string> indexNames = [];
        using (DbCommand indexCmd = connection.CreateCommand())
        {
            indexCmd.CommandText = "PRAGMA index_list('PlaylistItems');";
            using DbDataReader reader = indexCmd.ExecuteReader();
            while (reader.Read())
                indexNames.Add(reader.GetString(reader.GetOrdinal("name")));
        }

        Assert.Contains("IX_PlaylistItems_PlaylistId_Order", indexNames);
        Assert.Contains("IX_PlaylistItems_MovieId", indexNames);
        Assert.Contains("IX_PlaylistItems_TvId", indexNames);
        Assert.Contains("IX_PlaylistItems_EpisodeId", indexNames);
        Assert.Contains("IX_PlaylistItems_TrackId", indexNames);
        Assert.Contains("IX_PlaylistItems_SpecialId", indexNames);
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools physical file handles across connections even
        // after Dispose(); on Windows the OS-level lock outlives the `using`
        // context/connection above, so deleting the file immediately fails with
        // "process cannot access the file" unless the pool is cleared first.
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
        GC.SuppressFinalize(this);
    }
}
