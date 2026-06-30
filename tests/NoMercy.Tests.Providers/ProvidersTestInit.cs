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
using NoMercy.NmSystem.Information;
using NoMercy.Providers.Helpers;
using NoMercy.Setup.Server;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Providers;

/// <summary>
/// Runs once per test assembly before any provider test executes, wiring the
/// process-wide dependencies the TMDB/provider clients assume at runtime:
/// <list type="bullet">
///   <item>Test app-data paths (<see cref="Config.IsTest"/> = true), so every
///   provider file path resolves under the dedicated "NoMercy_test" app-data
///   folder instead of the production location.</item>
///   <item>The <see cref="ApiKeyStore"/> singleton, so <c>TmdbBaseClient</c>'s
///   constructor (which reads <c>ApiKeyStore.Current.TmdbToken</c>) does not fail
///   on an uninitialised dependency.</item>
///   <item><see cref="CacheController"/>, rooted at a temp folder inside the test
///   app-data folder, so provider response caching reads/writes a writable,
///   test-scoped location and never touches a protected/production path.</item>
/// </list>
/// </summary>
internal static class ProvidersTestInit
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Resolve all AppFiles paths under the dedicated test app-data folder.
        Config.IsTest = true;

        // Ensure the ApiKeyStore singleton exists so TmdbBaseClient's constructor
        // (Bearer {ApiKeyStore.Current.TmdbToken}) does not fail on a missing dep.
        try
        {
            _ = ApiKeyStore.Current;
        }
        catch (InvalidOperationException)
        {
            _ = new ApiKeyStore();
        }

        // Provider clients cache responses under the app-data cache folder. Point
        // CacheController at the temp folder inside the (test) app-data folder and
        // make sure the directories exist, so cache I/O is satisfied and confined
        // to a writable, test-scoped location.
        Directory.CreateDirectory(AppFiles.ApiCachePath);
        Directory.CreateDirectory(AppFiles.TempPath);

        LocalStorageDriver driver = new();
        StoragePathGuard guard = new([AppFiles.AppPath], driver);
        IStorage storage = new LocalStorage(driver, guard);
        CacheController.Initialize(storage);
    }
}
