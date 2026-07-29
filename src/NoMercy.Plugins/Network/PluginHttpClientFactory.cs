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

namespace NoMercy.Plugins.Network;

public static class PluginHttpClientFactory
{
    /// <param name="grantedHosts">
    /// Read on every request rather than captured once, so a host the owner
    /// grants after the plugin has started takes effect without a restart.
    /// </param>
    public static HttpClient Create(
        PluginCapabilities? capabilities,
        Func<IReadOnlyList<string>>? grantedHosts = null
    )
    {
        IReadOnlyList<string> hosts = capabilities?.Network?.Hosts ?? [];
        PluginNetworkAllowlistHandler handler = new(hosts, grantedHosts)
        {
            InnerHandler = new SocketsHttpHandler(),
        };
        return new(handler);
    }
}
