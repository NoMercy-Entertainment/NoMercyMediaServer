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

namespace NoMercy.Providers.Helpers;

public enum QueueProcessingOrder
{
    OldestFirst,
    NewestFirst,
}

public class TokenBucketRateLimiterOptions
{
    public int TokenLimit = 8;
    public QueueProcessingOrder QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    public int QueueLimit = 3;
    public TimeSpan ReplenishmentPeriod = TimeSpan.FromMilliseconds(1);
    public int TokensPerPeriod = 2;
    public bool AutoReplenishment = true;
}
