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
/// Covers the regression this slice fixes: <see cref="UsersSeed"/> used to be
/// gated on the Users table being empty, so a user invited after the server's
/// first boot was never synced. <see cref="ServerUserSyncService"/> is the
/// ongoing-reconciliation path the recurring cron job calls — it must upsert
/// every run regardless of table state, and must revoke access for users no
/// longer returned upstream without ever touching the owner.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class ServerUserSyncServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;
    private static readonly Mock<IStorage> NoLibrariesStorage = new();

    public ServerUserSyncServiceTests()
    {
        _connection = new(connectionString: "DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MediaContext>().UseSqlite(connection: _connection).Options;

        using MediaContext ctx = new(options: _options);
        ctx.Database.EnsureCreated();

        // No libraries.jsonc on disk in these tests — Exists() defaults to
        // false on an unconfigured Moq mock, so library-access seeding is a
        // no-op and out of scope for these assertions.
    }

    public void Dispose() => _connection.Dispose();

    private MediaContext CreateContext() => new(options: _options);

    private static readonly Guid OwnerId = Guid.Parse(input: "11111111-1111-1111-1111-111111111111");
    private static readonly Guid InvitedUserId = Guid.Parse(input: "22222222-2222-2222-2222-222222222222");

    private static User OwnerUser() =>
        new()
        {
            Id = OwnerId,
            Email = "owner@example.com",
            Name = "Owner",
            Allowed = true,
            Owner = true,
            AudioTranscoding = true,
            NoTranscoding = true,
            VideoTranscoding = true,
        };

    [Fact]
    public async Task SyncAsync_AddsNewlyInvitedUser_ToNonEmptyUsersTable()
    {
        // Regression case: the Users table already has the owner (from a prior
        // boot) when a second person accepts an invite. The old UsersSeed.Init
        // would never re-run once any user existed — this must not.
        await using MediaContext seedCtx = CreateContext();
        seedCtx.Users.Add(entity: OwnerUser());
        await seedCtx.SaveChangesAsync();

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
            new()
            {
                UserId = InvitedUserId.ToString(),
                Name = "Invited",
                Email = "invited@example.com",
                IsOwner = false,
                Enabled = true,
            },
        ]);
        ServerUserSyncService sut = new(apiClient: api);

        await using MediaContext runCtx = CreateContext();
        ServerUserSyncResult result = await sut.SyncAsync(
            dbContext: runCtx,
            storage: NoLibrariesStorage.Object,
            accessToken: "valid-token"
        );

        Assert.True(condition: result.Attempted);
        Assert.Equal(expected: 2, actual: result.UpstreamUserCount);

        await using MediaContext assertCtx = CreateContext();
        User? invited = await assertCtx.Users.FirstOrDefaultAsync(predicate: u => u.Id == InvitedUserId);
        Assert.NotNull(@object: invited);
        Assert.True(condition: invited!.Allowed);
        Assert.False(condition: invited.Owner);

        Assert.Equal(expected: 2, actual: await assertCtx.Users.CountAsync());
    }

    [Fact]
    public async Task SyncAsync_RevokesAccess_ForUserNoLongerReturnedUpstream_OwnerPreserved()
    {
        await using MediaContext seedCtx = CreateContext();
        seedCtx.Users.Add(entity: OwnerUser());
        seedCtx.Users.Add(
            entity: new()
            {
                Id = InvitedUserId,
                Email = "invited@example.com",
                Name = "Invited",
                Allowed = true,
                Owner = false,
                AudioTranscoding = true,
                NoTranscoding = true,
                VideoTranscoding = true,
            }
        );
        await seedCtx.SaveChangesAsync();

        // Upstream now only returns the owner — the invite was removed/declined.
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
        ServerUserSyncService sut = new(apiClient: api);

        await using MediaContext runCtx = CreateContext();
        ServerUserSyncResult result = await sut.SyncAsync(
            dbContext: runCtx,
            storage: NoLibrariesStorage.Object,
            accessToken: "valid-token"
        );

        Assert.Equal(expected: 1, actual: result.RevokedCount);

        await using MediaContext assertCtx = CreateContext();
        User? revoked = await assertCtx.Users.FirstOrDefaultAsync(predicate: u => u.Id == InvitedUserId);
        Assert.NotNull(@object: revoked);
        Assert.False(condition: revoked!.Allowed);
        Assert.False(condition: revoked.AudioTranscoding);
        Assert.False(condition: revoked.VideoTranscoding);
        Assert.False(condition: revoked.NoTranscoding);

        User? owner = await assertCtx.Users.FirstOrDefaultAsync(predicate: u => u.Id == OwnerId);
        Assert.NotNull(@object: owner);
        Assert.True(condition: owner!.Owner);
        Assert.True(condition: owner.Allowed);
    }

    [Fact]
    public async Task SyncAsync_NoAccessToken_SkipsWithoutThrowing_AndNeverCallsApi()
    {
        FakeServerUserApiClient api = new(response: []);
        ServerUserSyncService sut = new(apiClient: api);

        await using MediaContext runCtx = CreateContext();
        ServerUserSyncResult result = await sut.SyncAsync(
            dbContext: runCtx,
            storage: NoLibrariesStorage.Object,
            accessToken: null
        );

        Assert.False(condition: result.Attempted);
        Assert.Equal(expected: 0, actual: result.UpstreamUserCount);
        Assert.False(condition: api.WasCalled);
    }

    [Fact]
    public async Task SyncAsync_IsIdempotent_RunningTwiceKeepsSingleRowPerUser()
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
        ServerUserSyncService sut = new(apiClient: api);

        await using MediaContext firstRun = CreateContext();
        await sut.SyncAsync(dbContext: firstRun, storage: NoLibrariesStorage.Object, accessToken: "valid-token");

        await using MediaContext secondRun = CreateContext();
        await sut.SyncAsync(dbContext: secondRun, storage: NoLibrariesStorage.Object, accessToken: "valid-token");

        await using MediaContext assertCtx = CreateContext();
        Assert.Equal(expected: 1, actual: await assertCtx.Users.CountAsync());
    }

    /// <summary>
    /// api.nomercy.tv responding 200 with an empty/malformed body must surface as
    /// a hard failure (ServerUserApiClient.ParseResponse throws) rather than an
    /// empty user list — SyncAsync must let that exception propagate BEFORE any
    /// database write, so a transient bad response can never mass-revoke.
    /// </summary>
    [Fact]
    public async Task SyncAsync_ApiClientThrows_PropagatesException_AndRevokesNoOne()
    {
        await using MediaContext seedCtx = CreateContext();
        seedCtx.Users.Add(entity: OwnerUser());
        seedCtx.Users.Add(
            entity: new()
            {
                Id = InvitedUserId,
                Email = "invited@example.com",
                Name = "Invited",
                Allowed = true,
                Owner = false,
                AudioTranscoding = true,
                NoTranscoding = true,
                VideoTranscoding = true,
            }
        );
        await seedCtx.SaveChangesAsync();

        FakeServerUserApiClient api = new(
            response: [],
            throwInstead: new InvalidOperationException(
                message: "server-users response was empty or unparseable"
            )
        );
        ServerUserSyncService sut = new(apiClient: api);

        await using MediaContext runCtx = CreateContext();
        await Assert.ThrowsAsync<InvalidOperationException>(testCode: () =>
            sut.SyncAsync(dbContext: runCtx, storage: NoLibrariesStorage.Object, accessToken: "valid-token")
        );

        await using MediaContext assertCtx = CreateContext();
        User? invited = await assertCtx.Users.FirstOrDefaultAsync(predicate: u => u.Id == InvitedUserId);
        Assert.NotNull(@object: invited);
        Assert.True(condition: invited!.Allowed);
    }

    /// <summary>
    /// Self floor check: with_self=true means an authoritative response always
    /// contains this server's own owner. An empty (or owner-omitting) list must
    /// abort the whole reconcile — no upsert, no revoke — instead of being read
    /// as "upstream really does have zero users now".
    /// </summary>
    [Fact]
    public async Task SyncAsync_ResponseOmitsLocalOwner_AbortsReconcile_NoRevoke()
    {
        await using MediaContext seedCtx = CreateContext();
        seedCtx.Users.Add(entity: OwnerUser());
        seedCtx.Users.Add(
            entity: new()
            {
                Id = InvitedUserId,
                Email = "invited@example.com",
                Name = "Invited",
                Allowed = true,
                Owner = false,
                AudioTranscoding = true,
                NoTranscoding = true,
                VideoTranscoding = true,
            }
        );
        await seedCtx.SaveChangesAsync();

        // A well-formed but empty payload — the "silently treated as zero users"
        // shape the compat-gate flagged, not a thrown exception.
        FakeServerUserApiClient api = new(response: []);
        ServerUserSyncService sut = new(apiClient: api);

        await using MediaContext runCtx = CreateContext();
        ServerUserSyncResult result = await sut.SyncAsync(
            dbContext: runCtx,
            storage: NoLibrariesStorage.Object,
            accessToken: "valid-token"
        );

        Assert.False(condition: result.Attempted);
        Assert.Equal(expected: 0, actual: result.RevokedCount);

        await using MediaContext assertCtx = CreateContext();
        User? invited = await assertCtx.Users.FirstOrDefaultAsync(predicate: u => u.Id == InvitedUserId);
        Assert.NotNull(@object: invited);
        Assert.True(condition: invited!.Allowed);

        User? owner = await assertCtx.Users.FirstOrDefaultAsync(predicate: u => u.Id == OwnerId);
        Assert.NotNull(@object: owner);
        Assert.True(condition: owner!.Owner);
    }

    /// <summary>
    /// Transcoding flags are local per-user config, not upstream membership state.
    /// A stable server-users list (nothing invited or removed) must not silently
    /// re-stomp a local override back to serverUser.Enabled every cycle.
    /// </summary>
    [Fact]
    public async Task SyncAsync_PreservesLocalTranscodingOverride_AcrossSync()
    {
        await using MediaContext seedCtx = CreateContext();
        seedCtx.Users.Add(entity: OwnerUser());
        seedCtx.Users.Add(
            entity: new()
            {
                Id = InvitedUserId,
                Email = "invited@example.com",
                Name = "Invited",
                Allowed = true,
                Owner = false,
                // Locally overridden away from what upstream Enabled=true implies.
                AudioTranscoding = false,
                NoTranscoding = false,
                VideoTranscoding = false,
            }
        );
        await seedCtx.SaveChangesAsync();

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
            new()
            {
                UserId = InvitedUserId.ToString(),
                Name = "Invited",
                Email = "invited@example.com",
                IsOwner = false,
                Enabled = true,
            },
        ]);
        ServerUserSyncService sut = new(apiClient: api);

        await using MediaContext runCtx = CreateContext();
        await sut.SyncAsync(dbContext: runCtx, storage: NoLibrariesStorage.Object, accessToken: "valid-token");

        await using MediaContext assertCtx = CreateContext();
        User? invited = await assertCtx.Users.FirstOrDefaultAsync(predicate: u => u.Id == InvitedUserId);
        Assert.NotNull(@object: invited);
        Assert.True(condition: invited!.Allowed);
        Assert.False(condition: invited.AudioTranscoding);
        Assert.False(condition: invited.NoTranscoding);
        Assert.False(condition: invited.VideoTranscoding);
    }
}
