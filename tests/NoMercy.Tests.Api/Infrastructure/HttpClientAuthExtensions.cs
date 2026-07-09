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

namespace NoMercy.Tests.Api.Infrastructure;

public static class HttpClientAuthExtensions
{
    public static HttpClient AsAuthenticated(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove(TestAuthDefaults.TestAuthHeader);
        return client;
    }

    public static HttpClient AsUnauthenticated(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove(TestAuthDefaults.TestAuthHeader);
        client.DefaultRequestHeaders.Add(TestAuthDefaults.TestAuthHeader, TestAuthDefaults.Deny);
        client.DefaultRequestHeaders.CacheControl = new() { NoCache = true };
        return client;
    }

    /// <summary>
    /// Authenticates as TestAuthHandler.SecondaryUserId — a second, distinct,
    /// allowed seeded user — for ownership-isolation tests against data owned by
    /// the default test identity.
    /// </summary>
    public static HttpClient AsSecondaryUser(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove(TestAuthDefaults.TestAuthHeader);
        client.DefaultRequestHeaders.Remove(TestAuthDefaults.TestUserIdHeader);
        client.DefaultRequestHeaders.Add(
            TestAuthDefaults.TestUserIdHeader,
            TestAuthHandler.SecondaryUserId.ToString()
        );
        return client;
    }
}
