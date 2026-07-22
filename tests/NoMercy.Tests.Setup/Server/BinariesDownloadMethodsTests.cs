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
using System.Text;
using Newtonsoft.Json;
using NoMercy.NmSystem.Information;
using NoMercy.Setup.Dto;
using NoMercy.Setup.Server;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Setup.Server;

/// <summary>
/// Requirement: every one of <see cref="Binaries"/>'s per-dependency download methods
/// (App/Launcher/CLI/yt-dlp/shaka-packager/cloudflared/Whisper models/Tesseract data)
/// must: skip re-downloading when the installed version is already current, refuse to
/// proceed when the release has no assets at all, and skip cleanly (log + return, never
/// throw) when none of the release's assets match the running platform. Multi-part
/// downloads (Whisper) must concatenate parts in order and clean up the parts afterward.
/// </summary>
/// <remarks>
/// CI runs every test job on a single ubuntu-latest runner — there is no separate
/// per-OS leg. Each Download* happy-path test therefore registers every platform's
/// asset name against the same payload/digest (mirroring <c>ServerUpdateDecisionTests
/// .ServerAssets()</c>) so the platform switch inside <see cref="Binaries"/> resolves
/// identically whether the suite runs on a dev's Windows machine or ubuntu-latest.
/// <c>NOMERCY_APP_PATH</c> isolation matches <c>ServerUpdateDecisionTests</c>.
/// </remarks>
[Trait(name: "Category", value: "Unit")]
public sealed class BinariesDownloadMethodsTests : IDisposable
{
    private readonly string? _originalAppPath;
    private readonly string? _originalInstallDir;
    private readonly Version? _originalSoftwareVersion;
    private readonly string _tempAppPath;

    public BinariesDownloadMethodsTests()
    {
        _originalAppPath = Environment.GetEnvironmentVariable(variable: "NOMERCY_APP_PATH");
        _originalInstallDir = Environment.GetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR");
        _originalSoftwareVersion = NoMercy.NmSystem.Information.Software.Version;

        _tempAppPath = Path.Combine(
            path1: Path.GetTempPath(),
            path2: $"nm-binmethods-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(path: _tempAppPath);
        Environment.SetEnvironmentVariable(variable: "NOMERCY_APP_PATH", value: _tempAppPath);
        Environment.SetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR", value: null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(variable: "NOMERCY_APP_PATH", value: _originalAppPath);
        Environment.SetEnvironmentVariable(
            variable: "NOMERCY_INSTALL_DIR",
            value: _originalInstallDir
        );
        NoMercy.NmSystem.Information.Software.Version = _originalSoftwareVersion;
        try
        {
            if (Directory.Exists(path: _tempAppPath))
                Directory.Delete(path: _tempAppPath, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static Binaries BuildBinaries(FakeHttpHandler handler)
    {
        LocalStorageDriver driver = new();
        StoragePathGuard guard = new(allowedRoots: [], driver: driver);
        LocalStorage storage = new(driver: driver, guard: guard);
        HttpClient http = new(handler: handler);
        return new(driver: driver, storage: storage, httpClient: http);
    }

    private static GithubReleaseResponse ReleaseWithAssets(params Asset[] assets) =>
        ReleaseWithAssets(
            publishedAt: DateTimeOffset.UtcNow.AddDays(days: -1),
            tagName: "v1.0.0",
            assets: assets
        );

    private static GithubReleaseResponse ReleaseWithAssets(
        DateTimeOffset publishedAt,
        string tagName,
        params Asset[] assets
    ) =>
        new()
        {
            TagName = tagName,
            PublishedAt = publishedAt,
            Assets = assets,
        };

    private static Asset MakeAsset(string name, string url, byte[]? digestOf = null) =>
        new()
        {
            Name = name,
            BrowserDownloadUrl = new(uriString: url),
            Size = 1,
            Digest = digestOf is null
                ? string.Empty
                : "sha256:"
                    + Convert
                        .ToHexString(inArray: SHA256.HashData(source: digestOf))
                        .ToLowerInvariant(),
        };

    // -------------------------------------------------------------------------
    // DownloadApp
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadApp_NoAssetsInRelease_LogsAndReturnsWithoutThrowing()
    {
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);

        await binaries.DownloadApp();

        Assert.False(condition: File.Exists(path: AppFiles.AppExePath));
    }

    [Fact]
    public async Task DownloadApp_NoWindowsAssetInRelease_SkipsWithoutThrowing()
    {
        byte[] payload = "irrelevant"u8.ToArray();
        FakeHttpHandler handler = new();
        handler.Register(url: "https://example.com/linux-only", body: payload);
        Binaries binaries = BuildBinaries(handler: handler);

        await binaries.DownloadApp();

        Assert.False(condition: File.Exists(path: AppFiles.AppExePath));
    }

    [Fact]
    public async Task DownloadApp_WindowsAssetPresent_DownloadsAndVerifies()
    {
        byte[] payload = "nomercy-app-binary"u8.ToArray();
        string assetUrl = "https://example.com/NoMercyApp-windows-x64.exe";
        FakeHttpHandler handler = new();
        handler.Register(url: assetUrl, body: payload);

        // Register the GithubMediaServerApiUrl release JSON isn't needed — DownloadApp
        // calls GetLatestReleaseInfo which fetches from the API URL. Since GetLatestReleaseInfo
        // isn't mockable via asset registration alone, this test drives DownloadApp through
        // GetLatestReleaseInfo's own real HTTP call — registered below by URL.
        // Every platform's asset name points at the same registered URL/payload so
        // this test resolves identically whether it runs on a dev's Windows machine
        // or the ubuntu-latest CI runner (RuntimeInformation.IsOSPlatform selects a
        // different branch/asset-name on each — there is no separate per-OS CI leg).
        handler.RegisterReleaseInfo(
            apiUrl: "https://api.github.com/repos/NoMercy-Entertainment/nomercy-media-server/releases/latest",
            release: ReleaseWithAssets(
                assets:
                [
                    MakeAsset(name: "NoMercyApp-windows-x64.exe", url: assetUrl, digestOf: payload),
                    MakeAsset(name: "NoMercyApp-linux-x64", url: assetUrl, digestOf: payload),
                    MakeAsset(name: "NoMercyApp-linux-arm64", url: assetUrl, digestOf: payload),
                    MakeAsset(name: "NoMercyApp-macos-x64.dmg", url: assetUrl, digestOf: payload),
                ]
            )
        );

        Binaries binaries = BuildBinaries(handler: handler);

        await binaries.DownloadApp();

        Assert.True(condition: File.Exists(path: AppFiles.AppExePath));
        Assert.Equal(
            expected: payload,
            actual: await File.ReadAllBytesAsync(path: AppFiles.AppExePath)
        );
    }

    [Fact]
    public async Task DownloadApp_AlreadyCurrentVersion_SkipsDownload()
    {
        Directory.CreateDirectory(path: AppFiles.BinariesPath);
        await File.WriteAllTextAsync(path: AppFiles.AppExePath, contents: "existing-app-binary");
        // CheckLocalVersion compares file LastModified against the release's PublishedAt —
        // touch the file to a time after the release so it reads as already current.
        File.SetLastWriteTimeUtc(path: AppFiles.AppExePath, lastWriteTimeUtc: DateTime.UtcNow);

        FakeHttpHandler handler = new();
        handler.RegisterReleaseInfo(
            apiUrl: "https://api.github.com/repos/NoMercy-Entertainment/nomercy-media-server/releases/latest",
            release: ReleaseWithAssets(
                publishedAt: DateTimeOffset.UtcNow.AddDays(days: -10),
                tagName: "v1.0.0",
                assets: MakeAsset(
                    name: "NoMercyApp-windows-x64.exe",
                    url: "https://example.com/should-not-be-fetched"
                )
            )
        );

        Binaries binaries = BuildBinaries(handler: handler);
        string originalContent = await File.ReadAllTextAsync(path: AppFiles.AppExePath);

        await binaries.DownloadApp();

        Assert.Equal(
            expected: originalContent,
            actual: await File.ReadAllTextAsync(path: AppFiles.AppExePath)
        );
    }

    [Fact]
    public async Task DownloadApp_ExistsInInstalledDirectory_SkipsDownload()
    {
        string installDir = Path.Combine(path1: _tempAppPath, path2: "installer-dir");
        Directory.CreateDirectory(path: installDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(
                path1: installDir,
                path2: "NoMercyApp" + NoMercy.NmSystem.Information.Info.ExecSuffix
            ),
            contents: "installed-app"
        );
        Environment.SetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR", value: installDir);

        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);

        await binaries.DownloadApp();

        Assert.False(condition: File.Exists(path: AppFiles.AppExePath));
    }

    // -------------------------------------------------------------------------
    // DownloadLauncher / DownloadCli — same shape as DownloadApp, distinct asset names
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadLauncher_WindowsAssetPresent_DownloadsAndVerifies()
    {
        byte[] payload = "nomercy-launcher-binary"u8.ToArray();
        string assetUrl = "https://example.com/NoMercyLauncher-windows-x64.exe";
        FakeHttpHandler handler = new();
        handler.Register(url: assetUrl, body: payload);
        handler.RegisterReleaseInfo(
            apiUrl: "https://api.github.com/repos/NoMercy-Entertainment/nomercy-media-server/releases/latest",
            release: ReleaseWithAssets(
                assets:
                [
                    MakeAsset(
                        name: "NoMercyLauncher-windows-x64.exe",
                        url: assetUrl,
                        digestOf: payload
                    ),
                    MakeAsset(name: "NoMercyLauncher-linux-x64", url: assetUrl, digestOf: payload),
                    MakeAsset(
                        name: "NoMercyLauncher-linux-arm64",
                        url: assetUrl,
                        digestOf: payload
                    ),
                    MakeAsset(name: "NoMercyLauncher-macos-x64", url: assetUrl, digestOf: payload),
                ]
            )
        );

        Binaries binaries = BuildBinaries(handler: handler);
        await binaries.DownloadLauncher();

        Assert.True(condition: File.Exists(path: AppFiles.LauncherExePath));
    }

    [Fact]
    public async Task DownloadLauncher_NoAssetsInRelease_DoesNotThrow()
    {
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);

        await binaries.DownloadLauncher();

        Assert.False(condition: File.Exists(path: AppFiles.LauncherExePath));
    }

    [Fact]
    public async Task DownloadCli_WindowsAssetPresent_DownloadsAndVerifies()
    {
        byte[] payload = "nomercy-cli-binary"u8.ToArray();
        string assetUrl = "https://example.com/nomercy-windows-x64.exe";
        FakeHttpHandler handler = new();
        handler.Register(url: assetUrl, body: payload);
        handler.RegisterReleaseInfo(
            apiUrl: "https://api.github.com/repos/NoMercy-Entertainment/nomercy-media-server/releases/latest",
            release: ReleaseWithAssets(
                assets:
                [
                    MakeAsset(name: "nomercy-windows-x64.exe", url: assetUrl, digestOf: payload),
                    MakeAsset(name: "nomercy-linux-x64", url: assetUrl, digestOf: payload),
                    MakeAsset(name: "nomercy-linux-arm64", url: assetUrl, digestOf: payload),
                    MakeAsset(name: "nomercy-macos-x64", url: assetUrl, digestOf: payload),
                ]
            )
        );

        Binaries binaries = BuildBinaries(handler: handler);
        await binaries.DownloadCli();

        Assert.True(condition: File.Exists(path: AppFiles.CliExePath));
    }

    [Fact]
    public async Task DownloadCli_ExistsInInstalledDirectory_SkipsDownload()
    {
        string installDir = Path.Combine(path1: _tempAppPath, path2: "installer-dir-cli");
        Directory.CreateDirectory(path: installDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(
                path1: installDir,
                path2: "nomercy" + NoMercy.NmSystem.Information.Info.ExecSuffix
            ),
            contents: "installed-cli"
        );
        Environment.SetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR", value: installDir);

        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);

        await binaries.DownloadCli();

        Assert.False(condition: File.Exists(path: AppFiles.CliExePath));
    }

    // -------------------------------------------------------------------------
    // DownloadYtdlp — uses upstream SHA2-256SUMS, plus a third-party age gate
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadYtdlp_ReleaseOldEnough_DownloadsAndVerifiesAgainstUpstreamSums()
    {
        byte[] payload = "yt-dlp-binary"u8.ToArray();
        string sha256 = Convert
            .ToHexString(inArray: SHA256.HashData(source: payload))
            .ToLowerInvariant();
        string assetUrl = "https://example.com/yt-dlp_x86.exe";
        string sumsUrl = "https://example.com/SHA2-256SUMS";

        FakeHttpHandler handler = new();
        handler.Register(url: assetUrl, body: payload);
        // All platform asset names share the same payload/hash here, so the sums file
        // lists every name DownloadYtdlp might select depending on which OS runs this
        // test (Windows locally, ubuntu-latest x64 in CI — there is no separate per-OS
        // CI leg despite this suite's original assumption otherwise).
        handler.Register(
            url: sumsUrl,
            body: Encoding.ASCII.GetBytes(
                s: $"{sha256}  yt-dlp_x86.exe\n{sha256}  yt-dlp_linux\n{sha256}  yt-dlp_linux_aarch64\n{sha256}  yt-dlp_macos\n"
            )
        );

        GithubReleaseResponse release = ReleaseWithAssets(
            publishedAt: DateTimeOffset.UtcNow.AddDays(days: -30),
            tagName: "v1.0.0",
            assets:
            [
                MakeAsset(name: "yt-dlp_x86.exe", url: assetUrl),
                MakeAsset(name: "yt-dlp_linux", url: assetUrl),
                MakeAsset(name: "yt-dlp_linux_aarch64", url: assetUrl),
                MakeAsset(name: "yt-dlp_macos", url: assetUrl),
                MakeAsset(name: "SHA2-256SUMS", url: sumsUrl),
            ]
        );

        handler.RegisterReleaseList(
            listUrl: "https://api.github.com/repos/yt-dlp/yt-dlp/releases?per_page=30",
            releases: [release]
        );

        Binaries binaries = BuildBinaries(handler: handler);
        await binaries.DownloadYtdlp();

        Assert.True(condition: File.Exists(path: AppFiles.YtdlpPath));
    }

    [Fact]
    public async Task DownloadYtdlp_OnlyTooNewReleaseAvailable_KeepsExistingBinary()
    {
        Directory.CreateDirectory(path: AppFiles.DependenciesPath);
        await File.WriteAllTextAsync(path: AppFiles.YtdlpPath, contents: "existing-ytdlp");

        FakeHttpHandler handler = new();
        GithubReleaseResponse tooNew = ReleaseWithAssets(
            publishedAt: DateTimeOffset.UtcNow.AddDays(days: -1),
            tagName: "v1.0.0",
            assets: MakeAsset(name: "yt-dlp_x86.exe", url: "https://example.com/should-not-fetch")
        );
        handler.RegisterReleaseList(
            listUrl: "https://api.github.com/repos/yt-dlp/yt-dlp/releases?per_page=30",
            releases: [tooNew]
        );

        Binaries binaries = BuildBinaries(handler: handler);
        await binaries.DownloadYtdlp();

        Assert.Equal(
            expected: "existing-ytdlp",
            actual: await File.ReadAllTextAsync(path: AppFiles.YtdlpPath)
        );
    }

    [Fact]
    public async Task DownloadYtdlp_NoAssetsInEligibleRelease_DoesNotThrow()
    {
        FakeHttpHandler handler = new();
        handler.RegisterReleaseList(
            listUrl: "https://api.github.com/repos/yt-dlp/yt-dlp/releases?per_page=30",
            releases: []
        );

        Binaries binaries = BuildBinaries(handler: handler);
        await binaries.DownloadYtdlp();

        Assert.False(condition: File.Exists(path: AppFiles.YtdlpPath));
    }

    // -------------------------------------------------------------------------
    // DownloadFfmpeg — the only Download* method whose asset is an archive that
    // must be extracted (not a raw single executable) after verification.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadFfmpeg_NoAssetsInRelease_NoExistingBinary_ThrowsInvalidOperation()
    {
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);

        // FFmpeg is the one dependency the encoder cannot run without at all — an
        // empty release AND nothing on disk must fail loudly (retried by
        // DegradedModeRecovery), not silently leave the server encoder-less.
        await Assert.ThrowsAsync<InvalidOperationException>(testCode: () =>
            binaries.DownloadFfmpeg()
        );
    }

    [Fact]
    public async Task DownloadFfmpeg_NoAssetsInRelease_ExistingBinaryOnDisk_KeepsIt()
    {
        Directory.CreateDirectory(path: AppFiles.FfmpegFolder);
        await File.WriteAllTextAsync(path: AppFiles.FfmpegPath, contents: "existing-ffmpeg");

        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);

        await binaries.DownloadFfmpeg();

        Assert.Equal(
            expected: "existing-ffmpeg",
            actual: await File.ReadAllTextAsync(path: AppFiles.FfmpegPath)
        );
    }

    [Fact]
    public async Task DownloadFfmpeg_AlreadyCurrentVersion_SkipsDownload()
    {
        Directory.CreateDirectory(path: AppFiles.FfmpegFolder);
        await File.WriteAllTextAsync(path: AppFiles.FfmpegPath, contents: "existing-ffmpeg");
        File.SetLastWriteTimeUtc(path: AppFiles.FfmpegPath, lastWriteTimeUtc: DateTime.UtcNow);

        FakeHttpHandler handler = new();
        handler.RegisterReleaseInfo(
            apiUrl: "https://api.github.com/repos/NoMercy-Entertainment/nomercy-ffmpeg/releases/latest",
            release: ReleaseWithAssets(
                publishedAt: DateTimeOffset.UtcNow.AddDays(days: -10),
                tagName: "v1.0.0",
                assets: MakeAsset(
                    name: "ffmpeg-windows-x64.zip",
                    url: "https://example.com/should-not-fetch"
                )
            )
        );

        Binaries binaries = BuildBinaries(handler: handler);
        await binaries.DownloadFfmpeg();

        Assert.Equal(
            expected: "existing-ffmpeg",
            actual: await File.ReadAllTextAsync(path: AppFiles.FfmpegPath)
        );
    }

    [Fact]
    public async Task DownloadFfmpeg_NoWindowsAsset_KeepsExistingBinaryWithoutThrowing()
    {
        Directory.CreateDirectory(path: AppFiles.FfmpegFolder);
        await File.WriteAllTextAsync(path: AppFiles.FfmpegPath, contents: "existing-ffmpeg");

        FakeHttpHandler handler = new();
        handler.RegisterReleaseInfo(
            apiUrl: "https://api.github.com/repos/NoMercy-Entertainment/nomercy-ffmpeg/releases/latest",
            release: ReleaseWithAssets(
                assets: MakeAsset(
                    name: "ffmpeg-linux-only.tar.gz",
                    url: "https://example.com/linux"
                )
            )
        );

        Binaries binaries = BuildBinaries(handler: handler);
        await binaries.DownloadFfmpeg();

        Assert.Equal(
            expected: "existing-ffmpeg",
            actual: await File.ReadAllTextAsync(path: AppFiles.FfmpegPath)
        );
    }

    [Fact]
    public async Task DownloadFfmpeg_WindowsAssetPresent_DownloadsExtractsAndSetsExecutionPermissions()
    {
        byte[] zipBytes = BuildFfmpegZip();
        string assetUrl = "https://example.com/ffmpeg-windows-x64.zip";

        FakeHttpHandler handler = new();
        handler.Register(url: assetUrl, body: zipBytes);
        // DownloadFfmpeg's Linux branch requires the asset name to Contain both
        // "linux" and "x86_64" (and Archiving.ExtractArchive dispatches on the
        // ".zip" suffix) — register that name too so this test resolves on the
        // ubuntu-latest CI runner as well as a dev's Windows machine.
        handler.RegisterReleaseInfo(
            apiUrl: "https://api.github.com/repos/NoMercy-Entertainment/nomercy-ffmpeg/releases/latest",
            release: ReleaseWithAssets(
                assets:
                [
                    MakeAsset(name: "ffmpeg-windows-x64.zip", url: assetUrl, digestOf: zipBytes),
                    MakeAsset(name: "ffmpeg-linux-x86_64.zip", url: assetUrl, digestOf: zipBytes),
                ]
            )
        );

        Binaries binaries = BuildBinaries(handler: handler);
        await binaries.DownloadFfmpeg();

        Assert.True(condition: File.Exists(path: AppFiles.FfmpegPath));
        Assert.True(condition: File.Exists(path: AppFiles.FfProbePath));
    }

    /// <summary>Builds a minimal real .zip archive containing both the Windows and
    /// Unix executable names DownloadFfmpeg can expect after extraction.</summary>
    private static byte[] BuildFfmpegZip()
    {
        using MemoryStream stream = new();
        using (
            System.IO.Compression.ZipArchive archive = new(
                stream: stream,
                mode: System.IO.Compression.ZipArchiveMode.Create,
                leaveOpen: true
            )
        )
        {
            foreach (
                string name in new[]
                {
                    "ffmpeg",
                    "ffprobe",
                    "ffplay",
                    "ffmpeg.exe",
                    "ffprobe.exe",
                    "ffplay.exe",
                }
            )
            {
                System.IO.Compression.ZipArchiveEntry entry = archive.CreateEntry(entryName: name);
                using Stream entryStream = entry.Open();
                using StreamWriter writer = new(stream: entryStream);
                writer.Write(value: "fake binary content for " + name);
            }
        }

        return stream.ToArray();
    }

    // -------------------------------------------------------------------------
    // DownloadShakaPackager
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadShakaPackager_ReleaseOldEnough_DownloadsAndVerifiesAgainstDigest()
    {
        byte[] payload = "packager-binary"u8.ToArray();
        string assetUrl = "https://example.com/packager-win-x64.exe";

        FakeHttpHandler handler = new();
        handler.Register(url: assetUrl, body: payload);

        GithubReleaseResponse release = ReleaseWithAssets(
            publishedAt: DateTimeOffset.UtcNow.AddDays(days: -30),
            tagName: "v1.0.0",
            assets:
            [
                MakeAsset(name: "packager-win-x64.exe", url: assetUrl, digestOf: payload),
                MakeAsset(name: "packager-linux-x64", url: assetUrl, digestOf: payload),
            ]
        );
        handler.RegisterReleaseList(
            listUrl: "https://api.github.com/repos/shaka-project/shaka-packager/releases?per_page=30",
            releases: [release]
        );

        Binaries binaries = BuildBinaries(handler: handler);
        await binaries.DownloadShakaPackager();

        Assert.True(condition: File.Exists(path: AppFiles.ShakaPackagerPath));
    }

    // -------------------------------------------------------------------------
    // DownloadCloudflared — no checksums published; relies on the age gate + digest
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadCloudflared_NoAssetsInRelease_DoesNotThrow()
    {
        FakeHttpHandler handler = new();
        handler.RegisterReleaseList(
            listUrl: "https://api.github.com/repos/cloudflare/cloudflared/releases?per_page=30",
            releases: []
        );

        Binaries binaries = BuildBinaries(handler: handler);
        await binaries.DownloadCloudflared();

        Assert.False(condition: File.Exists(path: AppFiles.CloudflareDPath));
    }

    [Fact]
    public async Task DownloadCloudflared_ReleaseOldEnough_NoWindowsAsset_SkipsWithoutThrowing()
    {
        FakeHttpHandler handler = new();
        GithubReleaseResponse release = ReleaseWithAssets(
            publishedAt: DateTimeOffset.UtcNow.AddDays(days: -30),
            tagName: "v1.0.0",
            assets: MakeAsset(
                name: "cloudflared-linux-arm64",
                url: "https://example.com/should-not-fetch"
            )
        );
        handler.RegisterReleaseList(
            listUrl: "https://api.github.com/repos/cloudflare/cloudflared/releases?per_page=30",
            releases: [release]
        );

        Binaries binaries = BuildBinaries(handler: handler);
        await binaries.DownloadCloudflared();

        Assert.False(condition: File.Exists(path: AppFiles.CloudflareDPath));
    }

    // NOTE: DownloadCloudflared's happy path beyond asset selection is NOT covered here.
    // Unlike every other Download* method, it fetches the binary via
    // NoMercy.NmSystem.SystemCalls.Download.DownloadFile, which uses its OWN static
    // HttpClient field — entirely separate from the HttpClient this test file injects
    // into Binaries — so a FakeHttpHandler registered on the Binaries instance never
    // intercepts that call. Reaching it means a real network round-trip to whatever
    // BrowserDownloadUrl the release lists. See NoMercy.NmSystem.SystemCalls.Download.cs:22
    // (private static readonly HttpClient) and Binaries.cs's DownloadCloudflared body
    // (the Downloader.DownloadFile call) for the exact lines this leaves uncovered:
    // digest verification success/failure and the darwin .tgz extraction branch are all
    // downstream of that same real HTTP call.

    // -------------------------------------------------------------------------
    // DownloadWhisperModels — single-part and multi-part concatenation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadWhisperModels_SinglePart_DownloadsDirectly()
    {
        byte[] payload = "whisper-model-single-part"u8.ToArray();
        string assetUrl = "https://example.com/ggml-large-v3.bin";

        FakeHttpHandler handler = new();
        handler.Register(url: assetUrl, body: payload);
        handler.RegisterReleaseInfo(
            apiUrl: "https://api.github.com/repos/NoMercy-Entertainment/nomercy-whisper-models/releases/latest",
            release: ReleaseWithAssets(
                assets: MakeAsset(name: "ggml-large-v3.bin", url: assetUrl, digestOf: payload)
            )
        );

        Binaries binaries = BuildBinaries(handler: handler);
        await binaries.DownloadWhisperModels(modelName: "ggml-large-v3");

        // Single-part downloads land directly at DependenciesPath/{assetName} —
        // ConcatenateModelParts (and its FfmpegFolder destination) only runs when
        // there is more than one part to merge (see the MultiPart test below).
        string destination = Path.Combine(
            path1: AppFiles.DependenciesPath,
            path2: "ggml-large-v3.bin"
        );
        Assert.True(condition: File.Exists(path: destination));
        Assert.Equal(expected: payload, actual: await File.ReadAllBytesAsync(path: destination));
    }

    [Fact]
    public async Task DownloadWhisperModels_MultiPart_ConcatenatesInOrderAndCleansUpParts()
    {
        byte[] part1 = "PART-ONE-"u8.ToArray();
        byte[] part2 = "PART-TWO"u8.ToArray();
        string url1 = "https://example.com/ggml-large-v3.bin.part1";
        string url2 = "https://example.com/ggml-large-v3.bin.part2";

        FakeHttpHandler handler = new();
        handler.Register(url: url1, body: part1);
        handler.Register(url: url2, body: part2);
        handler.RegisterReleaseInfo(
            apiUrl: "https://api.github.com/repos/NoMercy-Entertainment/nomercy-whisper-models/releases/latest",
            release: ReleaseWithAssets(
                assets:
                [
                    MakeAsset(name: "ggml-large-v3.bin.part1", url: url1, digestOf: part1),
                    MakeAsset(name: "ggml-large-v3.bin.part2", url: url2, digestOf: part2),
                ]
            )
        );

        Binaries binaries = BuildBinaries(handler: handler);
        await binaries.DownloadWhisperModels(modelName: "ggml-large-v3");

        string destination = Path.Combine(path1: AppFiles.FfmpegFolder, path2: "ggml-large-v3.bin");
        Assert.True(condition: File.Exists(path: destination));
        byte[] concatenated = await File.ReadAllBytesAsync(path: destination);
        Assert.Equal(
            expected: Encoding.UTF8.GetBytes(s: "PART-ONE-PART-TWO"),
            actual: concatenated
        );

        // Parts must be cleaned up once concatenated into the final model file.
        Assert.False(
            condition: File.Exists(
                path: Path.Combine(
                    path1: AppFiles.DependenciesPath,
                    path2: "ggml-large-v3.bin.part1"
                )
            )
        );
        Assert.False(
            condition: File.Exists(
                path: Path.Combine(
                    path1: AppFiles.DependenciesPath,
                    path2: "ggml-large-v3.bin.part2"
                )
            )
        );
    }

    [Fact]
    public async Task DownloadWhisperModels_NoMatchingAssets_DoesNotThrow()
    {
        FakeHttpHandler handler = new();
        handler.RegisterReleaseInfo(
            apiUrl: "https://api.github.com/repos/NoMercy-Entertainment/nomercy-whisper-models/releases/latest",
            release: ReleaseWithAssets(
                assets: MakeAsset(name: "unrelated-file.bin", url: "https://example.com/unrelated")
            )
        );

        Binaries binaries = BuildBinaries(handler: handler);
        await binaries.DownloadWhisperModels(modelName: "ggml-large-v3");

        string destination = Path.Combine(path1: AppFiles.FfmpegFolder, path2: "ggml-large-v3.bin");
        Assert.False(condition: File.Exists(path: destination));
    }

    // -------------------------------------------------------------------------
    // DownloadTesseractData — per-language loop
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadTesseractData_MultipleLanguages_DownloadsEachAndSkipsMissing()
    {
        byte[] engPayload = "eng-model"u8.ToArray();
        string engUrl = "https://example.com/eng.traineddata";

        FakeHttpHandler handler = new();
        handler.Register(url: engUrl, body: engPayload);
        handler.RegisterReleaseInfo(
            apiUrl: "https://api.github.com/repos/NoMercy-Entertainment/nomercy-tesseract/releases/latest",
            release: ReleaseWithAssets(
                assets: MakeAsset(name: "eng.traineddata", url: engUrl, digestOf: engPayload)
            )
        );

        Binaries binaries = BuildBinaries(handler: handler);

        // "jpn" has no matching asset in the release — must be skipped, not thrown.
        await binaries.DownloadTesseractData(languages: ["eng", "jpn"]);

        Assert.True(
            condition: File.Exists(
                path: Path.Combine(path1: AppFiles.TesseractModelsFolder, path2: "eng.traineddata")
            )
        );
        Assert.False(
            condition: File.Exists(
                path: Path.Combine(path1: AppFiles.TesseractModelsFolder, path2: "jpn.traineddata")
            )
        );
    }

    [Fact]
    public async Task DownloadTesseractData_NoAssetsInRelease_DoesNotThrow()
    {
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);

        await binaries.DownloadTesseractData(languages: ["eng"]);

        Assert.False(
            condition: File.Exists(
                path: Path.Combine(path1: AppFiles.TesseractModelsFolder, path2: "eng.traineddata")
            )
        );
    }

    // -------------------------------------------------------------------------
    // ExistsInInstalledDirectory / CheckLocalVersion — direct unit coverage
    // -------------------------------------------------------------------------

    [Fact]
    public void ExistsInInstalledDirectory_NoInstallDirSet_ChecksOwnDirectoryOnly()
    {
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);

        bool result = binaries.ExistsInInstalledDirectory(
            executableName: "definitely-not-a-real-executable.exe"
        );

        Assert.False(condition: result);
    }

    [Fact]
    public void ExistsInInstalledDirectory_InstallDirSetButFileMissing_ReturnsFalse()
    {
        string installDir = Path.Combine(path1: _tempAppPath, path2: "empty-install-dir");
        Directory.CreateDirectory(path: installDir);
        Environment.SetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR", value: installDir);

        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);

        bool result = binaries.ExistsInInstalledDirectory(executableName: "missing-executable.exe");

        Assert.False(condition: result);
    }

    [Fact]
    public void CheckLocalVersion_FileDoesNotExist_ReturnsFalse()
    {
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);
        GithubReleaseResponse release = ReleaseWithAssets(
            publishedAt: DateTimeOffset.UtcNow.AddDays(days: -1),
            tagName: "v2.0.0"
        );

        bool result = binaries.CheckLocalVersion(
            releaseInfo: release,
            destination: Path.Combine(path1: _tempAppPath, path2: "nonexistent.bin"),
            version: out string version
        );

        Assert.False(condition: result);
        Assert.Equal(expected: "2.0.0", actual: version);
    }

    [Fact]
    public async Task CheckLocalVersion_FileOlderThanRelease_ReturnsFalse()
    {
        string path = Path.Combine(path1: _tempAppPath, path2: "old-file.bin");
        await File.WriteAllTextAsync(path: path, contents: "old");
        File.SetLastWriteTimeUtc(path: path, lastWriteTimeUtc: DateTime.UtcNow.AddDays(value: -10));

        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);
        GithubReleaseResponse release = ReleaseWithAssets(
            publishedAt: DateTimeOffset.UtcNow,
            tagName: "v3.0.0"
        );

        bool result = binaries.CheckLocalVersion(
            releaseInfo: release,
            destination: path,
            version: out string version
        );

        Assert.False(condition: result);
        Assert.Equal(expected: "3.0.0", actual: version);
    }

    [Fact]
    public void CheckLocalVersion_TagWithoutVPrefix_StripsNothing()
    {
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);
        GithubReleaseResponse release = ReleaseWithAssets(
            publishedAt: DateTimeOffset.UtcNow.AddDays(days: -1),
            tagName: "2024.01.01"
        );

        binaries.CheckLocalVersion(
            releaseInfo: release,
            destination: Path.Combine(path1: _tempAppPath, path2: "nope.bin"),
            version: out string version
        );

        Assert.Equal(expected: "2024.01.01", actual: version);
    }

    // -------------------------------------------------------------------------
    // VerifyAssetDigestOrThrow — used by third-party binaries downloaded outside
    // DownloadWithVerificationAsync (currently only DownloadCloudflared).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task VerifyAssetDigestOrThrow_NoDigestOnAsset_SkipsCheckWithoutThrowing()
    {
        string path = Path.Combine(path1: _tempAppPath, path2: "no-digest.bin");
        await File.WriteAllTextAsync(path: path, contents: "some content");
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);

        await binaries.VerifyAssetDigestOrThrow(
            path: path,
            asset: MakeAsset(name: "no-digest.bin", url: "https://example.com/x"),
            label: "test-asset"
        );

        Assert.True(condition: File.Exists(path: path));
    }

    [Fact]
    public async Task VerifyAssetDigestOrThrow_NullAsset_SkipsCheckWithoutThrowing()
    {
        string path = Path.Combine(path1: _tempAppPath, path2: "no-asset.bin");
        await File.WriteAllTextAsync(path: path, contents: "some content");
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);

        await binaries.VerifyAssetDigestOrThrow(path: path, asset: null, label: "test-asset");

        Assert.True(condition: File.Exists(path: path));
    }

    [Fact]
    public async Task VerifyAssetDigestOrThrow_MatchingDigest_DoesNotThrowOrDeleteFile()
    {
        byte[] payload = "verified content"u8.ToArray();
        string path = Path.Combine(path1: _tempAppPath, path2: "matching.bin");
        await File.WriteAllBytesAsync(path: path, bytes: payload);
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);

        await binaries.VerifyAssetDigestOrThrow(
            path: path,
            asset: MakeAsset(name: "matching.bin", url: "https://example.com/x", digestOf: payload),
            label: "test-asset"
        );

        Assert.True(condition: File.Exists(path: path));
    }

    [Fact]
    public async Task VerifyAssetDigestOrThrow_MismatchedDigest_DeletesFileAndThrows()
    {
        byte[] payload = "tampered content"u8.ToArray();
        string path = Path.Combine(path1: _tempAppPath, path2: "mismatched.bin");
        await File.WriteAllBytesAsync(path: path, bytes: payload);
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler: handler);

        await Assert.ThrowsAsync<InvalidDataException>(testCode: () =>
            binaries.VerifyAssetDigestOrThrow(
                path: path,
                asset: MakeAsset(
                    name: "mismatched.bin",
                    url: "https://example.com/x",
                    digestOf: "different content entirely"u8.ToArray()
                ),
                label: "test-asset"
            )
        );

        Assert.False(condition: File.Exists(path: path));
    }

    // -------------------------------------------------------------------------
    // DownloadAll — orchestration across every dependency, plus the "up to date"
    // report table it builds when one or more binaries are skipped.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadAll_EveryReleaseHasNoAssets_CompletesWithoutThrowing()
    {
        // DownloadFfmpeg has a hard-fail branch when its release has no assets AND
        // ffmpeg isn't already on disk — seed a placeholder so DownloadAll's sequential
        // await chain reaches every later step instead of faulting at ffmpeg.
        Directory.CreateDirectory(path: AppFiles.FfmpegFolder);
        await File.WriteAllTextAsync(path: AppFiles.FfmpegPath, contents: "placeholder-ffmpeg");

        FakeHttpHandler handler = new();
        foreach (
            string apiUrl in new[]
            {
                "https://api.github.com/repos/NoMercy-Entertainment/nomercy-media-server/releases/latest",
                "https://api.github.com/repos/NoMercy-Entertainment/nomercy-ffmpeg/releases/latest",
                "https://api.github.com/repos/NoMercy-Entertainment/nomercy-tesseract/releases/latest",
                "https://api.github.com/repos/NoMercy-Entertainment/nomercy-whisper-models/releases/latest",
            }
        )
            handler.RegisterReleaseInfo(apiUrl: apiUrl, release: ReleaseWithAssets());

        foreach (
            string listUrl in new[]
            {
                "https://api.github.com/repos/yt-dlp/yt-dlp/releases?per_page=30",
                "https://api.github.com/repos/cloudflare/cloudflared/releases?per_page=30",
                "https://api.github.com/repos/shaka-project/shaka-packager/releases?per_page=30",
            }
        )
            handler.RegisterReleaseList(listUrl: listUrl, releases: []);

        Binaries binaries = BuildBinaries(handler: handler);

        await binaries.DownloadAll();
    }

    [Fact]
    public async Task DownloadAll_SomeBinariesAlreadyCurrent_BuildsUpToDateReportWithoutThrowing()
    {
        // Pre-seed App/Launcher/CLI as already current so DownloadAll's post-run report
        // table (3-column layout, built only when _binaryReport is non-empty) actually
        // has entries to format — the report path is otherwise never exercised.
        Directory.CreateDirectory(path: AppFiles.BinariesPath);
        Directory.CreateDirectory(path: AppFiles.FfmpegFolder);
        await File.WriteAllTextAsync(path: AppFiles.FfmpegPath, contents: "placeholder-ffmpeg");

        // DownloadServerUpdate (also invoked by DownloadAll) compares the running
        // version against the release tag — pin it to match "v1.0.0" below so it takes
        // the AlreadyUpToDate branch instead of attempting a real download of the
        // unregistered "https://example.com/server" placeholder URL.
        NoMercy.NmSystem.Information.Software.Version = new(major: 1, minor: 0, build: 0);

        DateTimeOffset publishedAt = DateTimeOffset.UtcNow.AddDays(days: -10);
        foreach (
            string exePath in new[]
            {
                AppFiles.AppExePath,
                AppFiles.LauncherExePath,
                AppFiles.CliExePath,
            }
        )
        {
            await File.WriteAllTextAsync(path: exePath, contents: "already-current");
            File.SetLastWriteTimeUtc(path: exePath, lastWriteTimeUtc: DateTime.UtcNow);
        }

        FakeHttpHandler handler = new();
        handler.RegisterReleaseInfo(
            apiUrl: "https://api.github.com/repos/NoMercy-Entertainment/nomercy-media-server/releases/latest",
            release: ReleaseWithAssets(
                publishedAt: publishedAt,
                tagName: "v1.0.0",
                assets:
                [
                    MakeAsset(name: "NoMercyApp-windows-x64.exe", url: "https://example.com/app"),
                    MakeAsset(
                        name: "NoMercyLauncher-windows-x64.exe",
                        url: "https://example.com/launcher"
                    ),
                    MakeAsset(name: "nomercy-windows-x64.exe", url: "https://example.com/cli"),
                    MakeAsset(
                        name: "NoMercyMediaServer-windows-x64.exe",
                        url: "https://example.com/server"
                    ),
                ]
            )
        );
        handler.RegisterReleaseInfo(
            apiUrl: "https://api.github.com/repos/NoMercy-Entertainment/nomercy-ffmpeg/releases/latest",
            release: ReleaseWithAssets()
        );
        handler.RegisterReleaseInfo(
            apiUrl: "https://api.github.com/repos/NoMercy-Entertainment/nomercy-tesseract/releases/latest",
            release: ReleaseWithAssets()
        );
        handler.RegisterReleaseInfo(
            apiUrl: "https://api.github.com/repos/NoMercy-Entertainment/nomercy-whisper-models/releases/latest",
            release: ReleaseWithAssets()
        );
        foreach (
            string listUrl in new[]
            {
                "https://api.github.com/repos/yt-dlp/yt-dlp/releases?per_page=30",
                "https://api.github.com/repos/cloudflare/cloudflared/releases?per_page=30",
                "https://api.github.com/repos/shaka-project/shaka-packager/releases?per_page=30",
            }
        )
            handler.RegisterReleaseList(listUrl: listUrl, releases: []);

        Binaries binaries = BuildBinaries(handler: handler);

        await binaries.DownloadAll();

        Assert.Equal(
            expected: "already-current",
            actual: await File.ReadAllTextAsync(path: AppFiles.AppExePath)
        );
        Assert.Equal(
            expected: "already-current",
            actual: await File.ReadAllTextAsync(path: AppFiles.LauncherExePath)
        );
        Assert.Equal(
            expected: "already-current",
            actual: await File.ReadAllTextAsync(path: AppFiles.CliExePath)
        );
    }
}

/// <summary>
/// Stub <see cref="HttpMessageHandler"/> serving pre-registered byte-array responses by
/// exact URL, plus release-metadata JSON for the two shapes <see cref="Binaries"/>'s
/// internal fetchers need: a single "releases/latest" object
/// (<see cref="Binaries.GetLatestReleaseInfo"/>) and a "releases?per_page=30" array
/// (<see cref="Binaries.GetLatestReleaseInfoOlderThan"/>'s third-party age gate). Kept
/// local to this file rather than extending <c>BinaryDownloaderTests.FakeHttpHandler</c>,
/// matching this suite's existing convention (see <c>TesseractModelDownloaderTests</c>)
/// of not modifying shared test infrastructure another slice may depend on.
/// </summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, byte[]> _responses = new(
        comparer: StringComparer.OrdinalIgnoreCase
    );

    public void Register(string url, byte[] body) => _responses[key: url] = body;

    public void RegisterReleaseInfo(string apiUrl, GithubReleaseResponse release) =>
        Register(
            url: apiUrl,
            body: Encoding.UTF8.GetBytes(s: JsonConvert.SerializeObject(value: release))
        );

    public void RegisterReleaseList(string listUrl, GithubReleaseResponse[] releases) =>
        Register(
            url: listUrl,
            body: Encoding.UTF8.GetBytes(s: JsonConvert.SerializeObject(value: releases))
        );

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        string url = request.RequestUri?.ToString() ?? string.Empty;
        if (_responses.TryGetValue(key: url, value: out byte[]? body))
        {
            HttpResponseMessage ok = new(statusCode: HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content: body),
            };
            return Task.FromResult(result: ok);
        }

        return Task.FromResult(
            result: new HttpResponseMessage(statusCode: HttpStatusCode.NotFound)
        );
    }
}
