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

namespace NoMercy.Networking.Discovery;

/// <summary>
/// One live `_googlecast._tcp` announcement. <see cref="Id"/> is Google's own
/// Cast device id (the mDNS TXT `id=` key) — stable across reboots, unlike
/// the IP it currently answers on.
/// </summary>
public sealed record CastMdnsHit(
    string Id,
    string? FriendlyName,
    string? Model,
    string Ip,
    DateTime SeenAt
);
