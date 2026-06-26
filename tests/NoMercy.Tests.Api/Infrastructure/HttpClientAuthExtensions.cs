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
}
