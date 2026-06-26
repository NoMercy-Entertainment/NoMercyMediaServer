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

namespace NoMercy.Providers.Helpers;

public static class HttpClientProvider
{
    private static IHttpClientFactory? _factory;

    public static void Initialize(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    internal static void Reset()
    {
        _factory = null;
    }

    public static HttpClient CreateClient(string name)
    {
        if (_factory is not null)
        {
            try
            {
                return _factory.CreateClient(name);
            }
            catch (ObjectDisposedException)
            {
                _factory = null;
            }
        }

        return new();
    }
}
