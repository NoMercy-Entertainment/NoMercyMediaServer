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
using Microsoft.AspNetCore.SignalR;
using NoMercy.Authorization;

namespace NoMercy.Api.Hubs;

/// <summary>
/// Maps a SignalR connection to the NoMercy user id (the NameIdentifier GUID),
/// normalized to its canonical lowercase string. Without this, SignalR's default
/// provider routes <c>Clients.User(id)</c> on the raw NameIdentifier claim string,
/// which must coincidentally equal <c>Device.OwnerUserId.ToString()</c> for per-user
/// pushes (e.g. DeviceListChanged) to land on the right account. Pinning both sides
/// to <c>Guid.ToString()</c> removes that fragility and prevents cross-account leaks.
/// </summary>
public sealed class NoMercyUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        Guid userId = connection.User.UserId();
        return userId == Guid.Empty ? null : userId.ToString();
    }
}
