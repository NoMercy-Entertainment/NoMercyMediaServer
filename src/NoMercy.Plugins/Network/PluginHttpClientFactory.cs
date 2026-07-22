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
    public static HttpClient Create(PluginCapabilities? capabilities)
    {
        IReadOnlyList<string> hosts = capabilities?.Network?.Hosts ?? [];
        PluginNetworkAllowlistHandler handler = new(allowedHosts: hosts)
        {
            InnerHandler = new SocketsHttpHandler(),
        };
        return new(handler: handler);
    }
}
