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

using NoMercy.Encoder.Distribution;
using Xunit;

namespace NoMercy.Tests.Encoder.Distribution;

/// <summary>
/// The introspect cache only checked TTL on read and never removed entries, so
/// rotated worker tokens accumulated as permanent keys. ExpiredIntrospectKeys is
/// what the write-path sweep uses to keep it bounded.
/// </summary>
public class LicenseTokenClientCacheTests
{
    private static KeyValuePair<string, (IntrospectResult Result, DateTime CachedAt)> Entry(
        string token,
        DateTime cachedAt
    ) => new(key: token, value: (new IntrospectResult(Active: true, Scopes: [], Message: null), cachedAt));

    [Fact]
    public void ExpiredIntrospectKeys_SelectsEntriesAtOrPastTtl_KeepsFreshOnes()
    {
        DateTime now = new(year: 2026, month: 1, day: 1, hour: 0, minute: 0, second: 30, kind: DateTimeKind.Utc);
        TimeSpan ttl = TimeSpan.FromSeconds(seconds: 30);
        List<KeyValuePair<string, (IntrospectResult Result, DateTime CachedAt)>> entries =
        [
            Entry(token: "fresh", cachedAt: now.AddSeconds(value: -5)),
            Entry(token: "edge", cachedAt: now.AddSeconds(value: -30)),
            Entry(token: "stale", cachedAt: now.AddSeconds(value: -120)),
        ];

        List<string> expired = LicenseTokenClient.ExpiredIntrospectKeys(entries: entries, now: now, ttl: ttl).ToList();

        Assert.Equal(expected: 2, actual: expired.Count);
        Assert.Contains(expected: "edge", collection: expired);
        Assert.Contains(expected: "stale", collection: expired);
        Assert.DoesNotContain(expected: "fresh", collection: expired);
    }

    [Fact]
    public void ExpiredIntrospectKeys_AllFresh_SelectsNothing()
    {
        DateTime now = new(year: 2026, month: 1, day: 1, hour: 0, minute: 0, second: 30, kind: DateTimeKind.Utc);
        List<KeyValuePair<string, (IntrospectResult Result, DateTime CachedAt)>> entries =
        [
            Entry(token: "a", cachedAt: now.AddSeconds(value: -1)),
            Entry(token: "b", cachedAt: now.AddSeconds(value: -10)),
        ];

        Assert.Empty(
            collection: LicenseTokenClient.ExpiredIntrospectKeys(entries: entries, now: now, ttl: TimeSpan.FromSeconds(seconds: 30))
        );
    }
}
