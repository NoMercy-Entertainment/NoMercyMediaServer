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

namespace NoMercy.Tests.Service.TestHelpers;

/// <summary>
/// An <see cref="IDbContextFactory{TContext}"/> for <see cref="MediaContext"/>
/// backed by a single open in-memory SQLite connection, so every context handed
/// out shares the same schema/data instead of each getting its own throwaway
/// empty database. Production code only calls the
/// <c>Microsoft.EntityFrameworkCore.DbContextFactoryExtensions.CreateDbContextAsync</c>
/// extension method, which wraps this synchronous <see cref="CreateDbContext"/>
/// in a completed <see cref="Task"/> — implementing only the sync member is
/// sufficient and keeps this a real DB (not a mock of the type under test).
/// </summary>
public sealed class SqliteMediaContextFactory : IDbContextFactory<MediaContext>, IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public SqliteMediaContextFactory()
    {
        _connection = new(connectionString: "DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                connection: _connection,
                sqliteOptionsAction: o => o.UseQuerySplittingBehavior(querySplittingBehavior: QuerySplittingBehavior.SplitQuery)
            )
            .Options;

        using MediaContext ctx = new(options: _options);
        ctx.Database.EnsureCreated();
    }

    public MediaContext CreateDbContext() => new(options: _options);

    public ValueTask DisposeAsync()
    {
        _connection.Dispose();
        return ValueTask.CompletedTask;
    }
}
