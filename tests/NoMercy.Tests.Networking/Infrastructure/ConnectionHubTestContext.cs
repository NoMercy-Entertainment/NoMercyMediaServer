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

using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;

namespace NoMercy.Tests.Networking.Infrastructure;

/// <summary>
/// A real, migration-free (EnsureCreated) in-memory MediaContext factory so
/// ConnectionHub's Device-upsert path exercises real EF Core / SQLite
/// behavior instead of a fake repository. Each factory owns one shared-cache
/// in-memory connection kept open for the factory's lifetime, matching the
/// pattern EF's own DbContextFactory tests use for connection-scoped SQLite.
/// </summary>
public sealed class ConnectionHubTestDbContextFactory : IDbContextFactory<MediaContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public ConnectionHubTestDbContextFactory()
    {
        string dbName = Guid.NewGuid().ToString();
        _connection = new($"DataSource={dbName};Mode=Memory;Cache=Shared");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MediaContext>().UseSqlite(_connection).Options;

        using MediaContext init = new(_options);
        init.Database.EnsureCreated();
    }

    public MediaContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}

/// <summary>
/// Minimal IHttpContextAccessor stand-in — the real implementation is an
/// AsyncLocal-backed singleton unsuited to constructing an isolated
/// HttpContext per test.
/// </summary>
public sealed class HttpContextAccessorStub(HttpContext httpContext) : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; } = httpContext;
}
