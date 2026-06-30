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

namespace NoMercy.Tests;

/// <summary>
/// Runs once per test assembly (i.e. per test-host process) before any test
/// executes. Marks the run as a test run and gives the process its own isolated
/// app-data root so test assemblies can run in parallel
/// (<c>default.runsettings</c> MaxCpuCount=0) without sharing a database, cache
/// or log directory — which is what previously forced serial execution.
/// </summary>
public static class TestEnvironmentSetup
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Config.IsTest = true;
        EnsureIsolatedAppData();

        string testPath = AppFiles.AppPath;
        if (!Directory.Exists(testPath))
            Directory.CreateDirectory(testPath);
    }

    /// <summary>
    /// Points this process at its own app-data root via NOMERCY_APP_PATH so that
    /// parallel test-host processes never touch the same database/cache/logs.
    /// Idempotent and ordering-independent: safe to call from any module
    /// initializer (the first caller wins; an explicit NOMERCY_APP_PATH, e.g. from
    /// scripts/run-tests.sh, is always respected). Must be invoked before any
    /// AppFiles path is read.
    /// </summary>
    public static void EnsureIsolatedAppData()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NOMERCY_APP_PATH")))
            return;

        string root = Path.Combine(
            Path.GetTempPath(),
            $"nomercy-test-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable("NOMERCY_APP_PATH", root);
        Directory.CreateDirectory(root);
    }
}
