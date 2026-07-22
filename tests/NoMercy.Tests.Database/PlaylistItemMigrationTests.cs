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
/// Proves the AddPlaylistItem + CorrectUserPlaylistToVideoOnlyContainer
/// migrations replay cleanly, in order, against a brand-new, on-disk SQLite
/// database (the full chain, not just these two migrations) and leave behind
/// exactly the video-only, separate-container shape the user playlist slice
/// depends on: a UserPlaylists table (its own container, distinct from the
/// music-only Playlist table) and a PlaylistItems table FK'd to it with five
/// foreign keys (cascade on UserPlaylist/Movie/Tv/Episode, restrict on
/// Special — no Track FK/column) and its indexes (four partial sparse-FK
/// indexes plus the UserPlaylistId+Order composite).
/// </summary>
public class PlaylistItemMigrationTests : IDisposable
{
    private readonly string _dbPath;

    public PlaylistItemMigrationTests()
    {
        _dbPath = Path.Combine(
            path1: Path.GetTempPath(),
            path2: $"nm_playlistitem_migration_{Guid.NewGuid():N}.db"
        );
    }

    [Fact]
    public void FreshDatabase_MigratesCleanly_AndCreatesPlaylistItemsAndUserPlaylistsTables()
    {
        DbContextOptionsBuilder<MediaContext> builder = new();
        builder.UseSqlite(connectionString: $"Data Source={_dbPath}");

        using MediaContext context = new(options: builder.Options);
        context.Database.Migrate();

        DbConnection connection = context.Database.GetDbConnection();
        connection.Open();

        using (DbCommand tableCmd = connection.CreateCommand())
        {
            tableCmd.CommandText =
                "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('PlaylistItems', 'UserPlaylists') ORDER BY name;";
            using DbDataReader reader = tableCmd.ExecuteReader();
            List<string> tableNames = [];
            while (reader.Read())
                tableNames.Add(item: reader.GetString(ordinal: 0));

            Assert.Equal(expected: ["PlaylistItems", "UserPlaylists"], actual: tableNames);
        }
    }

    [Fact]
    public void FreshDatabase_PlaylistItems_HasExpectedForeignKeys_NoTrackFk()
    {
        DbContextOptionsBuilder<MediaContext> builder = new();
        builder.UseSqlite(connectionString: $"Data Source={_dbPath}");

        using MediaContext context = new(options: builder.Options);
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
                string table = reader.GetString(ordinal: reader.GetOrdinal(name: "table"));
                string from = reader.GetString(ordinal: reader.GetOrdinal(name: "from"));
                string onDelete = reader.GetString(ordinal: reader.GetOrdinal(name: "on_delete"));
                foreignKeysByColumn[key: from] = (table, onDelete);
            }
        }

        Assert.Equal(expected: 5, actual: foreignKeysByColumn.Count);

        Assert.Equal(expected: ("UserPlaylists", "CASCADE"), actual: foreignKeysByColumn[key: "UserPlaylistId"]);
        Assert.Equal(expected: ("Movies", "CASCADE"), actual: foreignKeysByColumn[key: "MovieId"]);
        Assert.Equal(expected: ("Tvs", "CASCADE"), actual: foreignKeysByColumn[key: "TvId"]);
        Assert.Equal(expected: ("Episodes", "CASCADE"), actual: foreignKeysByColumn[key: "EpisodeId"]);
        Assert.Equal(expected: ("Specials", "RESTRICT"), actual: foreignKeysByColumn[key: "SpecialId"]);

        Assert.DoesNotContain(expected: "TrackId", collection: foreignKeysByColumn.Keys);
        Assert.DoesNotContain(expected: "PlaylistId", collection: foreignKeysByColumn.Keys);
    }

    [Fact]
    public void FreshDatabase_PlaylistItems_HasExpectedIndexes_NoTrackIndex()
    {
        DbContextOptionsBuilder<MediaContext> builder = new();
        builder.UseSqlite(connectionString: $"Data Source={_dbPath}");

        using MediaContext context = new(options: builder.Options);
        context.Database.Migrate();

        DbConnection connection = context.Database.GetDbConnection();
        connection.Open();

        List<string> indexNames = [];
        using (DbCommand indexCmd = connection.CreateCommand())
        {
            indexCmd.CommandText = "PRAGMA index_list('PlaylistItems');";
            using DbDataReader reader = indexCmd.ExecuteReader();
            while (reader.Read())
                indexNames.Add(item: reader.GetString(ordinal: reader.GetOrdinal(name: "name")));
        }

        Assert.Contains(expected: "IX_PlaylistItems_UserPlaylistId_Order", collection: indexNames);
        Assert.Contains(expected: "IX_PlaylistItems_MovieId", collection: indexNames);
        Assert.Contains(expected: "IX_PlaylistItems_TvId", collection: indexNames);
        Assert.Contains(expected: "IX_PlaylistItems_EpisodeId", collection: indexNames);
        Assert.Contains(expected: "IX_PlaylistItems_SpecialId", collection: indexNames);

        Assert.DoesNotContain(expected: "IX_PlaylistItems_TrackId", collection: indexNames);
        Assert.DoesNotContain(expected: "IX_PlaylistItems_PlaylistId_Order", collection: indexNames);
    }

    [Fact]
    public void FreshDatabase_UserPlaylists_HasExpectedShape_SeparateFromMusicPlaylists()
    {
        DbContextOptionsBuilder<MediaContext> builder = new();
        builder.UseSqlite(connectionString: $"Data Source={_dbPath}");

        using MediaContext context = new(options: builder.Options);
        context.Database.Migrate();

        DbConnection connection = context.Database.GetDbConnection();
        connection.Open();

        // UserPlaylists is its own table — distinct from the music-only Playlists
        // table — so there is no shared container between the two features.
        using (DbCommand tableCmd = connection.CreateCommand())
        {
            tableCmd.CommandText =
                "SELECT name FROM sqlite_master WHERE type='table' AND name='Playlists';";
            object? tableName = tableCmd.ExecuteScalar();
            Assert.Equal(expected: "Playlists", actual: tableName);
        }

        Dictionary<string, (string Table, string OnDelete)> foreignKeysByColumn = new();
        using (DbCommand fkCmd = connection.CreateCommand())
        {
            fkCmd.CommandText = "PRAGMA foreign_key_list('UserPlaylists');";
            using DbDataReader reader = fkCmd.ExecuteReader();
            while (reader.Read())
            {
                string table = reader.GetString(ordinal: reader.GetOrdinal(name: "table"));
                string from = reader.GetString(ordinal: reader.GetOrdinal(name: "from"));
                string onDelete = reader.GetString(ordinal: reader.GetOrdinal(name: "on_delete"));
                foreignKeysByColumn[key: from] = (table, onDelete);
            }
        }

        Assert.Single(collection: foreignKeysByColumn);
        Assert.Equal(expected: ("Users", "RESTRICT"), actual: foreignKeysByColumn[key: "UserId"]);

        List<string> indexNames = [];
        using (DbCommand indexCmd = connection.CreateCommand())
        {
            indexCmd.CommandText = "PRAGMA index_list('UserPlaylists');";
            using DbDataReader reader = indexCmd.ExecuteReader();
            while (reader.Read())
                indexNames.Add(item: reader.GetString(ordinal: reader.GetOrdinal(name: "name")));
        }

        Assert.Contains(expected: "IX_UserPlaylists_UserId", collection: indexNames);
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools physical file handles across connections even
        // after Dispose(); on Windows the OS-level lock outlives the `using`
        // context/connection above, so deleting the file immediately fails with
        // "process cannot access the file" unless the pool is cleared first.
        SqliteConnection.ClearAllPools();

        if (File.Exists(path: _dbPath))
            File.Delete(path: _dbPath);
        GC.SuppressFinalize(obj: this);
    }
}
