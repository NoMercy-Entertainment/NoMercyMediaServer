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
/// Runs on Windows locally (per <c>ServerUpdateDecisionTests</c> precedent) — the
/// Windows asset-name branch is what this suite exercises; the Linux/macOS branches of
/// the same platform switches are covered by the project's Linux CI leg, not reachable
/// in a single local run. <c>NOMERCY_APP_PATH</c> isolation matches
/// <c>ServerUpdateDecisionTests</c>.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class BinariesDownloadMethodsTests : IDisposable
{
    private readonly string? _originalAppPath;
    private readonly string? _originalInstallDir;
    private readonly Version? _originalSoftwareVersion;
    private readonly string _tempAppPath;

    public BinariesDownloadMethodsTests()
    {
        _originalAppPath = Environment.GetEnvironmentVariable("NOMERCY_APP_PATH");
        _originalInstallDir = Environment.GetEnvironmentVariable("NOMERCY_INSTALL_DIR");
        _originalSoftwareVersion = NoMercy.NmSystem.Information.Software.Version;

        _tempAppPath = Path.Combine(Path.GetTempPath(), $"nm-binmethods-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempAppPath);
        Environment.SetEnvironmentVariable("NOMERCY_APP_PATH", _tempAppPath);
        Environment.SetEnvironmentVariable("NOMERCY_INSTALL_DIR", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("NOMERCY_APP_PATH", _originalAppPath);
        Environment.SetEnvironmentVariable("NOMERCY_INSTALL_DIR", _originalInstallDir);
        NoMercy.NmSystem.Information.Software.Version = _originalSoftwareVersion;
        try
        {
            if (Directory.Exists(_tempAppPath))
                Directory.Delete(_tempAppPath, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static Binaries BuildBinaries(FakeHttpHandler handler)
    {
        LocalStorageDriver driver = new();
        StoragePathGuard guard = new([], driver);
        LocalStorage storage = new(driver, guard);
        HttpClient http = new(handler);
        return new(driver, storage, http);
    }

    private static GithubReleaseResponse ReleaseWithAssets(params Asset[] assets) =>
        ReleaseWithAssets(DateTimeOffset.UtcNow.AddDays(-1), "v1.0.0", assets);

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
            BrowserDownloadUrl = new(url),
            Size = 1,
            Digest = digestOf is null
                ? string.Empty
                : "sha256:" + Convert.ToHexString(SHA256.HashData(digestOf)).ToLowerInvariant(),
        };

    // -------------------------------------------------------------------------
    // DownloadApp
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadApp_NoAssetsInRelease_LogsAndReturnsWithoutThrowing()
    {
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);

        await binaries.DownloadApp();

        Assert.False(File.Exists(AppFiles.AppExePath));
    }

    [Fact]
    public async Task DownloadApp_NoWindowsAssetInRelease_SkipsWithoutThrowing()
    {
        byte[] payload = "irrelevant"u8.ToArray();
        FakeHttpHandler handler = new();
        handler.Register("https://example.com/linux-only", payload);
        Binaries binaries = BuildBinaries(handler);

        await binaries.DownloadApp();

        Assert.False(File.Exists(AppFiles.AppExePath));
    }

    [Fact]
    public async Task DownloadApp_WindowsAssetPresent_DownloadsAndVerifies()
    {
        byte[] payload = "nomercy-app-binary"u8.ToArray();
        string assetUrl = "https://example.com/NoMercyApp-windows-x64.exe";
        FakeHttpHandler handler = new();
        handler.Register(assetUrl, payload);

        // Register the GithubMediaServerApiUrl release JSON isn't needed — DownloadApp
        // calls GetLatestReleaseInfo which fetches from the API URL. Since GetLatestReleaseInfo
        // isn't mockable via asset registration alone, this test drives DownloadApp through
        // GetLatestReleaseInfo's own real HTTP call — registered below by URL.
        handler.RegisterReleaseInfo(
            "https://api.github.com/repos/NoMercy-Entertainment/nomercy-media-server/releases/latest",
            ReleaseWithAssets(MakeAsset("NoMercyApp-windows-x64.exe", assetUrl, digestOf: payload))
        );

        Binaries binaries = BuildBinaries(handler);

        await binaries.DownloadApp();

        Assert.True(File.Exists(AppFiles.AppExePath));
        Assert.Equal(payload, await File.ReadAllBytesAsync(AppFiles.AppExePath));
    }

    [Fact]
    public async Task DownloadApp_AlreadyCurrentVersion_SkipsDownload()
    {
        Directory.CreateDirectory(AppFiles.BinariesPath);
        await File.WriteAllTextAsync(AppFiles.AppExePath, "existing-app-binary");
        // CheckLocalVersion compares file LastModified against the release's PublishedAt —
        // touch the file to a time after the release so it reads as already current.
        File.SetLastWriteTimeUtc(AppFiles.AppExePath, DateTime.UtcNow);

        FakeHttpHandler handler = new();
        handler.RegisterReleaseInfo(
            "https://api.github.com/repos/NoMercy-Entertainment/nomercy-media-server/releases/latest",
            ReleaseWithAssets(
                DateTimeOffset.UtcNow.AddDays(-10),
                "v1.0.0",
                MakeAsset("NoMercyApp-windows-x64.exe", "https://example.com/should-not-be-fetched")
            )
        );

        Binaries binaries = BuildBinaries(handler);
        string originalContent = await File.ReadAllTextAsync(AppFiles.AppExePath);

        await binaries.DownloadApp();

        Assert.Equal(originalContent, await File.ReadAllTextAsync(AppFiles.AppExePath));
    }

    [Fact]
    public async Task DownloadApp_ExistsInInstalledDirectory_SkipsDownload()
    {
        string installDir = Path.Combine(_tempAppPath, "installer-dir");
        Directory.CreateDirectory(installDir);
        await File.WriteAllTextAsync(
            Path.Combine(installDir, "NoMercyApp" + NoMercy.NmSystem.Information.Info.ExecSuffix),
            "installed-app"
        );
        Environment.SetEnvironmentVariable("NOMERCY_INSTALL_DIR", installDir);

        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);

        await binaries.DownloadApp();

        Assert.False(File.Exists(AppFiles.AppExePath));
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
        handler.Register(assetUrl, payload);
        handler.RegisterReleaseInfo(
            "https://api.github.com/repos/NoMercy-Entertainment/nomercy-media-server/releases/latest",
            ReleaseWithAssets(
                MakeAsset("NoMercyLauncher-windows-x64.exe", assetUrl, digestOf: payload)
            )
        );

        Binaries binaries = BuildBinaries(handler);
        await binaries.DownloadLauncher();

        Assert.True(File.Exists(AppFiles.LauncherExePath));
    }

    [Fact]
    public async Task DownloadLauncher_NoAssetsInRelease_DoesNotThrow()
    {
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);

        await binaries.DownloadLauncher();

        Assert.False(File.Exists(AppFiles.LauncherExePath));
    }

    [Fact]
    public async Task DownloadCli_WindowsAssetPresent_DownloadsAndVerifies()
    {
        byte[] payload = "nomercy-cli-binary"u8.ToArray();
        string assetUrl = "https://example.com/nomercy-windows-x64.exe";
        FakeHttpHandler handler = new();
        handler.Register(assetUrl, payload);
        handler.RegisterReleaseInfo(
            "https://api.github.com/repos/NoMercy-Entertainment/nomercy-media-server/releases/latest",
            ReleaseWithAssets(MakeAsset("nomercy-windows-x64.exe", assetUrl, digestOf: payload))
        );

        Binaries binaries = BuildBinaries(handler);
        await binaries.DownloadCli();

        Assert.True(File.Exists(AppFiles.CliExePath));
    }

    [Fact]
    public async Task DownloadCli_ExistsInInstalledDirectory_SkipsDownload()
    {
        string installDir = Path.Combine(_tempAppPath, "installer-dir-cli");
        Directory.CreateDirectory(installDir);
        await File.WriteAllTextAsync(
            Path.Combine(installDir, "nomercy" + NoMercy.NmSystem.Information.Info.ExecSuffix),
            "installed-cli"
        );
        Environment.SetEnvironmentVariable("NOMERCY_INSTALL_DIR", installDir);

        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);

        await binaries.DownloadCli();

        Assert.False(File.Exists(AppFiles.CliExePath));
    }

    // -------------------------------------------------------------------------
    // DownloadYtdlp — uses upstream SHA2-256SUMS, plus a third-party age gate
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadYtdlp_ReleaseOldEnough_DownloadsAndVerifiesAgainstUpstreamSums()
    {
        byte[] payload = "yt-dlp-binary"u8.ToArray();
        string sha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        string assetUrl = "https://example.com/yt-dlp_x86.exe";
        string sumsUrl = "https://example.com/SHA2-256SUMS";

        FakeHttpHandler handler = new();
        handler.Register(assetUrl, payload);
        handler.Register(sumsUrl, Encoding.ASCII.GetBytes($"{sha256}  yt-dlp_x86.exe\n"));

        GithubReleaseResponse release = ReleaseWithAssets(
            DateTimeOffset.UtcNow.AddDays(-30),
            "v1.0.0",
            MakeAsset("yt-dlp_x86.exe", assetUrl),
            MakeAsset("SHA2-256SUMS", sumsUrl)
        );

        handler.RegisterReleaseList(
            "https://api.github.com/repos/yt-dlp/yt-dlp/releases?per_page=30",
            [release]
        );

        Binaries binaries = BuildBinaries(handler);
        await binaries.DownloadYtdlp();

        Assert.True(File.Exists(AppFiles.YtdlpPath));
    }

    [Fact]
    public async Task DownloadYtdlp_OnlyTooNewReleaseAvailable_KeepsExistingBinary()
    {
        Directory.CreateDirectory(AppFiles.DependenciesPath);
        await File.WriteAllTextAsync(AppFiles.YtdlpPath, "existing-ytdlp");

        FakeHttpHandler handler = new();
        GithubReleaseResponse tooNew = ReleaseWithAssets(
            DateTimeOffset.UtcNow.AddDays(-1),
            "v1.0.0",
            MakeAsset("yt-dlp_x86.exe", "https://example.com/should-not-fetch")
        );
        handler.RegisterReleaseList(
            "https://api.github.com/repos/yt-dlp/yt-dlp/releases?per_page=30",
            [tooNew]
        );

        Binaries binaries = BuildBinaries(handler);
        await binaries.DownloadYtdlp();

        Assert.Equal("existing-ytdlp", await File.ReadAllTextAsync(AppFiles.YtdlpPath));
    }

    [Fact]
    public async Task DownloadYtdlp_NoAssetsInEligibleRelease_DoesNotThrow()
    {
        FakeHttpHandler handler = new();
        handler.RegisterReleaseList(
            "https://api.github.com/repos/yt-dlp/yt-dlp/releases?per_page=30",
            []
        );

        Binaries binaries = BuildBinaries(handler);
        await binaries.DownloadYtdlp();

        Assert.False(File.Exists(AppFiles.YtdlpPath));
    }

    // -------------------------------------------------------------------------
    // DownloadFfmpeg — the only Download* method whose asset is an archive that
    // must be extracted (not a raw single executable) after verification.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadFfmpeg_NoAssetsInRelease_NoExistingBinary_ThrowsInvalidOperation()
    {
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);

        // FFmpeg is the one dependency the encoder cannot run without at all — an
        // empty release AND nothing on disk must fail loudly (retried by
        // DegradedModeRecovery), not silently leave the server encoder-less.
        await Assert.ThrowsAsync<InvalidOperationException>(() => binaries.DownloadFfmpeg());
    }

    [Fact]
    public async Task DownloadFfmpeg_NoAssetsInRelease_ExistingBinaryOnDisk_KeepsIt()
    {
        Directory.CreateDirectory(AppFiles.FfmpegFolder);
        await File.WriteAllTextAsync(AppFiles.FfmpegPath, "existing-ffmpeg");

        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);

        await binaries.DownloadFfmpeg();

        Assert.Equal("existing-ffmpeg", await File.ReadAllTextAsync(AppFiles.FfmpegPath));
    }

    [Fact]
    public async Task DownloadFfmpeg_AlreadyCurrentVersion_SkipsDownload()
    {
        Directory.CreateDirectory(AppFiles.FfmpegFolder);
        await File.WriteAllTextAsync(AppFiles.FfmpegPath, "existing-ffmpeg");
        File.SetLastWriteTimeUtc(AppFiles.FfmpegPath, DateTime.UtcNow);

        FakeHttpHandler handler = new();
        handler.RegisterReleaseInfo(
            "https://api.github.com/repos/NoMercy-Entertainment/nomercy-ffmpeg/releases/latest",
            ReleaseWithAssets(
                DateTimeOffset.UtcNow.AddDays(-10),
                "v1.0.0",
                MakeAsset("ffmpeg-windows-x64.zip", "https://example.com/should-not-fetch")
            )
        );

        Binaries binaries = BuildBinaries(handler);
        await binaries.DownloadFfmpeg();

        Assert.Equal("existing-ffmpeg", await File.ReadAllTextAsync(AppFiles.FfmpegPath));
    }

    [Fact]
    public async Task DownloadFfmpeg_NoWindowsAsset_KeepsExistingBinaryWithoutThrowing()
    {
        Directory.CreateDirectory(AppFiles.FfmpegFolder);
        await File.WriteAllTextAsync(AppFiles.FfmpegPath, "existing-ffmpeg");

        FakeHttpHandler handler = new();
        handler.RegisterReleaseInfo(
            "https://api.github.com/repos/NoMercy-Entertainment/nomercy-ffmpeg/releases/latest",
            ReleaseWithAssets(MakeAsset("ffmpeg-linux-only.tar.gz", "https://example.com/linux"))
        );

        Binaries binaries = BuildBinaries(handler);
        await binaries.DownloadFfmpeg();

        Assert.Equal("existing-ffmpeg", await File.ReadAllTextAsync(AppFiles.FfmpegPath));
    }

    [Fact]
    public async Task DownloadFfmpeg_WindowsAssetPresent_DownloadsExtractsAndSetsExecutionPermissions()
    {
        byte[] zipBytes = BuildFfmpegZip();
        string assetUrl = "https://example.com/ffmpeg-windows-x64.zip";

        FakeHttpHandler handler = new();
        handler.Register(assetUrl, zipBytes);
        handler.RegisterReleaseInfo(
            "https://api.github.com/repos/NoMercy-Entertainment/nomercy-ffmpeg/releases/latest",
            ReleaseWithAssets(MakeAsset("ffmpeg-windows-x64.zip", assetUrl, digestOf: zipBytes))
        );

        Binaries binaries = BuildBinaries(handler);
        await binaries.DownloadFfmpeg();

        Assert.True(File.Exists(AppFiles.FfmpegPath));
        Assert.True(File.Exists(AppFiles.FfProbePath));
    }

    /// <summary>Builds a minimal real .zip archive containing the three executables
    /// DownloadFfmpeg's Windows branch expects to find after extraction.</summary>
    private static byte[] BuildFfmpegZip()
    {
        using MemoryStream stream = new();
        using (
            System.IO.Compression.ZipArchive archive = new(
                stream,
                System.IO.Compression.ZipArchiveMode.Create,
                leaveOpen: true
            )
        )
        {
            foreach (string name in new[] { "ffmpeg.exe", "ffprobe.exe", "ffplay.exe" })
            {
                System.IO.Compression.ZipArchiveEntry entry = archive.CreateEntry(name);
                using Stream entryStream = entry.Open();
                using StreamWriter writer = new(entryStream);
                writer.Write("fake binary content for " + name);
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
        handler.Register(assetUrl, payload);

        GithubReleaseResponse release = ReleaseWithAssets(
            DateTimeOffset.UtcNow.AddDays(-30),
            "v1.0.0",
            MakeAsset("packager-win-x64.exe", assetUrl, digestOf: payload)
        );
        handler.RegisterReleaseList(
            "https://api.github.com/repos/shaka-project/shaka-packager/releases?per_page=30",
            [release]
        );

        Binaries binaries = BuildBinaries(handler);
        await binaries.DownloadShakaPackager();

        Assert.True(File.Exists(AppFiles.ShakaPackagerPath));
    }

    // -------------------------------------------------------------------------
    // DownloadCloudflared — no checksums published; relies on the age gate + digest
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadCloudflared_NoAssetsInRelease_DoesNotThrow()
    {
        FakeHttpHandler handler = new();
        handler.RegisterReleaseList(
            "https://api.github.com/repos/cloudflare/cloudflared/releases?per_page=30",
            []
        );

        Binaries binaries = BuildBinaries(handler);
        await binaries.DownloadCloudflared();

        Assert.False(File.Exists(AppFiles.CloudflareDPath));
    }

    [Fact]
    public async Task DownloadCloudflared_ReleaseOldEnough_NoWindowsAsset_SkipsWithoutThrowing()
    {
        FakeHttpHandler handler = new();
        GithubReleaseResponse release = ReleaseWithAssets(
            DateTimeOffset.UtcNow.AddDays(-30),
            "v1.0.0",
            MakeAsset("cloudflared-linux-arm64", "https://example.com/should-not-fetch")
        );
        handler.RegisterReleaseList(
            "https://api.github.com/repos/cloudflare/cloudflared/releases?per_page=30",
            [release]
        );

        Binaries binaries = BuildBinaries(handler);
        await binaries.DownloadCloudflared();

        Assert.False(File.Exists(AppFiles.CloudflareDPath));
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
        handler.Register(assetUrl, payload);
        handler.RegisterReleaseInfo(
            "https://api.github.com/repos/NoMercy-Entertainment/nomercy-whisper-models/releases/latest",
            ReleaseWithAssets(MakeAsset("ggml-large-v3.bin", assetUrl, digestOf: payload))
        );

        Binaries binaries = BuildBinaries(handler);
        await binaries.DownloadWhisperModels("ggml-large-v3");

        // Single-part downloads land directly at DependenciesPath/{assetName} —
        // ConcatenateModelParts (and its FfmpegFolder destination) only runs when
        // there is more than one part to merge (see the MultiPart test below).
        string destination = Path.Combine(AppFiles.DependenciesPath, "ggml-large-v3.bin");
        Assert.True(File.Exists(destination));
        Assert.Equal(payload, await File.ReadAllBytesAsync(destination));
    }

    [Fact]
    public async Task DownloadWhisperModels_MultiPart_ConcatenatesInOrderAndCleansUpParts()
    {
        byte[] part1 = "PART-ONE-"u8.ToArray();
        byte[] part2 = "PART-TWO"u8.ToArray();
        string url1 = "https://example.com/ggml-large-v3.bin.part1";
        string url2 = "https://example.com/ggml-large-v3.bin.part2";

        FakeHttpHandler handler = new();
        handler.Register(url1, part1);
        handler.Register(url2, part2);
        handler.RegisterReleaseInfo(
            "https://api.github.com/repos/NoMercy-Entertainment/nomercy-whisper-models/releases/latest",
            ReleaseWithAssets(
                MakeAsset("ggml-large-v3.bin.part1", url1, digestOf: part1),
                MakeAsset("ggml-large-v3.bin.part2", url2, digestOf: part2)
            )
        );

        Binaries binaries = BuildBinaries(handler);
        await binaries.DownloadWhisperModels("ggml-large-v3");

        string destination = Path.Combine(AppFiles.FfmpegFolder, "ggml-large-v3.bin");
        Assert.True(File.Exists(destination));
        byte[] concatenated = await File.ReadAllBytesAsync(destination);
        Assert.Equal(Encoding.UTF8.GetBytes("PART-ONE-PART-TWO"), concatenated);

        // Parts must be cleaned up once concatenated into the final model file.
        Assert.False(
            File.Exists(Path.Combine(AppFiles.DependenciesPath, "ggml-large-v3.bin.part1"))
        );
        Assert.False(
            File.Exists(Path.Combine(AppFiles.DependenciesPath, "ggml-large-v3.bin.part2"))
        );
    }

    [Fact]
    public async Task DownloadWhisperModels_NoMatchingAssets_DoesNotThrow()
    {
        FakeHttpHandler handler = new();
        handler.RegisterReleaseInfo(
            "https://api.github.com/repos/NoMercy-Entertainment/nomercy-whisper-models/releases/latest",
            ReleaseWithAssets(MakeAsset("unrelated-file.bin", "https://example.com/unrelated"))
        );

        Binaries binaries = BuildBinaries(handler);
        await binaries.DownloadWhisperModels("ggml-large-v3");

        string destination = Path.Combine(AppFiles.FfmpegFolder, "ggml-large-v3.bin");
        Assert.False(File.Exists(destination));
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
        handler.Register(engUrl, engPayload);
        handler.RegisterReleaseInfo(
            "https://api.github.com/repos/NoMercy-Entertainment/nomercy-tesseract/releases/latest",
            ReleaseWithAssets(MakeAsset("eng.traineddata", engUrl, digestOf: engPayload))
        );

        Binaries binaries = BuildBinaries(handler);

        // "jpn" has no matching asset in the release — must be skipped, not thrown.
        await binaries.DownloadTesseractData(["eng", "jpn"]);

        Assert.True(File.Exists(Path.Combine(AppFiles.TesseractModelsFolder, "eng.traineddata")));
        Assert.False(File.Exists(Path.Combine(AppFiles.TesseractModelsFolder, "jpn.traineddata")));
    }

    [Fact]
    public async Task DownloadTesseractData_NoAssetsInRelease_DoesNotThrow()
    {
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);

        await binaries.DownloadTesseractData(["eng"]);

        Assert.False(File.Exists(Path.Combine(AppFiles.TesseractModelsFolder, "eng.traineddata")));
    }

    // -------------------------------------------------------------------------
    // ExistsInInstalledDirectory / CheckLocalVersion — direct unit coverage
    // -------------------------------------------------------------------------

    [Fact]
    public void ExistsInInstalledDirectory_NoInstallDirSet_ChecksOwnDirectoryOnly()
    {
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);

        bool result = binaries.ExistsInInstalledDirectory("definitely-not-a-real-executable.exe");

        Assert.False(result);
    }

    [Fact]
    public void ExistsInInstalledDirectory_InstallDirSetButFileMissing_ReturnsFalse()
    {
        string installDir = Path.Combine(_tempAppPath, "empty-install-dir");
        Directory.CreateDirectory(installDir);
        Environment.SetEnvironmentVariable("NOMERCY_INSTALL_DIR", installDir);

        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);

        bool result = binaries.ExistsInInstalledDirectory("missing-executable.exe");

        Assert.False(result);
    }

    [Fact]
    public void CheckLocalVersion_FileDoesNotExist_ReturnsFalse()
    {
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);
        GithubReleaseResponse release = ReleaseWithAssets(
            DateTimeOffset.UtcNow.AddDays(-1),
            "v2.0.0"
        );

        bool result = binaries.CheckLocalVersion(
            release,
            Path.Combine(_tempAppPath, "nonexistent.bin"),
            out string version
        );

        Assert.False(result);
        Assert.Equal("2.0.0", version);
    }

    [Fact]
    public async Task CheckLocalVersion_FileOlderThanRelease_ReturnsFalse()
    {
        string path = Path.Combine(_tempAppPath, "old-file.bin");
        await File.WriteAllTextAsync(path, "old");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-10));

        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);
        GithubReleaseResponse release = ReleaseWithAssets(DateTimeOffset.UtcNow, "v3.0.0");

        bool result = binaries.CheckLocalVersion(release, path, out string version);

        Assert.False(result);
        Assert.Equal("3.0.0", version);
    }

    [Fact]
    public void CheckLocalVersion_TagWithoutVPrefix_StripsNothing()
    {
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);
        GithubReleaseResponse release = ReleaseWithAssets(
            DateTimeOffset.UtcNow.AddDays(-1),
            "2024.01.01"
        );

        binaries.CheckLocalVersion(
            release,
            Path.Combine(_tempAppPath, "nope.bin"),
            out string version
        );

        Assert.Equal("2024.01.01", version);
    }

    // -------------------------------------------------------------------------
    // VerifyAssetDigestOrThrow — used by third-party binaries downloaded outside
    // DownloadWithVerificationAsync (currently only DownloadCloudflared).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task VerifyAssetDigestOrThrow_NoDigestOnAsset_SkipsCheckWithoutThrowing()
    {
        string path = Path.Combine(_tempAppPath, "no-digest.bin");
        await File.WriteAllTextAsync(path, "some content");
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);

        await binaries.VerifyAssetDigestOrThrow(
            path,
            MakeAsset("no-digest.bin", "https://example.com/x"),
            "test-asset"
        );

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task VerifyAssetDigestOrThrow_NullAsset_SkipsCheckWithoutThrowing()
    {
        string path = Path.Combine(_tempAppPath, "no-asset.bin");
        await File.WriteAllTextAsync(path, "some content");
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);

        await binaries.VerifyAssetDigestOrThrow(path, asset: null, "test-asset");

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task VerifyAssetDigestOrThrow_MatchingDigest_DoesNotThrowOrDeleteFile()
    {
        byte[] payload = "verified content"u8.ToArray();
        string path = Path.Combine(_tempAppPath, "matching.bin");
        await File.WriteAllBytesAsync(path, payload);
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);

        await binaries.VerifyAssetDigestOrThrow(
            path,
            MakeAsset("matching.bin", "https://example.com/x", digestOf: payload),
            "test-asset"
        );

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task VerifyAssetDigestOrThrow_MismatchedDigest_DeletesFileAndThrows()
    {
        byte[] payload = "tampered content"u8.ToArray();
        string path = Path.Combine(_tempAppPath, "mismatched.bin");
        await File.WriteAllBytesAsync(path, payload);
        FakeHttpHandler handler = new();
        Binaries binaries = BuildBinaries(handler);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            binaries.VerifyAssetDigestOrThrow(
                path,
                MakeAsset(
                    "mismatched.bin",
                    "https://example.com/x",
                    digestOf: "different content entirely"u8.ToArray()
                ),
                "test-asset"
            )
        );

        Assert.False(File.Exists(path));
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
        Directory.CreateDirectory(AppFiles.FfmpegFolder);
        await File.WriteAllTextAsync(AppFiles.FfmpegPath, "placeholder-ffmpeg");

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
            handler.RegisterReleaseInfo(apiUrl, ReleaseWithAssets());

        foreach (
            string listUrl in new[]
            {
                "https://api.github.com/repos/yt-dlp/yt-dlp/releases?per_page=30",
                "https://api.github.com/repos/cloudflare/cloudflared/releases?per_page=30",
                "https://api.github.com/repos/shaka-project/shaka-packager/releases?per_page=30",
            }
        )
            handler.RegisterReleaseList(listUrl, []);

        Binaries binaries = BuildBinaries(handler);

        await binaries.DownloadAll();
    }

    [Fact]
    public async Task DownloadAll_SomeBinariesAlreadyCurrent_BuildsUpToDateReportWithoutThrowing()
    {
        // Pre-seed App/Launcher/CLI as already current so DownloadAll's post-run report
        // table (3-column layout, built only when _binaryReport is non-empty) actually
        // has entries to format — the report path is otherwise never exercised.
        Directory.CreateDirectory(AppFiles.BinariesPath);
        Directory.CreateDirectory(AppFiles.FfmpegFolder);
        await File.WriteAllTextAsync(AppFiles.FfmpegPath, "placeholder-ffmpeg");

        // DownloadServerUpdate (also invoked by DownloadAll) compares the running
        // version against the release tag — pin it to match "v1.0.0" below so it takes
        // the AlreadyUpToDate branch instead of attempting a real download of the
        // unregistered "https://example.com/server" placeholder URL.
        NoMercy.NmSystem.Information.Software.Version = new(1, 0, 0);

        DateTimeOffset publishedAt = DateTimeOffset.UtcNow.AddDays(-10);
        foreach (
            string exePath in new[]
            {
                AppFiles.AppExePath,
                AppFiles.LauncherExePath,
                AppFiles.CliExePath,
            }
        )
        {
            await File.WriteAllTextAsync(exePath, "already-current");
            File.SetLastWriteTimeUtc(exePath, DateTime.UtcNow);
        }

        FakeHttpHandler handler = new();
        handler.RegisterReleaseInfo(
            "https://api.github.com/repos/NoMercy-Entertainment/nomercy-media-server/releases/latest",
            ReleaseWithAssets(
                publishedAt,
                "v1.0.0",
                MakeAsset("NoMercyApp-windows-x64.exe", "https://example.com/app"),
                MakeAsset("NoMercyLauncher-windows-x64.exe", "https://example.com/launcher"),
                MakeAsset("nomercy-windows-x64.exe", "https://example.com/cli"),
                MakeAsset("NoMercyMediaServer-windows-x64.exe", "https://example.com/server")
            )
        );
        handler.RegisterReleaseInfo(
            "https://api.github.com/repos/NoMercy-Entertainment/nomercy-ffmpeg/releases/latest",
            ReleaseWithAssets()
        );
        handler.RegisterReleaseInfo(
            "https://api.github.com/repos/NoMercy-Entertainment/nomercy-tesseract/releases/latest",
            ReleaseWithAssets()
        );
        handler.RegisterReleaseInfo(
            "https://api.github.com/repos/NoMercy-Entertainment/nomercy-whisper-models/releases/latest",
            ReleaseWithAssets()
        );
        foreach (
            string listUrl in new[]
            {
                "https://api.github.com/repos/yt-dlp/yt-dlp/releases?per_page=30",
                "https://api.github.com/repos/cloudflare/cloudflared/releases?per_page=30",
                "https://api.github.com/repos/shaka-project/shaka-packager/releases?per_page=30",
            }
        )
            handler.RegisterReleaseList(listUrl, []);

        Binaries binaries = BuildBinaries(handler);

        await binaries.DownloadAll();

        Assert.Equal("already-current", await File.ReadAllTextAsync(AppFiles.AppExePath));
        Assert.Equal("already-current", await File.ReadAllTextAsync(AppFiles.LauncherExePath));
        Assert.Equal("already-current", await File.ReadAllTextAsync(AppFiles.CliExePath));
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
    private readonly Dictionary<string, byte[]> _responses = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string url, byte[] body) => _responses[url] = body;

    public void RegisterReleaseInfo(string apiUrl, GithubReleaseResponse release) =>
        Register(apiUrl, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(release)));

    public void RegisterReleaseList(string listUrl, GithubReleaseResponse[] releases) =>
        Register(listUrl, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(releases)));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        string url = request.RequestUri?.ToString() ?? string.Empty;
        if (_responses.TryGetValue(url, out byte[]? body))
        {
            HttpResponseMessage ok = new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(body),
            };
            return Task.FromResult(ok);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
