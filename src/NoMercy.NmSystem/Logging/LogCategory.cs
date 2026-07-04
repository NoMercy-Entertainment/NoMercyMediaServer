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

namespace NoMercy.NmSystem.Logging;

/// <summary>
/// A console log category: the display label and its colour in each theme. One
/// category groups a subsystem (a provider, a worker, etc.) so related lines share
/// a colour while remaining distinguishable from other subsystems.
/// </summary>
public sealed record LogCategory(
    string Key,
    string DisplayName,
    string Group,
    string DarkHex,
    string LightHex
);
