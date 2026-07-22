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

using System.Net;
using System.Security.Cryptography;
using Newtonsoft.Json;
using NoMercy.NmSystem.Information;
using NoMercy.Setup.Dto;
using NoMercy.Setup.Server;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Setup;

/// <summary>
/// Characterizes the <see cref="Binaries.DownloadServerUpdate"/> decision matrix —
/// the logic that decides whether a new server binary is fetched and staged for a
/// self-hosted user. A regression here is high blast radius: it either strands users
/// on an old build, re-downloads the same binary on every check (the "stuck update
/// loop"), clobbers an installer deployment, or — worst — stages a downgrade. Each
/// <see cref="ServerUpdateResult"/> branch that can be reached without a real
/// version-stamped PE on disk is locked here.
/// </summary>
/// <remarks>
/// Testability: <see cref="Binaries"/> exposes an internal HttpClient constructor and
/// <see cref="Software.Version"/> is a settable static, so the running version and the
/// "latest" release are both controllable without the network. <c>NOMERCY_APP_PATH</c>
/// is isolated to a temp directory per the <c>DegradedModeStartupTests</c> pattern so
/// the release-metadata cache and any staged binary land in the temp tree — never the
/// developer's real app-data path, where a fabricated "latest" cache could otherwise
/// trigger a bogus real update.
///
/// The <see cref="ServerUpdateResult.RestartNeeded"/> branch is intentionally not
/// covered: it fires only when a binary already on disk reports a matching file
/// version, which requires a genuine version-stamped PE that a unit test cannot
/// synthesize (a plain file reads back as 0.0.0 → null). The download-and-stage
/// mechanics themselves are covered by <c>BinaryDownloaderTests</c>.
/// </remarks>
[Trait(name: "Category", value: "Unit")]
public sealed class ServerUpdateDecisionTests : IDisposable
{
    private readonly Version? _originalVersion;
    private readonly string? _originalAppPath;
    private readonly string? _originalInstallDir;
    private readonly string _tempAppPath;

    public ServerUpdateDecisionTests()
    {
        _originalVersion = Software.Version;
        _originalAppPath = Environment.GetEnvironmentVariable(variable: "NOMERCY_APP_PATH");
        _originalInstallDir = Environment.GetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR");

        _tempAppPath = Path.Combine(path1: Path.GetTempPath(), path2: $"nomercy-update-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempAppPath);
        Environment.SetEnvironmentVariable(variable: "NOMERCY_APP_PATH", value: _tempAppPath);

        // Installer deployments are opt-in via this variable; clear it so the default
        // test is a standalone deployment. Individual tests set it explicitly.
        Environment.SetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR", value: null);
    }

    public void Dispose()
    {
        Software.Version = _originalVersion;
        Environment.SetEnvironmentVariable(variable: "NOMERCY_APP_PATH", value: _originalAppPath);
        Environment.SetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR", value: _originalInstallDir);

        // Best-effort: the logger keeps its daily log file under the isolated app
        // path open for the process lifetime, so a recursive delete races that handle.
        // The temp tree is disposable either way — the OS temp sweep reclaims it.
        try
        {
            if (Directory.Exists(path: _tempAppPath))
                Directory.Delete(path: _tempAppPath, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task DownloadServerUpdate_NoAssetsPublished_ReturnsNoAssetFound()
    {
        Software.Version = new(major: 1, minor: 0, build: 0);
        Binaries binaries = BuildBinaries(handler: WithReleaseThen(tagName: "v9.9.9", assets: []));

        ServerUpdateResult result = await binaries.DownloadServerUpdate();

        result.Should().Be(expected: ServerUpdateResult.NoAssetFound);
    }

    [Fact]
    public async Task DownloadServerUpdate_LatestEqualsRunning_ReturnsAlreadyUpToDate()
    {
        // Idempotency: a check when already on the latest version must never download.
        Software.Version = new(major: 2, minor: 0, build: 1);
        Binaries binaries = BuildBinaries(handler: WithReleaseThen(tagName: "v2.0.1", assets: ServerAssets()));

        ServerUpdateResult result = await binaries.DownloadServerUpdate();

        result.Should().Be(expected: ServerUpdateResult.AlreadyUpToDate);
        StagedBinaryExists().Should().BeFalse(because: "an up-to-date server must not stage a binary");
    }

    [Fact]
    public async Task DownloadServerUpdate_LatestOlderThanRunning_ReturnsAlreadyUpToDate()
    {
        // Never downgrade: a running build ahead of GitHub's "latest" (e.g. a user on a
        // hotfix) must hold, not roll back to the older published release.
        Software.Version = new(major: 9, minor: 9, build: 9);
        Binaries binaries = BuildBinaries(handler: WithReleaseThen(tagName: "v1.0.0", assets: ServerAssets()));

        ServerUpdateResult result = await binaries.DownloadServerUpdate();

        result.Should().Be(expected: ServerUpdateResult.AlreadyUpToDate);
        StagedBinaryExists().Should().BeFalse(because: "a downgrade must never be staged");
    }

    [Fact]
    public async Task DownloadServerUpdate_InstallerDeployment_DefersToInstaller()
    {
        // An installer deployment owns its own update flow; the server must not fetch a
        // binary into the binaries path behind the installer's back.
        Software.Version = new(major: 1, minor: 0, build: 0);
        Environment.SetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR", value: _tempAppPath);
        Binaries binaries = BuildBinaries(handler: WithReleaseThen(tagName: "v9.9.9", assets: ServerAssets()));

        ServerUpdateResult result = await binaries.DownloadServerUpdate();

        result.Should().Be(expected: ServerUpdateResult.UseInstaller);
        StagedBinaryExists().Should().BeFalse(because: "an installer deployment must not stage a binary");
    }

    [Fact]
    public async Task DownloadServerUpdate_NewerReleaseAvailable_DownloadsAndStages()
    {
        // The happy path: a genuinely newer release on a standalone deployment is fetched,
        // integrity-checked against the GitHub asset digest, and staged for the next restart.
        Software.Version = new(major: 1, minor: 0, build: 0);
        byte[] payload = "newer server binary"u8.ToArray();
        string digest =
            "sha256:" + Convert.ToHexString(inArray: SHA256.HashData(source: payload)).ToLowerInvariant();

        SequencedHttpHandler handler = new();
        handler.Enqueue(factory: () => Json(release: BuildRelease(tagName: "v9.9.9", assets: ServerAssets(digest: digest))));
        handler.Enqueue(factory: () =>
            new HttpResponseMessage(statusCode: HttpStatusCode.OK) { Content = new ByteArrayContent(content: payload) }
        );

        Binaries binaries = BuildBinaries(handler: handler);

        ServerUpdateResult result = await binaries.DownloadServerUpdate();

        result.Should().Be(expected: ServerUpdateResult.Downloaded);
        StagedBinaryExists().Should().BeTrue(because: "a newer release must be staged for restart");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static bool StagedBinaryExists() =>
        File.Exists(path: AppFiles.ServerTempExePath)
        && new FileInfo(fileName: AppFiles.ServerTempExePath).Length > 0;

    private static SequencedHttpHandler WithReleaseThen(string tagName, Asset[] assets)
    {
        SequencedHttpHandler handler = new();
        handler.Enqueue(factory: () => Json(release: BuildRelease(tagName: tagName, assets: assets)));
        return handler;
    }

    private static HttpResponseMessage Json(GithubReleaseResponse release) =>
        new(statusCode: HttpStatusCode.OK)
        {
            Content = new StringContent(content: JsonConvert.SerializeObject(value: release)),
        };

    private static GithubReleaseResponse BuildRelease(string tagName, Asset[] assets) =>
        new()
        {
            TagName = tagName,
            PublishedAt = DateTimeOffset.UtcNow.AddDays(days: -1),
            Assets = assets,
        };

    // The full platform asset set so the release resolves on whichever OS the suite
    // runs on (Windows locally, Linux in CI coverage). Names mirror the production
    // switch in Binaries.DownloadServerUpdate.
    private static Asset[] ServerAssets(string digest = "") =>
        [
            ServerAsset(name: "NoMercyMediaServer-windows-x64.exe", digest: digest),
            ServerAsset(name: "NoMercyMediaServer-linux-x64", digest: digest),
            ServerAsset(name: "NoMercyMediaServer-linux-arm64", digest: digest),
            ServerAsset(name: "NoMercyMediaServer-macos-x64", digest: digest),
        ];

    private static Asset ServerAsset(string name, string digest) =>
        new()
        {
            Name = name,
            BrowserDownloadUrl = new(uriString: $"https://example.com/{name}"),
            Size = 1,
            Digest = digest,
        };

    private static Binaries BuildBinaries(SequencedHttpHandler handler)
    {
        LocalStorageDriver driver = new();
        StoragePathGuard guard = new(allowedRoots: [], driver: driver);
        LocalStorage storage = new(driver: driver, guard: guard);
        HttpClient http = new(handler: handler);
        return new(driver: driver, storage: storage, httpClient: http);
    }
}
