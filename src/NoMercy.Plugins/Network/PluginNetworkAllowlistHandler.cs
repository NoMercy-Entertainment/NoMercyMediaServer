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
    : Exception(message: $"Plugin network access to host '{host}' is not permitted by its capabilities.");

public class PluginNetworkAllowlistHandler : DelegatingHandler
{
    private readonly List<Regex> _patterns;

    public PluginNetworkAllowlistHandler(IReadOnlyList<string> allowedHosts)
    {
        _patterns = allowedHosts.Select(selector: ToPattern).ToList();
    }

    private static Regex ToPattern(string host)
    {
        string escaped = Regex.Escape(str: host).Replace(oldValue: "\\*", newValue: "[^.]+");
        return new(pattern: $"^{escaped}$", options: RegexOptions.IgnoreCase);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        string host = request.RequestUri?.Host ?? string.Empty;
        if (!_patterns.Any(predicate: pattern => pattern.IsMatch(input: host)))
            throw new PluginNetworkDeniedException(host: host);

        return base.SendAsync(request: request, cancellationToken: cancellationToken);
    }
}
