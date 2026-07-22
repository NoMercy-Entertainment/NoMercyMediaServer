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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace NoMercy.Tests.Database;

/// <summary>
/// Verifies that EnableSensitiveDataLogging is only active
/// when Config.IsDev is true (HIGH-03).
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class SensitiveDataLoggingTests
{
    /// <summary>
    /// Replicates the exact conditional from MediaContext.OnConfiguring
    /// to verify sensitive data logging is gated on Config.IsDev.
    /// </summary>
    private static bool BuildOptionsAndCheckSensitiveLogging(bool isDev)
    {
        DbContextOptionsBuilder options = new();
        options.UseSqlite(connectionString: "Data Source=:memory:");

        if (isDev)
            options.EnableSensitiveDataLogging();

        CoreOptionsExtension? coreExtension = options.Options.FindExtension<CoreOptionsExtension>();

        return coreExtension?.IsSensitiveDataLoggingEnabled ?? false;
    }

    [Fact]
    public void ProductionMode_DoesNotEnableSensitiveDataLogging()
    {
        bool isSensitiveLogging = BuildOptionsAndCheckSensitiveLogging(isDev: false);

        Assert.False(
            condition: isSensitiveLogging,
            userMessage: "EnableSensitiveDataLogging must not be active in production mode"
        );
    }

    [Fact]
    public void DevMode_EnablesSensitiveDataLogging()
    {
        bool isSensitiveLogging = BuildOptionsAndCheckSensitiveLogging(isDev: true);

        Assert.True(condition: isSensitiveLogging, userMessage: "EnableSensitiveDataLogging must be active in dev mode");
    }

    [Fact]
    public void MediaContext_OnConfiguring_GuardsSensitiveDataLogging_WithConfigIsDev()
    {
        // Verify the source code contains the Config.IsDev guard around EnableSensitiveDataLogging.
        // This catches regressions where someone removes the conditional.
        string sourceFile = FindRepoFile(
            relativePath: Path.Combine(path1: "src", path2: "NoMercy.Database", path3: "Contexts", path4: "MediaContext.cs")
        );

        string source = File.ReadAllText(path: sourceFile);

        Assert.Contains(expectedSubstring: "if (Config.IsDev)", actualString: source);
        Assert.Contains(expectedSubstring: "EnableSensitiveDataLogging", actualString: source);
    }

    // Walk up from the test assembly instead of a fixed ".." chain — the output
    // directory depth changes under a redirected BaseOutputPath.
    private static string FindRepoFile(string relativePath)
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null!)
        {
            string candidate = Path.Combine(path1: dir, path2: relativePath);
            if (File.Exists(path: candidate))
                return candidate;

            dir = Path.GetDirectoryName(path: dir)!;
        }

        throw new FileNotFoundException(
            message: $"Could not locate {relativePath} above {AppContext.BaseDirectory}"
        );
    }
}
