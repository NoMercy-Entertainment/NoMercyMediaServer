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

namespace NoMercy.NmSystem.Dto;

/// <summary>
/// Why this server does or does not have a tunnel token. Without the distinction every
/// outcome — none provisioned, still provisioning, and "the check itself failed" — produced
/// the same "this is a paid feature" line, which is actively misleading when the real
/// problem is that the API could not be reached.
/// </summary>
public enum TunnelAvailability
{
    Unknown,
    NotProvisioned,
    Provisioning,
    Available,
    CheckFailed,
}
