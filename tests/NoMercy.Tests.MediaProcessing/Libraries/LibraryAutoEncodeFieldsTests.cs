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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Tests.MediaProcessing.Libraries;

/// <summary>
/// Pins the per-library auto-encode opt-in defaults: an existing (pre-slice)
/// library row must resolve <see cref="Library.AutoEncodeOnScan"/> to
/// <c>false</c> and <see cref="Library.EncodePresetId"/> to <c>null</c> so
/// self-hosted installs keep their current no-auto-encode behavior after the
/// migration runs.
/// </summary>
public class LibraryAutoEncodeFieldsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MediaContext _context;

    public LibraryAutoEncodeFieldsTests()
    {
        _connection = new(connectionString: "DataSource=:memory:");
        _connection.Open();

        using (SqliteCommand pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF;";
            pragma.ExecuteNonQuery();
        }

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection: _connection)
            .Options;

        _context = new(options: options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task NewLibrary_AfterSaveAndReload_DefaultsAutoEncodeOff()
    {
        Ulid libraryId = Ulid.NewUlid();
        _context.Libraries.Add(
            entity: new()
            {
                Id = libraryId,
                Title = "Test Movies",
                Type = "movie",
            }
        );
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        Library reloaded = await _context.Libraries.SingleAsync(predicate: l => l.Id == libraryId);

        reloaded.AutoEncodeOnScan.Should().BeFalse();
        reloaded.EncodePresetId.Should().BeNull();
    }
}
