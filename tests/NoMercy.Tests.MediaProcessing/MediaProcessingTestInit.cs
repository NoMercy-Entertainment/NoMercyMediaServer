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

using System.Runtime.CompilerServices;
using NoMercy.Database;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.Helpers;
using NoMercy.Setup.Server;

namespace NoMercy.Tests.MediaProcessing;

/// <summary>
/// Runs once per test assembly before any test executes. Prepares the
/// process-wide state MediaProcessing tests depend on: the ApiKeyStore singleton
/// (so reads of ApiKeyStore.Current succeed), a mock TMDB HTTP handler (so
/// MovieManager's lookups are served from fixtures with no network or token), and
/// a freshly created media-database schema so DB-backed tests do not depend on
/// another test assembly creating the shared schema first.
/// </summary>
internal static class MediaProcessingTestInit
{

    [ModuleInitializer]
    internal static void Initialize()
    {
        TestEnvironmentSetup.EnsureIsolatedAppData();

        // Ensure test paths regardless of module-initializer ordering.
        Config.IsTest = true;

        // Ensure the process-wide ApiKeyStore singleton exists so TmdbBaseClient's
        // constructor (which reads ApiKeyStore.Current.TmdbToken) does not throw.
        try
        {
            _ = ApiKeyStore.Current;
        }
        catch (InvalidOperationException)
        {
            _ = new ApiKeyStore();
        }

        // Route all TMDB HTTP calls through a mock handler so MovieManager's
        // real lookups are served from fixtures (no network, no token needed).
        HttpClientProvider.Initialize(new TmdbMockHttpClientFactory());

        // Create a full media-database schema for this assembly's process so
        // MovieManager store tests (Images/Similar/Recommendations) do not hit
        // "no such table" when MediaProcessing runs before the Api test assembly.
        Directory.CreateDirectory(AppFiles.DataPath);
        foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            string file = AppFiles.MediaDatabase + suffix;
            if (File.Exists(file))
                File.Delete(file);
        }

        using MediaContext db = new();
        db.Database.EnsureCreated();
    }
}
