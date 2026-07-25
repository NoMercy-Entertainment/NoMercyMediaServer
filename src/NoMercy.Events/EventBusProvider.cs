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

namespace NoMercy.Events;

public static class EventBusProvider
{
    private static IEventBus? _instance;

    public static IEventBus Current =>
        _instance
        ?? throw new InvalidOperationException(
            "EventBus has not been configured. Call EventBusProvider.Configure() during startup."
        );

    public static bool IsConfigured => _instance is not null;

    public static void Configure(IEventBus eventBus)
    {
        _instance = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }
}
