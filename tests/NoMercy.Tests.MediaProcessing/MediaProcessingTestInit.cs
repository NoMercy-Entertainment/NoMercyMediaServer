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
using System;
using System.Runtime.CompilerServices;
using NoMercy.Setup.Server;

namespace NoMercy.Tests.MediaProcessing;

/// <summary>
/// Runs once per test assembly before any test executes. Ensures the
/// process-wide <see cref="ApiKeyStore"/> singleton is initialised so tests
/// that read <see cref="ApiKeyStore.Current"/> (for example MovieManager) do
/// not depend on another test having constructed it first — previously an
/// order-dependent, flaky "ApiKeyStore not initialized" failure under parallel
/// execution.
/// </summary>
internal static class MediaProcessingTestInit
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        try
        {
            _ = ApiKeyStore.Current;
        }
        catch (InvalidOperationException)
        {
            _ = new ApiKeyStore();
        }
    }
}
