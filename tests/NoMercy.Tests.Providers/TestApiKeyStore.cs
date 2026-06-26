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
using NoMercy.Setup.Server;

namespace NoMercy.Tests.Providers;

/// <summary>
/// Shared accessor for the process-wide <see cref="ApiKeyStore"/> singleton used by
/// provider tests. Returns the existing <see cref="ApiKeyStore.Current"/> instance when
/// one has been initialised, otherwise creates it. Tests mutate only the key they need
/// and restore it, mirroring the pre-refactor shared-static semantics so concurrent test
/// classes do not clobber each other's keys.
/// </summary>
internal static class TestApiKeyStore
{
    private static readonly object Gate = new();

    internal static ApiKeyStore Instance
    {
        get
        {
            lock (Gate)
            {
                try
                {
                    return (ApiKeyStore)ApiKeyStore.Current;
                }
                catch (InvalidOperationException)
                {
                    return new ApiKeyStore();
                }
            }
        }
    }
}
