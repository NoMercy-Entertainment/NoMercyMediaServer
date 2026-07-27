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

using NoMercy.Database.Models.Security;

namespace NoMercy.Data.Security;

public interface IIpBanRepository
{
    Task<List<IpBan>> ActiveAsync(DateTime now, CancellationToken ct);

    Task<IpBan?> FindActiveAsync(string address, DateTime now, CancellationToken ct);

    Task<int> PriorBanCountAsync(string address, CancellationToken ct);

    Task<IpBan> UpsertAsync(IpBan ban, CancellationToken ct);

    Task<bool> RemoveAsync(string address, CancellationToken ct);

    Task<int> PurgeExpiredAsync(DateTime cutoff, CancellationToken ct);
}
