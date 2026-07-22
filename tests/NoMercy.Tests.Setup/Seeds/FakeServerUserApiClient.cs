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

using NoMercy.Service.Seeds;
using NoMercy.Service.Seeds.Dto;

namespace NoMercy.Tests.Setup.Seeds;

/// <summary>
/// Stands in for the real api.nomercy.tv call in <see cref="ServerUserSyncService"/>
/// tests. Records whether it was invoked so a "no token" test can assert the HTTP
/// boundary was never crossed. Optionally throws instead of returning, standing in
/// for <see cref="ServerUserApiClient"/>'s own throw-on-empty/malformed-body behavior
/// so sync-level tests can assert "no DB write happens on a bad upstream response"
/// without going over the network.
/// </summary>
internal sealed class FakeServerUserApiClient(
    ServerUserDtoData[] response,
    Exception? throwInstead = null
) : IServerUserApiClient
{
    public bool WasCalled { get; private set; }

    public Task<ServerUserDtoData[]> GetServerUsersAsync(
        string accessToken,
        CancellationToken cancellationToken = default
    )
    {
        WasCalled = true;
        return throwInstead is not null
            ? Task.FromException<ServerUserDtoData[]>(exception: throwInstead)
            : Task.FromResult(result: response);
    }
}
