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

using System.Net;
using NoMercy.Database.Models.Security;

namespace NoMercy.Api.Security;

public interface IAbuseGuard
{
    Task<bool> IsBannedAsync(IPAddress? address, CancellationToken ct);

    Task RecordAsync(IPAddress? address, RequestOutcome outcome, CancellationToken ct);

    Task<IpBan> BanAsync(
        IPAddress address,
        string reason,
        TimeSpan duration,
        bool manual,
        CancellationToken ct
    );

    Task<bool> UnbanAsync(string address, CancellationToken ct);

    Task<List<IpBan>> ActiveBansAsync(CancellationToken ct);

    Task RefreshAsync(CancellationToken ct);
}
