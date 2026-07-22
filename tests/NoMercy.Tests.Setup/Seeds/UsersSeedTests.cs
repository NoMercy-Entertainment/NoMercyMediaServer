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
using NoMercy.Database.Models.Users;
using NoMercy.Service.Seeds;
using NoMercy.Service.Seeds.Dto;
using NoMercy.Storage;

namespace NoMercy.Tests.Setup.Seeds;

/// <summary>
/// <see cref="UsersSeed"/> is now a thin first-boot wrapper around
/// <see cref="ServerUserSyncService"/> — these tests confirm boot-time
/// behavior is unchanged: it still seeds an empty Users table, and still
/// never re-runs once any user exists (that ongoing job now belongs to
/// <c>ServerUserSyncCronJob</c>, covered separately).
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class UsersSeedTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;
    private static readonly Mock<IStorage> NoLibrariesStorage = new();

    public UsersSeedTests()
    {
        _connection = new(connectionString: "DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MediaContext>().UseSqlite(connection: _connection).Options;

        using MediaContext ctx = new(options: _options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private MediaContext CreateContext() => new(options: _options);

    private static readonly Guid OwnerId = Guid.Parse(input: "33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task Init_EmptyUsersTable_SeedsFromUpstream()
    {
        FakeServerUserApiClient api = new(response:
        [
            new()
            {
                UserId = OwnerId.ToString(),
                Name = "Owner",
                Email = "owner@example.com",
                IsOwner = true,
                Enabled = true,
            },
        ]);
        ServerUserSyncService realSyncService = new(apiClient: api);

        await using MediaContext ctx = CreateContext();
        await ctx.Init(storage: NoLibrariesStorage.Object, accessToken: "valid-token", syncService: realSyncService);

        User? owner = await ctx.Users.FirstOrDefaultAsync(predicate: u => u.Id == OwnerId);
        Assert.NotNull(@object: owner);
        Assert.True(condition: owner!.Owner);
        Assert.True(condition: api.WasCalled);
    }

    [Fact]
    public async Task Init_NonEmptyUsersTable_NeverCallsSyncService()
    {
        await using MediaContext seedCtx = CreateContext();
        seedCtx.Users.Add(
            entity: new()
            {
                Id = OwnerId,
                Email = "owner@example.com",
                Name = "Owner",
                Allowed = true,
                Owner = true,
            }
        );
        await seedCtx.SaveChangesAsync();

        FakeServerUserApiClient api = new(response: []);
        ServerUserSyncService realSyncService = new(apiClient: api);

        await using MediaContext ctx = CreateContext();
        await ctx.Init(storage: NoLibrariesStorage.Object, accessToken: "valid-token", syncService: realSyncService);

        // The one-shot seed must stay gated — ongoing sync is the cron job's job.
        Assert.False(condition: api.WasCalled);
    }

    [Fact]
    public async Task Init_NoAccessToken_DoesNotThrow()
    {
        await using MediaContext ctx = CreateContext();

        Exception? exception = await Record.ExceptionAsync(testCode: () =>
            UsersSeed.Init(dbContext: ctx, storage: NoLibrariesStorage.Object, accessToken: null)
        );

        Assert.Null(@object: exception);
    }
}
