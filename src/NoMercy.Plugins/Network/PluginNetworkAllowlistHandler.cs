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

using System.Text.RegularExpressions;

namespace NoMercy.Plugins.Network;

public class PluginNetworkDeniedException(string host)
    : Exception($"Plugin network access to host '{host}' is not permitted by its capabilities.");

public class PluginNetworkAllowlistHandler : DelegatingHandler
{
    private readonly List<Regex> _patterns;

    public PluginNetworkAllowlistHandler(IReadOnlyList<string> allowedHosts)
    {
        _patterns = allowedHosts.Select(ToPattern).ToList();
    }

    private static Regex ToPattern(string host)
    {
        string escaped = Regex.Escape(host).Replace("\\*", "[^.]+");
        return new($"^{escaped}$", RegexOptions.IgnoreCase);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        string host = request.RequestUri?.Host ?? string.Empty;
        if (!_patterns.Any(pattern => pattern.IsMatch(host)))
            throw new PluginNetworkDeniedException(host);

        return base.SendAsync(request, cancellationToken);
    }
}
