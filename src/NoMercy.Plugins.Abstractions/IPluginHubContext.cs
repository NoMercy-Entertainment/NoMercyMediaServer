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

namespace NoMercy.Plugins.Abstractions;

/// <summary>
/// Server-to-client push, scoped to this plugin's subscribers.
/// <para>
/// A plugin cannot address another plugin's group: the instance it gets is
/// built around its own id. Transfer progress belongs here rather than in a
/// two-second poll of the view endpoint.
/// </para>
/// </summary>
public interface IPluginHubContext
{
    Task PushAsync(string type, object? payload);

    Task PushToUserAsync(string userId, string type, object? payload);
}
