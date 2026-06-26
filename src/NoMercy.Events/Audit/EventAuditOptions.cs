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

namespace NoMercy.Events.Audit;

public sealed class EventAuditOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxEntries { get; set; } = 10_000;
    public double CompactionPercentage { get; set; } = 0.25;
    public HashSet<string> ExcludedEventTypes { get; set; } = [];
}
