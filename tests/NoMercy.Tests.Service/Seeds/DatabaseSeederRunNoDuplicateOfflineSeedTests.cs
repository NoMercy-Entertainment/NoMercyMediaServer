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

namespace NoMercy.Tests.Service.Seeds;

/// <summary>
/// Requirement: <c>DatabaseSeeder.Run</c> must NOT call <c>SeedOfflineData</c> itself.
/// The only caller (<c>ServerBootstrapper</c>) already runs <c>SeedOfflineData</c>
/// directly, immediately after schema init, so the UI has config/library/encoder-preset
/// data before auth completes — <c>Run</c> re-running it doubled all six sub-seeds (44
/// SELECTs + 44 upserts on encoder presets alone) on every boot.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DatabaseSeederRunNoDuplicateOfflineSeedTests
{
    [Fact]
    public void Run_MethodBody_DoesNotCallSeedOfflineData()
    {
        string source = ReadSource("src/NoMercy.Service/Seeds/DatabaseSeeder.cs");

        int runStart = source.IndexOf(
            "public static async Task Run(IStorage storage, IStorageDriver storageDriver)",
            StringComparison.Ordinal
        );
        Assert.True(
            runStart >= 0,
            "DatabaseSeeder.Run signature moved — this guard needs updating"
        );

        int runEnd = source.IndexOf(
            "public static async Task LoadDiskOverlaysAsync",
            runStart,
            StringComparison.Ordinal
        );
        Assert.True(runEnd > runStart, "could not bound the Run method body");

        string runBody = source[runStart..runEnd];

        Assert.DoesNotContain("SeedOfflineData", runBody);
    }

    [Fact]
    public void ServerBootstrapper_CallsSeedOfflineDataExactlyOnce()
    {
        string source = ReadSource("src/NoMercy.Service/Hosting/ServerBootstrapper.cs");

        int count = 0;
        int index = 0;
        while ((index = source.IndexOf("SeedOfflineData", index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += "SeedOfflineData".Length;
        }

        Assert.Equal(1, count);
    }

    private static string ReadSource(string relativePath)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.GetFullPath(Path.Combine(dir, relativePath));
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException($"Could not locate {relativePath}");
    }
}
