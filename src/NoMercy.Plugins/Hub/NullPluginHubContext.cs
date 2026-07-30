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

using NoMercy.Plugins.Abstractions;

namespace NoMercy.Plugins.Hub;

/// <summary>
/// Pushing succeeds and reaches nobody, which is the truth where no hub is
/// mapped. The alternative is a plugin crashing outside the web host for
/// calling a method that is part of its contract.
/// </summary>
public class NullPluginHubContext : IPluginHubContext
{
    public Task PushAsync(string type, object? payload) => Task.CompletedTask;

    public Task PushToUserAsync(string userId, string type, object? payload) => Task.CompletedTask;
}
