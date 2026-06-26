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
using System.IO;
using System.Runtime.CompilerServices;
using NoMercy.Database;
using NoMercy.NmSystem.Information;
using NoMercy.Setup.Server;

namespace NoMercy.Tests.MediaProcessing;

/// <summary>
/// Runs once per test assembly before any test executes. Prepares the
/// process-wide state MediaProcessing tests depend on: the ApiKeyStore singleton
/// (with the shared TMDB read token so MovieManager's real lookups authenticate),
/// and a freshly created media-database schema so DB-backed tests do not depend
/// on another test assembly creating the shared schema first.
/// </summary>
internal static class MediaProcessingTestInit
{
    // TMDB read-only API token shared with the provider tests (TmdbTestBase).
    private const string TmdbReadToken =
        "eyJhbGciOiJIUzI1NiJ9.eyJhdWQiOiJlZDNiZjg2MGFkZWYwNTM3NzgzZTRhYmVlODZkNjVhZiIsInN1YiI6IjViNTE5MWQ3MGUwYTI2MjU5OTAwZmY0MyIsInNjb3BlcyI6WyJhcGlfcmVhZCJdLCJ2ZXJzaW9uIjoxfQ.QndOAaK4WKspNYRhVxp0yq1-plwoJR7iBcwQSn0NQJA";

    [ModuleInitializer]
    internal static void Initialize()
    {
        // Ensure test paths regardless of module-initializer ordering.
        Config.IsTest = true;

        // Initialise the process-wide ApiKeyStore with the TMDB token so
        // MovieManager's real TMDB lookups authenticate instead of returning 401.
        ApiKeyStore apiKeyStore;
        try
        {
            apiKeyStore = (ApiKeyStore)ApiKeyStore.Current;
        }
        catch (InvalidOperationException)
        {
            apiKeyStore = new ApiKeyStore();
        }

        apiKeyStore.TmdbToken = TmdbReadToken;

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
