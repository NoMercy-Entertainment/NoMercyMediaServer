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

using NoMercy.Data.Security;
using NoMercy.Database.Models.Security;

namespace NoMercy.Tests.Api.Security;

// Mirrors IpBanRepository's contract, including its one-row-per-address rule,
// which IpBanRepositoryTests proves against real SQLite.
public class InMemoryIpBanRepository : IIpBanRepository
{
    public List<IpBan> Rows { get; } = [];

    public Task<List<IpBan>> ActiveAsync(DateTime now, CancellationToken ct) =>
        Task.FromResult(
            Rows.Where(ban => ban.ExpiresAt > now).OrderByDescending(ban => ban.BannedAt).ToList()
        );

    public Task<IpBan?> FindActiveAsync(string address, DateTime now, CancellationToken ct) =>
        Task.FromResult(Rows.FirstOrDefault(ban => ban.Address == address && ban.ExpiresAt > now));

    public Task<int> PriorBanCountAsync(string address, CancellationToken ct)
    {
        List<IpBan> matching = Rows.Where(ban => ban.Address == address).ToList();

        return Task.FromResult(matching.Count == 0 ? 0 : matching.Max(ban => ban.BanNumber));
    }

    public Task<IpBan> UpsertAsync(IpBan ban, CancellationToken ct)
    {
        IpBan? existing = Rows.FirstOrDefault(row => row.Address == ban.Address);
        if (existing is null)
        {
            Rows.Add(ban);
            return Task.FromResult(ban);
        }

        existing.Reason = ban.Reason;
        existing.LastPath = ban.LastPath;
        existing.OffenceCount = ban.OffenceCount;
        existing.BanNumber = ban.BanNumber;
        existing.BannedAt = ban.BannedAt;
        existing.ExpiresAt = ban.ExpiresAt;
        existing.Manual = ban.Manual;

        return Task.FromResult(existing);
    }

    public Task<bool> RemoveAsync(string address, CancellationToken ct) =>
        Task.FromResult(Rows.RemoveAll(ban => ban.Address == address) > 0);

    public Task<int> PurgeExpiredAsync(DateTime cutoff, CancellationToken ct) =>
        Task.FromResult(Rows.RemoveAll(ban => ban.ExpiresAt < cutoff));
}
