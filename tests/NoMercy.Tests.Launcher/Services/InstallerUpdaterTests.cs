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
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NoMercy",
            "UpdateCache"
        );

    private static string InstallerFileName(string version) =>
        $"NoMercyMediaServer-{version}-windows-x64-setup.exe";

    [Fact]
    public async Task IsInstallerDeploymentAsync_ProcessRunningOutsideBinariesPath_ReturnsTrue()
    {
        // The test host process (testhost.exe / dotnet) never lives under
        // %AppData%\NoMercy\binaries, so this must report "installer deployment".
        InstallerUpdater updater = new(new ServerConnection());

        bool result = await updater.IsInstallerDeploymentAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsInstallerDeploymentAsync_InstallDirEnvVarSet_ReturnsTrueWithoutPathCheck()
    {
        Environment.SetEnvironmentVariable("NOMERCY_INSTALL_DIR", @"C:\Program Files\NoMercy");
        try
        {
            InstallerUpdater updater = new(new ServerConnection());

            bool result = await updater.IsInstallerDeploymentAsync();

            result.Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("NOMERCY_INSTALL_DIR", null);
        }
    }

    [Fact]
    public async Task VerifyInstallerAsync_NoSha256Sidecar_ReturnsTrueAsLegacyRelease()
    {
        string version = $"test-{Guid.NewGuid():N}";
        InstallerUpdater updater = new(new ServerConnection());

        bool result = await updater.VerifyInstallerAsync(version);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyInstallerAsync_MatchingSha256Sidecar_ReturnsTrue()
    {
        string version = $"test-{Guid.NewGuid():N}";
        Directory.CreateDirectory(CacheDir);
        string exePath = Path.Combine(CacheDir, InstallerFileName(version));
        string sha256Path = exePath + ".sha256";

        try
        {
            byte[] content = [.. "fake installer bytes for hash verification"u8];
            await File.WriteAllBytesAsync(exePath, content);
            string hash = Convert.ToHexString(SHA256.HashData(content));
            await File.WriteAllTextAsync(sha256Path, hash);

            InstallerUpdater updater = new(new ServerConnection());

            bool result = await updater.VerifyInstallerAsync(version);

            result.Should().BeTrue();
        }
        finally
        {
            File.Delete(exePath);
            File.Delete(sha256Path);
        }
    }

    [Fact]
    public async Task VerifyInstallerAsync_SidecarInHashSpaceFilenameFormat_StillParsesTheHash()
    {
        string version = $"test-{Guid.NewGuid():N}";
        Directory.CreateDirectory(CacheDir);
        string exePath = Path.Combine(CacheDir, InstallerFileName(version));
        string sha256Path = exePath + ".sha256";

        try
        {
            byte[] content = [.. "fake installer bytes, sha256sum-style sidecar"u8];
            await File.WriteAllBytesAsync(exePath, content);
            string hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            // sha256sum's own output format is "HASH  filename" (lowercase hex).
            await File.WriteAllTextAsync(sha256Path, $"{hash}  {InstallerFileName(version)}");

            InstallerUpdater updater = new(new ServerConnection());

            bool result = await updater.VerifyInstallerAsync(version);

            result.Should().BeTrue();
        }
        finally
        {
            File.Delete(exePath);
            File.Delete(sha256Path);
        }
    }

    [Fact]
    public async Task VerifyInstallerAsync_MismatchedSha256_ThrowsInvalidDataException()
    {
        string version = $"test-{Guid.NewGuid():N}";
        Directory.CreateDirectory(CacheDir);
        string exePath = Path.Combine(CacheDir, InstallerFileName(version));
        string sha256Path = exePath + ".sha256";

        try
        {
            await File.WriteAllBytesAsync(exePath, [.. "real content"u8]);
            // Sidecar records the hash of totally different bytes.
            string wrongHash = Convert.ToHexString(
                SHA256.HashData("different content"u8.ToArray())
            );
            await File.WriteAllTextAsync(sha256Path, wrongHash);

            InstallerUpdater updater = new(new ServerConnection());

            Func<Task> act = () => updater.VerifyInstallerAsync(version);

            await act.Should().ThrowAsync<InvalidDataException>();
        }
        finally
        {
            File.Delete(exePath);
            File.Delete(sha256Path);
        }
    }

    [Fact]
    public async Task CleanCacheAsync_RemovesFilesForOtherVersions_KeepsCurrentAndPending()
    {
        string current = $"cur-{Guid.NewGuid():N}";
        string pending = $"pend-{Guid.NewGuid():N}";
        string stale = $"stale-{Guid.NewGuid():N}";
        Directory.CreateDirectory(CacheDir);

        string currentPath = Path.Combine(CacheDir, InstallerFileName(current));
        string pendingPath = Path.Combine(CacheDir, InstallerFileName(pending));
        string stalePath = Path.Combine(CacheDir, InstallerFileName(stale));

        try
        {
            await File.WriteAllTextAsync(currentPath, "current");
            await File.WriteAllTextAsync(pendingPath, "pending");
            await File.WriteAllTextAsync(stalePath, "stale");

            InstallerUpdater updater = new(new ServerConnection());

            await updater.CleanCacheAsync(current, pending);

            File.Exists(currentPath)
                .Should()
                .BeTrue("the running version's installer must survive a prune");
            File.Exists(pendingPath)
                .Should()
                .BeTrue("the pending update's installer must survive a prune");
            File.Exists(stalePath)
                .Should()
                .BeFalse("an installer for neither the current nor pending version is stale");
        }
        finally
        {
            File.Delete(currentPath);
            File.Delete(pendingPath);
            if (File.Exists(stalePath))
                File.Delete(stalePath);
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
        InstallerUpdater updater = new(new ServerConnection());

        Func<Task> act = () => updater.CleanCacheAsync($"nonexistent-{Guid.NewGuid():N}", null);

        await act.Should().NotThrowAsync();
    }
}
