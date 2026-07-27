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

using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Security;
using NoMercy.Database;
using NoMercy.Database.Models.Security;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

public class IpBanRepositoryTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly IpBanRepository _repository;

    public IpBanRepositoryTests()
    {
        (IDbContextFactory<MediaContext> factory, SqliteConnection connection) =
            TestMediaContextFactory.CreateFactory();

        _connection = connection;
        _repository = new(factory);
    }

    [Fact]
    public async Task ActiveAsync_ExcludesExpiredBans()
    {
        await _repository.UpsertAsync(
            Ban("203.0.113.10", Now.AddHours(-2), Now.AddHours(-1)),
            CancellationToken.None
        );
        await _repository.UpsertAsync(
            Ban("203.0.113.11", Now, Now.AddHours(1)),
            CancellationToken.None
        );

        List<IpBan> active = await _repository.ActiveAsync(Now, CancellationToken.None);

        active.Should().ContainSingle().Which.Address.Should().Be("203.0.113.11");
    }

    [Fact]
    public async Task FindActiveAsync_ReturnsNothingOnceTheBanHasExpired()
    {
        await _repository.UpsertAsync(
            Ban("203.0.113.20", Now.AddHours(-2), Now.AddHours(-1)),
            CancellationToken.None
        );

        IpBan? found = await _repository.FindActiveAsync(
            "203.0.113.20",
            Now,
            CancellationToken.None
        );

        found.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_SameAddressTwice_ExtendsInsteadOfDuplicating()
    {
        await _repository.UpsertAsync(
            Ban("203.0.113.12", Now, Now.AddHours(1)),
            CancellationToken.None
        );

        IpBan extended = Ban("203.0.113.12", Now, Now.AddHours(4));
        extended.OffenceCount = 9;
        IpBan second = await _repository.UpsertAsync(extended, CancellationToken.None);

        List<IpBan> active = await _repository.ActiveAsync(Now, CancellationToken.None);

        active.Should().ContainSingle();
        second.ExpiresAt.Should().Be(Now.AddHours(4));
        second.OffenceCount.Should().Be(9);
    }

    [Fact]
    public async Task PriorBanCountAsync_ReturnsTheHighestBanNumberEverIssuedForTheAddress()
    {
        IpBan previous = Ban("203.0.113.13", Now.AddDays(-3), Now.AddDays(-3).AddHours(1));
        previous.BanNumber = 3;
        await _repository.UpsertAsync(previous, CancellationToken.None);

        int prior = await _repository.PriorBanCountAsync("203.0.113.13", CancellationToken.None);

        prior.Should().Be(3);
    }

    [Fact]
    public async Task PriorBanCountAsync_IsZeroForAnAddressNeverSeenBefore()
    {
        int prior = await _repository.PriorBanCountAsync("203.0.113.99", CancellationToken.None);

        prior.Should().Be(0);
    }

    [Fact]
    public async Task RemoveAsync_UnbansImmediately()
    {
        await _repository.UpsertAsync(
            Ban("203.0.113.14", Now, Now.AddHours(1)),
            CancellationToken.None
        );

        bool removed = await _repository.RemoveAsync("203.0.113.14", CancellationToken.None);
        List<IpBan> active = await _repository.ActiveAsync(Now, CancellationToken.None);

        removed.Should().BeTrue();
        active.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_ReportsFalseWhenThereWasNothingToLift()
    {
        bool removed = await _repository.RemoveAsync("203.0.113.98", CancellationToken.None);

        removed.Should().BeFalse();
    }

    [Fact]
    public async Task PurgeExpiredAsync_KeepsHistoryNewerThanCutoff()
    {
        await _repository.UpsertAsync(
            Ban("203.0.113.15", Now.AddDays(-40), Now.AddDays(-39)),
            CancellationToken.None
        );
        await _repository.UpsertAsync(
            Ban("203.0.113.16", Now.AddDays(-2), Now.AddDays(-1)),
            CancellationToken.None
        );

        int purged = await _repository.PurgeExpiredAsync(Now.AddDays(-30), CancellationToken.None);

        purged.Should().Be(1);
    }

    private static IpBan Ban(string address, DateTime bannedAt, DateTime expiresAt) =>
        new()
        {
            Address = address,
            Reason = "KnownProbe",
            LastPath = "/wp-login.php",
            OffenceCount = 2,
            BanNumber = 1,
            BannedAt = bannedAt,
            ExpiresAt = expiresAt,
        };

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
