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

namespace NoMercy.Api.Security;

/// <summary>
/// Decides whether a finished request was someone looking for a vulnerability.
///
/// The generic rule keys on whether ROUTING matched an endpoint, not on a 404:
/// AccessLogMiddleware answers 401 for anything unauthenticated, so a rule that
/// waited for a 404 would never fire against a real scanner.
/// </summary>
public static class ProbeDetector
{
    private const int KnownProbeWeight = 5;
    private const int SuspiciousWeight = 1;

    // Nothing in the NoMercy ecosystem asks for any of these, on any hostname,
    // ever. One hit is worth several ordinary misses.
    private static readonly string[] ForeignExtensions =
    [
        ".php",
        ".php5",
        ".php7",
        ".phtml",
        ".asp",
        ".aspx",
        ".jsp",
        ".cgi",
        ".cfm",
        ".shtml",
    ];

    private static readonly string[] ExploitFragments =
    [
        "/wp-admin",
        "/wp-content",
        "/wp-includes",
        "/wp-login",
        "/wordpress/",
        "/xmlrpc",
        "/phpmyadmin",
        "/pma/",
        "/adminer",
        "/vendor/phpunit",
        "/.env",
        "/.git/",
        "/.svn/",
        "/.aws/",
        "/.ssh/",
        "/cgi-bin/",
        "/solr/",
        "/actuator/",
        "/telescope/",
        "/jenkins/",
        "/hudson",
        "/boaform/",
        "/hnap1",
        "/etc/passwd",
    ];

    // A browser misses assets for innocent reasons — a stale service worker, a
    // deploy landing mid-session. Those 404s must never add up to a ban, so they
    // are cleared before the generic rule sees them.
    private static readonly string[] AssetExtensions =
    [
        ".js",
        ".mjs",
        ".css",
        ".map",
        ".woff",
        ".woff2",
        ".ttf",
        ".otf",
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp",
        ".avif",
        ".svg",
        ".ico",
        ".webmanifest",
    ];

    public static ProbeVerdict Classify(RequestOutcome outcome)
    {
        string path = outcome.Path.ToLowerInvariant();

        if (ForeignExtensions.Any(extension => path.EndsWith(extension, StringComparison.Ordinal)))
            return ProbeVerdict.KnownProbe;

        if (ExploitFragments.Any(fragment => path.Contains(fragment, StringComparison.Ordinal)))
            return ProbeVerdict.KnownProbe;

        if (AssetExtensions.Any(extension => path.EndsWith(extension, StringComparison.Ordinal)))
            return ProbeVerdict.Clean;

        if (outcome.IsAuthenticated || outcome.EndpointMatched || outcome.StatusCode < 400)
            return ProbeVerdict.Clean;

        return ProbeVerdict.Suspicious;
    }

    public static int Weight(ProbeVerdict verdict) =>
        verdict switch
        {
            ProbeVerdict.KnownProbe => KnownProbeWeight,
            ProbeVerdict.Suspicious => SuspiciousWeight,
            _ => 0,
        };
}
