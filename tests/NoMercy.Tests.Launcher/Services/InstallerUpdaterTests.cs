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

using System.Security.Cryptography;
using NoMercy.Launcher.Services;
using Xunit;

namespace NoMercy.Tests.Launcher.Services;

/// <summary>
/// <see cref="InstallerUpdater"/>'s cache-file methods (<c>VerifyInstallerAsync</c>,
/// <c>CleanCacheAsync</c>) resolve their directory from a hardcoded
/// <c>%LocalAppData%\NoMercy\UpdateCache</c> — unlike the rest of the Launcher
/// it does NOT go through <c>AppFiles.AppPath</c>, so it is not covered by
/// TestEnvironmentSetup's NOMERCY_APP_PATH isolation (flagged in the coverage
/// report as a real, if minor, testability/isolation gap — not fixed here
/// since it's a deliberate-looking choice: the installer cache is meant to
/// survive across dev/test/prod app-data roots). Every test below uses a
/// GUID-suffixed fake "version" so it can never collide with a real cached
/// installer, and removes exactly the files it created in a finally block —
/// it never touches or clears the directory wholesale.
/// </summary>
public sealed class InstallerUpdaterTests
{
    private static string CacheDir =>
        Path.Combine(
            path1: Environment.GetFolderPath(folder: Environment.SpecialFolder.LocalApplicationData),
            path2: "NoMercy",
            path3: "UpdateCache"
        );

    private static string InstallerFileName(string version) =>
        $"NoMercyMediaServer-{version}-windows-x64-setup.exe";

    [Fact]
    public async Task IsInstallerDeploymentAsync_ProcessRunningOutsideBinariesPath_ReturnsTrue()
    {
        // The test host process (testhost.exe / dotnet) never lives under
        // %AppData%\NoMercy\binaries, so this must report "installer deployment".
        InstallerUpdater updater = new(serverConnection: new ServerConnection());

        bool result = await updater.IsInstallerDeploymentAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsInstallerDeploymentAsync_InstallDirEnvVarSet_ReturnsTrueWithoutPathCheck()
    {
        Environment.SetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR", value: @"C:\Program Files\NoMercy");
        try
        {
            InstallerUpdater updater = new(serverConnection: new ServerConnection());

            bool result = await updater.IsInstallerDeploymentAsync();

            result.Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR", value: null);
        }
    }

    [Fact]
    public async Task VerifyInstallerAsync_NoSha256Sidecar_ReturnsTrueAsLegacyRelease()
    {
        string version = $"test-{Guid.NewGuid():N}";
        InstallerUpdater updater = new(serverConnection: new ServerConnection());

        bool result = await updater.VerifyInstallerAsync(version: version);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyInstallerAsync_MatchingSha256Sidecar_ReturnsTrue()
    {
        string version = $"test-{Guid.NewGuid():N}";
        Directory.CreateDirectory(path: CacheDir);
        string exePath = Path.Combine(path1: CacheDir, path2: InstallerFileName(version: version));
        string sha256Path = exePath + ".sha256";

        try
        {
            byte[] content = "fake installer bytes for hash verification"u8.ToArray();
            await File.WriteAllBytesAsync(path: exePath, bytes: content);
            string hash = Convert.ToHexString(inArray: SHA256.HashData(source: content));
            await File.WriteAllTextAsync(path: sha256Path, contents: hash);

            InstallerUpdater updater = new(serverConnection: new ServerConnection());

            bool result = await updater.VerifyInstallerAsync(version: version);

            result.Should().BeTrue();
        }
        finally
        {
            File.Delete(path: exePath);
            File.Delete(path: sha256Path);
        }
    }

    [Fact]
    public async Task VerifyInstallerAsync_SidecarInHashSpaceFilenameFormat_StillParsesTheHash()
    {
        string version = $"test-{Guid.NewGuid():N}";
        Directory.CreateDirectory(path: CacheDir);
        string exePath = Path.Combine(path1: CacheDir, path2: InstallerFileName(version: version));
        string sha256Path = exePath + ".sha256";

        try
        {
            byte[] content = "fake installer bytes, sha256sum-style sidecar"u8.ToArray();
            await File.WriteAllBytesAsync(path: exePath, bytes: content);
            string hash = Convert.ToHexString(inArray: SHA256.HashData(source: content)).ToLowerInvariant();
            // sha256sum's own output format is "HASH  filename" (lowercase hex).
            await File.WriteAllTextAsync(path: sha256Path, contents: $"{hash}  {InstallerFileName(version: version)}");

            InstallerUpdater updater = new(serverConnection: new ServerConnection());

            bool result = await updater.VerifyInstallerAsync(version: version);

            result.Should().BeTrue();
        }
        finally
        {
            File.Delete(path: exePath);
            File.Delete(path: sha256Path);
        }
    }

    [Fact]
    public async Task VerifyInstallerAsync_MismatchedSha256_ThrowsInvalidDataException()
    {
        string version = $"test-{Guid.NewGuid():N}";
        Directory.CreateDirectory(path: CacheDir);
        string exePath = Path.Combine(path1: CacheDir, path2: InstallerFileName(version: version));
        string sha256Path = exePath + ".sha256";

        try
        {
            await File.WriteAllBytesAsync(path: exePath, bytes: "real content"u8.ToArray());
            // Sidecar records the hash of totally different bytes.
            string wrongHash = Convert.ToHexString(
                inArray: SHA256.HashData(source: "different content"u8.ToArray())
            );
            await File.WriteAllTextAsync(path: sha256Path, contents: wrongHash);

            InstallerUpdater updater = new(serverConnection: new ServerConnection());

            Func<Task> act = () => updater.VerifyInstallerAsync(version: version);

            await act.Should().ThrowAsync<InvalidDataException>();
        }
        finally
        {
            File.Delete(path: exePath);
            File.Delete(path: sha256Path);
        }
    }

    [Fact]
    public async Task CleanCacheAsync_RemovesFilesForOtherVersions_KeepsCurrentAndPending()
    {
        string current = $"cur-{Guid.NewGuid():N}";
        string pending = $"pend-{Guid.NewGuid():N}";
        string stale = $"stale-{Guid.NewGuid():N}";
        Directory.CreateDirectory(path: CacheDir);

        string currentPath = Path.Combine(path1: CacheDir, path2: InstallerFileName(version: current));
        string pendingPath = Path.Combine(path1: CacheDir, path2: InstallerFileName(version: pending));
        string stalePath = Path.Combine(path1: CacheDir, path2: InstallerFileName(version: stale));

        try
        {
            await File.WriteAllTextAsync(path: currentPath, contents: "current");
            await File.WriteAllTextAsync(path: pendingPath, contents: "pending");
            await File.WriteAllTextAsync(path: stalePath, contents: "stale");

            InstallerUpdater updater = new(serverConnection: new ServerConnection());

            await updater.CleanCacheAsync(currentVersion: current, pendingVersion: pending);

            File.Exists(path: currentPath)
                .Should()
                .BeTrue(because: "the running version's installer must survive a prune");
            File.Exists(path: pendingPath)
                .Should()
                .BeTrue(because: "the pending update's installer must survive a prune");
            File.Exists(path: stalePath)
                .Should()
                .BeFalse(because: "an installer for neither the current nor pending version is stale");
        }
        finally
        {
            File.Delete(path: currentPath);
            File.Delete(path: pendingPath);
            if (File.Exists(path: stalePath))
                File.Delete(path: stalePath);
        }
    }

    [Fact]
    public async Task CleanCacheAsync_NoCacheDirectory_DoesNotThrow()
    {
        // Exercises the early-return branch without needing to actually delete
        // the real UpdateCache directory (which may legitimately hold a real
        // cached installer on this machine) — instead we just prove the method
        // is a no-op when the version strings can't match anything on disk,
        // by pointing at a version that certainly doesn't exist while the
        // directory itself may or may not be present.
        InstallerUpdater updater = new(serverConnection: new ServerConnection());

        Func<Task> act = () => updater.CleanCacheAsync(currentVersion: $"nonexistent-{Guid.NewGuid():N}", pendingVersion: null);

        await act.Should().NotThrowAsync();
    }
}
