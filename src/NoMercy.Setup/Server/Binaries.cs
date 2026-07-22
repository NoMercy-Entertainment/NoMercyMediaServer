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

using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.FileSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using NoMercy.Storage;
using Serilog.Events;
using Downloader = NoMercy.NmSystem.SystemCalls.Download;
using FileAttributes = NoMercy.NmSystem.FileSystem.FileAttributes;
using HttpClient = System.Net.Http.HttpClient;

namespace NoMercy.Setup.Server;

public enum ServerUpdateResult
{
    Downloaded,
    AlreadyUpToDate,
    UseInstaller,
    RestartNeeded,
    NoAssetFound,
}

public class Binaries
{
    private readonly IStorageDriver _driver;
    private readonly IStorage _storage;
    private readonly HttpClient _httpClient;

    private const string GithubMediaServerApiUrl =
        "https://api.github.com/repos/NoMercy-Entertainment/nomercy-media-server/releases/latest";
    private const string GithubFfmpegApiUrl =
        "https://api.github.com/repos/NoMercy-Entertainment/nomercy-ffmpeg/releases/latest";
    private const string GithubTesseractApiUrl =
        "https://api.github.com/repos/NoMercy-Entertainment/nomercy-tesseract/releases/latest";
    private const string GithubWhisperModelApiUrl =
        "https://api.github.com/repos/NoMercy-Entertainment/nomercy-whisper-models/releases/latest";

    private const string GithubYtdlpApiUrl =
        "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
    private const string GithubCloudflaredApiUrl =
        "https://api.github.com/repos/cloudflare/cloudflared/releases/latest";
    private const string GithubShakaPackagerApiUrl =
        "https://api.github.com/repos/shaka-project/shaka-packager/releases/latest";

    // Third-party releases (yt-dlp, cloudflared, shaka-packager) are held to a
    // minimum age before adoption: a compromised or yanked upstream release gets a
    // soak window to surface before a self-hosted server pulls it. NoMercy-owned
    // releases are exempt — they are signature-verified and must ship fast.
    private static readonly TimeSpan ThirdPartyMinReleaseAge = TimeSpan.FromDays(days: 14);

    public Binaries(IStorageDriver driver, IStorage storage)
    {
        _driver = driver;
        _storage = storage;
        _httpClient = new()
        {
            // Default HttpClient.Timeout is 100s which is fine for API calls
            // but binary downloads can be hundreds of MB. Cap at 10 minutes
            // so a stuck CDN connection eventually surfaces as a TaskCanceled
            // instead of pinning the setup phase forever.
            Timeout = TimeSpan.FromMinutes(minutes: 10),
        };
        _httpClient.DefaultRequestHeaders.Add(
            name: "User-Agent",
            value: ExternalServicesConfig.Current.UserAgent
        );
    }

    /// <summary>
    /// Testing constructor — allows injection of a custom <see cref="HttpClient"/> so that
    /// unit tests can supply a fake <see cref="HttpMessageHandler"/> without hitting the network.
    /// </summary>
    internal Binaries(IStorageDriver driver, IStorage storage, HttpClient httpClient)
    {
        _driver = driver;
        _storage = storage;
        _httpClient = httpClient;
    }

    // -------------------------------------------------------------------------
    // Per-instance manifest cache: keyed by API URL so that the same release
    // info is not re-fetched when multiple Download* methods run for the same
    // endpoint in a single session.
    // -------------------------------------------------------------------------
    private readonly Dictionary<
        string,
        (ReleaseManifest? Manifest, bool SignatureVerified, bool SignaturePresent)
    > _manifestCache = new();

    /// <summary>
    /// Fetches (and caches) the <c>manifest.json</c> for <paramref name="releaseInfo"/>.
    /// If <c>manifest.json.sig</c> is also present among the release assets, verifies the
    /// detached signature and records the result in <see cref="_manifestCache"/>.
    /// </summary>
    /// <remarks>
    /// <c>SignaturePresent</c> is tracked separately from <c>SignatureVerified</c> so a
    /// caller can tell "no <c>.sig</c> asset was published for this release" (the signing
    /// rollout has not reached this repo's CI yet — expected, low-noise) apart from "a
    /// <c>.sig</c> asset exists but does not verify against the embedded key" (a real
    /// integrity concern worth an error-level log).
    /// <para>
    /// Internal (not private) so <see cref="TesseractModelDownloader"/> can reuse the exact
    /// same manifest-fetch + signature-verification path — and its per-instance cache — for
    /// the on-demand OCR model pull instead of duplicating the fetch/verify logic.
    /// </para>
    /// </remarks>
    internal async Task<(
        ReleaseManifest? Manifest,
        bool SignatureVerified,
        bool SignaturePresent
    )> GetOrFetchManifestAsync(string apiUrl, GithubReleaseResponse releaseInfo)
    {
        if (
            _manifestCache.TryGetValue(
                key: apiUrl,
                value: out (
                    ReleaseManifest? Manifest,
                    bool SignatureVerified,
                    bool SignaturePresent
                ) cached
            )
        )
            return cached;

        Uri? manifestUrl = releaseInfo
            .Assets.FirstOrDefault(predicate: a =>
                a.Name.Equals(value: "manifest.json", comparisonType: StringComparison.OrdinalIgnoreCase)
            )
            ?.BrowserDownloadUrl;

        if (manifestUrl is null)
        {
            _manifestCache[key: apiUrl] = (null, false, false);
            return (null, false, false);
        }

        try
        {
            string manifestJson = await _httpClient.GetStringAsync(requestUri: manifestUrl);
            ReleaseManifest? manifest = JsonConvert.DeserializeObject<ReleaseManifest>(
                value: manifestJson
            );

            bool signatureVerified = false;
            Uri? sigUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: "manifest.json.sig", comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
            bool signaturePresent = sigUrl is not null;

            if (sigUrl is not null)
            {
                string armoredSig = await _httpClient.GetStringAsync(requestUri: sigUrl);
                signatureVerified = BinaryVerification.VerifyManifestSignature(
                    manifestJson: manifestJson,
                    armoredSignature: armoredSig
                );

                if (signatureVerified)
                    Logger.Setup(message: "Release manifest signature verified", level: LogEventLevel.Verbose);
                else
                    Logger.Setup(
                        message: "Release manifest signature could not be verified",
                        level: LogEventLevel.Warning
                    );
            }

            (ReleaseManifest? Manifest, bool SignatureVerified, bool SignaturePresent) result = (
                manifest,
                signatureVerified,
                signaturePresent
            );
            _manifestCache[key: apiUrl] = result;
            return result;
        }
        catch (Exception ex)
        {
            Logger.Setup(message: $"Failed to fetch release manifest: {ex.Message}", level: LogEventLevel.Warning);
            _manifestCache[key: apiUrl] = (null, false, false);
            return (null, false, false);
        }
    }

    /// <summary>
    /// Downloads <paramref name="downloadUrl"/> to a temporary file, verifies its SHA-256
    /// against the release manifest or per-asset sidecar, then atomically replaces
    /// <paramref name="destPath"/> (keeping a <c>.bak</c> backup until the smoke-check
    /// succeeds).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Verification is <em>opportunistic</em>: if no manifest and no <c>.sha256</c> sidecar
    /// are present in the release assets, the file is accepted as-is (preserving backward
    /// compatibility with third-party releases).
    /// </para>
    /// <para>
    /// If a manifest is present but its signature verification failed, a warning is emitted
    /// and the SHA-256 from the manifest is still used for integrity checking.
    /// </para>
    /// </remarks>
    internal async Task<string> DownloadWithVerificationAsync(
        string apiUrl,
        string label,
        Uri downloadUrl,
        string destPath,
        GithubReleaseResponse releaseInfo,
        string assetName,
        bool enforceSignedManifest,
        string? expectedSha256Override = null
    )
    {
        // Resolve the expected SHA-256 in trust order:
        //   1. explicit override (e.g. yt-dlp's own SHA2-256SUMS)
        //   2. signature-verified release manifest (NoMercy-owned repos)
        //   3. per-asset .sha256 sidecar
        //   4. GitHub's server-computed asset digest (universal integrity floor)
        string? expectedSha256 = expectedSha256Override;
        string sha256Source = expectedSha256 is not null ? "upstream checksum" : "none";

        (ReleaseManifest? manifest, bool sigVerified, bool sigPresent) =
            await GetOrFetchManifestAsync(apiUrl: apiUrl, releaseInfo: releaseInfo);

        // Owned assets prefer the signature-verified manifest for authenticity. But a
        // signature that does NOT verify — a stale key after a rotation, a CI signing
        // glitch, or a stripped/forged manifest — must never brick the update path: we
        // log loudly and fall through to GitHub's independent asset digest, which still
        // guarantees integrity. Only an actual hash MISMATCH (below) is fatal. A hard
        // throw here would also be illusory security: a repo-write attacker simply omits
        // the manifest and hits the same digest fallback, so failing closed only strands
        // honest servers on the next key rotation.
        bool manifestTrusted = sigVerified || !enforceSignedManifest;

        if (enforceSignedManifest && manifest is not null && sigPresent && !sigVerified)
        {
            // A .sig asset WAS published for this release but did not verify against the
            // embedded key — a genuine signing concern (rotated key, forged manifest, CI
            // glitch) worth surfacing loudly.
            Logger.Setup(
                message: $"Release manifest signature for {label} did not verify against the embedded NoMercy key — "
                         + "falling back to GitHub asset-digest integrity. Investigate the signing key if this persists.",
                level: LogEventLevel.Error
            );
        }
        else if (enforceSignedManifest && manifest is not null && !sigPresent)
        {
            // The manifest exists but no .sig asset was published at all — this repo's CI
            // signing rollout has not landed yet (ffmpeg/tesseract/whisper as of writing).
            // Expected during rollout, not a verification failure, so it must not be logged
            // as one: low-noise so a fresh install doesn't read this as a security incident.
            Logger.Setup(
                message: $"No signature published yet for {label}'s release manifest — accepting on GitHub asset-digest integrity (signing rollout in progress for this repo).",
                level: LogEventLevel.Debug
            );
        }
        else if (enforceSignedManifest && manifest is null)
        {
            // Visible until every owned repo publishes signed manifests. A missing
            // manifest on an owned release is either the signing rollout still in
            // progress (ffmpeg/tesseract/whisper) or a downgrade attempt — either way
            // it must not be silent.
            Logger.Setup(
                message: $"No signed manifest published for owned release {label} — accepting on GitHub digest integrity only.",
                level: LogEventLevel.Warning
            );
        }

        if (expectedSha256 is null && manifest is not null && manifestTrusted)
        {
            expectedSha256 = manifest
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.Sha256;
            if (expectedSha256 is not null)
                sha256Source = sigVerified ? "signed manifest" : "manifest";
        }

        if (expectedSha256 is null)
        {
            // Fall back to per-asset sidecar (assetName + ".sha256")
            Uri? sidecarUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName + ".sha256", comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;

            if (sidecarUrl is not null)
            {
                try
                {
                    expectedSha256 = (await _httpClient.GetStringAsync(requestUri: sidecarUrl)).Trim();
                    sha256Source = "sidecar";
                }
                catch (Exception ex)
                {
                    Logger.Setup(
                        message: $"Could not fetch SHA-256 sidecar for {assetName}: {ex.Message}",
                        level: LogEventLevel.Warning
                    );
                }
            }
        }

        if (expectedSha256 is null)
        {
            // GitHub returns "sha256:<hex>" for every release asset — a zero-cost
            // integrity check against what GitHub stored, even for third-party binaries.
            string? digest = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.Digest;
            expectedSha256 = BinaryVerification.ExtractSha256FromDigest(digest: digest);
            if (expectedSha256 is not null)
                sha256Source = "github digest";
        }

        // --- Download to a temp file -----------------------------------------------------------
        string tempPath = destPath + ".tmp";

        // Clean up any leftover temp from a previous aborted download.
        if (_driver.FileExists(path: tempPath))
            _driver.DeleteFile(path: tempPath);

        string? directory = Path.GetDirectoryName(path: destPath);
        if (directory is not null && !_driver.DirectoryExists(path: directory))
            _storage.CreateDirectory(path: directory);

        Logger.Setup(message: $"Downloading {label}", level: LogEventLevel.Verbose);

        using (
            HttpResponseMessage response = await _httpClient.GetAsync(
                requestUri: downloadUrl,
                completionOption: HttpCompletionOption.ResponseHeadersRead
            )
        )
        {
            response.EnsureSuccessStatusCode();

            await using Stream contentStream = await response.Content.ReadAsStreamAsync();
            await using Stream fileStream = _driver.OpenWrite(path: tempPath, overwrite: true);
            await contentStream.CopyToAsync(destination: fileStream);
            await fileStream.FlushAsync();
        }

        if (!_driver.FileExists(path: tempPath) || _storage.SizeOrZero(path: tempPath) == 0)
        {
            if (_driver.FileExists(path: tempPath))
                _driver.DeleteFile(path: tempPath);
            throw new IOException(
                message: $"Download of {label} produced an empty or missing file at {tempPath}"
            );
        }

        // --- SHA-256 verification --------------------------------------------------------------
        if (expectedSha256 is not null)
        {
            bool hashOk = await BinaryVerification.VerifyFileSha256Async(filePath: tempPath, expectedHex: expectedSha256);
            if (!hashOk)
            {
                _driver.DeleteFile(path: tempPath);
                throw new InvalidDataException(
                    message: $"SHA-256 mismatch for {label}: the downloaded file does not match the expected checksum"
                );
            }

            Logger.Setup(message: $"SHA-256 verified for {label} ({sha256Source})", level: LogEventLevel.Verbose);
        }
        else
        {
            // No manifest, sidecar, or digest resolved. For NoMercy-owned assets this
            // is unexpected (every current release carries at least a digest) and worth
            // surfacing; for third-party it is tolerated by policy.
            Logger.Setup(
                message: $"No SHA-256 available for {label} — skipping integrity check",
                level: enforceSignedManifest ? LogEventLevel.Warning : LogEventLevel.Debug
            );
        }

        // --- Atomic replace with backup --------------------------------------------------------
        string backupPath = destPath + ".bak";

        if (_driver.FileExists(path: backupPath))
            _driver.DeleteFile(path: backupPath);

        if (_driver.FileExists(path: destPath))
            _driver.MoveFile(source: destPath, destination: backupPath);

        _driver.MoveFile(source: tempPath, destination: destPath);

        // Smoke-check: destination must exist and be non-zero after the move.
        if (!_driver.FileExists(path: destPath) || _storage.SizeOrZero(path: destPath) == 0)
        {
            // Restore backup if the smoke-check fails.
            if (_driver.FileExists(path: backupPath))
            {
                if (_driver.FileExists(path: destPath))
                    _driver.DeleteFile(path: destPath);
                _driver.MoveFile(source: backupPath, destination: destPath);
            }

            throw new IOException(
                message: $"Post-download smoke-check failed for {label}: file missing or empty at {destPath}"
            );
        }

        // Backup no longer needed.
        if (_driver.FileExists(path: backupPath))
            _driver.DeleteFile(path: backupPath);

        Logger.Setup(
            message: $"Downloaded {label} to {destPath} ({_storage.SizeOrZero(path: destPath)} bytes)",
            level: LogEventLevel.Verbose
        );

        return destPath;
    }

    /// <summary>
    /// Returns true when the binary exists in an installer directory (not the binaries path).
    /// This prevents redundant downloads when binaries were installed by an installer.
    /// </summary>
    // Internal (not private): NoMercy.Tests.Setup exercises the platform/asset-resolution
    // branches of each Download* method directly (installer-directory short-circuit,
    // per-platform asset name selection, "no asset found" fallbacks) without needing to
    // drive the entire DownloadAll() sequence — the same testability rationale already
    // documented on DownloadFfmpeg below.
    internal bool ExistsInInstalledDirectory(string executableName)
    {
        // Check the Launcher's install directory (set for installer deployments only)
        string? installDir = Environment.GetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR");
        if (
            !string.IsNullOrEmpty(value: installDir)
            && _driver.FileExists(path: Path.Combine(path1: installDir, path2: executableName))
        )
            return true;

        // Also check the server's own directory, but only if it differs from the binaries path
        // (otherwise this is a standalone deployment and we DO want to download updates)
        string? ownDir = Path.GetDirectoryName(
            path: Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location
        );

        if (
            ownDir is not null
            && !string.Equals(
                a: Path.GetFullPath(path: ownDir),
                b: Path.GetFullPath(path: AppFiles.BinariesPath),
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
            && _driver.FileExists(path: Path.Combine(path1: ownDir, path2: executableName))
        )
            return true;

        return false;
    }

    private readonly List<string> _binaryReport = new();

    public Task DownloadAll()
    {
        return Task.Run(function: async () =>
        {
            Logger.Setup(message: "Downloading Binaries");

            await DownloadApp();
            await DownloadLauncher();
            await DownloadCli();
            await DownloadServerUpdate();
            await DownloadFfmpeg();
            await DownloadCloudflared();
            await DownloadYtdlp();
            await DownloadShakaPackager();
            await DownloadWhisperModels(modelName: AppFiles.WhisperModel);

            List<string> tesseractLanguages = ["eng", "jpn"];
            if (!CultureInfo.CurrentCulture.Equals(value: CultureInfo.InvariantCulture))
            {
                string currentCulture = CultureInfo.CurrentCulture.EnglishLanguageTag();
                if (
                    !string.IsNullOrEmpty(value: currentCulture)
                    && !tesseractLanguages.Contains(item: currentCulture)
                )
                    tesseractLanguages.Add(item: currentCulture);
            }
            await DownloadTesseractData(languages: tesseractLanguages);

            if (_binaryReport.Count > 0)
            {
                const int columns = 3;
                int rows = (_binaryReport.Count + columns - 1) / columns;
                int[] columnWidth = new int[columns];
                for (int i = 0; i < _binaryReport.Count; i++)
                {
                    int col = i % columns;
                    if (_binaryReport[index: i].Length > columnWidth[col])
                        columnWidth[col] = _binaryReport[index: i].Length;
                }

                StringBuilder report = new();
                report.Append(handler: $"Binaries up to date ({_binaryReport.Count}):");
                for (int row = 0; row < rows; row++)
                {
                    report.Append(value: '\n').Append(value: "  ");
                    for (int col = 0; col < columns; col++)
                    {
                        int index = (row * columns) + col;
                        if (index >= _binaryReport.Count)
                            break;

                        bool last = col == columns - 1 || index == _binaryReport.Count - 1;
                        report.Append(
                            value: last
                                ? _binaryReport[index: index]
                                : _binaryReport[index: index].PadRight(totalWidth: columnWidth[col] + 2)
                        );
                    }
                }

                Logger.Setup(message: report.ToString(), level: LogEventLevel.Verbose);
            }
        });
    }

    internal bool CheckLocalVersion(
        GithubReleaseResponse releaseInfo,
        string destination,
        out string version
    )
    {
        version = releaseInfo.TagName.StartsWith(value: "v")
            ? releaseInfo.TagName[1..]
            : releaseInfo.TagName;

        bool fileExists = _storage.Exists(path: destination);
        if (!fileExists)
            return false;

        DateTime creationTime = _storage.LastModified(path: destination).UtcDateTime;
        DateTimeOffset releaseDate =
            releaseInfo.PublishedAt != DateTimeOffset.MinValue
                ? releaseInfo.PublishedAt.UtcDateTime
                : DateTimeOffset.Now;

        return creationTime >= releaseDate;
    }

    // A rate-limited fetch must never block a boot/recovery tick for the full GitHub
    // hourly reset window (up to 60 minutes) — that starves every other startup
    // dependency and, for the degraded-mode recovery loop, means the process never
    // gets a chance to fall back to cached metadata at all. Once either cap is hit,
    // GetLatestReleaseInfo stops waiting and serves the last-known-good cache instead.
    private const int MaxRateLimitedAttempts = 3;
    private static readonly TimeSpan MaxRateLimitWait = TimeSpan.FromMinutes(minutes: 2);

    internal async Task<GithubReleaseResponse> GetLatestReleaseInfo(string apiUrl)
    {
        int attempt = 0;
        int rateLimitedAttempts = 0;
        TimeSpan backoff = TimeSpan.FromSeconds(seconds: 30);

        while (true)
        {
            attempt++;
            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(requestUri: apiUrl);

                if (
                    response.StatusCode
                    is HttpStatusCode.Forbidden
                        or HttpStatusCode.TooManyRequests
                )
                {
                    rateLimitedAttempts++;
                    TimeSpan waitTime = ResolveRateLimitWaitTime(response: response, backoff: backoff);

                    if (ExceedsRateLimitBudget(waitTime: waitTime, rateLimitedAttempts: rateLimitedAttempts))
                        return await ResolveRateLimitedFallbackAsync(apiUrl: apiUrl, waitTime: waitTime);

                    Logger.Setup(
                        message: $"GitHub API rate limited (attempt {attempt}), waiting {waitTime.TotalSeconds:F0}s to retry: {apiUrl}",
                        level: LogEventLevel.Warning
                    );

                    await Task.Delay(delay: waitTime);
                    backoff = TimeSpan.FromSeconds(value: Math.Min(val1: backoff.TotalSeconds * 2, val2: 600));
                    continue;
                }

                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();

                return await ParseAndCacheReleaseInfoAsync(apiUrl: apiUrl, jsonResponse: jsonResponse);
            }
            catch (Exception e)
            {
                Logger.Setup(
                    message: $"Error fetching release info from {apiUrl}: {e.Message}",
                    level: LogEventLevel.Warning
                );
                return await ResolveCachedOrEmptyAsync(apiUrl: apiUrl);
            }
        }
    }

    private static TimeSpan ResolveRateLimitWaitTime(HttpResponseMessage response, TimeSpan backoff)
    {
        if (!response.Headers.TryGetValues(name: "X-RateLimit-Reset", values: out IEnumerable<string>? values))
            return backoff;

        string? resetValue = values.FirstOrDefault();
        if (resetValue is null || !long.TryParse(s: resetValue, result: out long resetUnix))
            return backoff;

        DateTimeOffset resetTime = DateTimeOffset.FromUnixTimeSeconds(seconds: resetUnix);
        TimeSpan untilReset = resetTime - DateTimeOffset.UtcNow;
        return untilReset > TimeSpan.Zero ? untilReset + TimeSpan.FromSeconds(seconds: 2) : backoff;
    }

    private static bool ExceedsRateLimitBudget(TimeSpan waitTime, int rateLimitedAttempts) =>
        waitTime > MaxRateLimitWait || rateLimitedAttempts >= MaxRateLimitedAttempts;

    private async Task<GithubReleaseResponse> ResolveRateLimitedFallbackAsync(
        string apiUrl,
        TimeSpan waitTime
    )
    {
        GithubReleaseResponse? cached = await TryLoadCachedReleaseInfoAsync(apiUrl: apiUrl);
        if (cached is not null)
        {
            Logger.Setup(
                message: $"GitHub API rate limited for {apiUrl} — serving cached release metadata "
                         + $"instead of waiting {waitTime.TotalSeconds:F0}s for the reset.",
                level: LogEventLevel.Information
            );
            return cached;
        }

        Logger.Setup(
            message: $"GitHub API rate limited for {apiUrl} and no cached release metadata is available.",
            level: LogEventLevel.Warning
        );
        return new();
    }

    private async Task<GithubReleaseResponse> ResolveCachedOrEmptyAsync(string apiUrl)
    {
        GithubReleaseResponse? cached = await TryLoadCachedReleaseInfoAsync(apiUrl: apiUrl);
        if (cached is not null)
        {
            Logger.Setup(
                message: $"Serving cached release metadata for {apiUrl} because the live fetch failed.",
                level: LogEventLevel.Information
            );
            return cached;
        }

        return new();
    }

    private async Task<GithubReleaseResponse> ParseAndCacheReleaseInfoAsync(
        string apiUrl,
        string jsonResponse
    )
    {
        GithubReleaseResponse? parsed = jsonResponse.FromJson<GithubReleaseResponse>();
        if (parsed is null)
            return new();

        await WriteReleaseCacheAsync(apiUrl: apiUrl, json: jsonResponse);
        return parsed;
    }

    // -------------------------------------------------------------------------
    // Last-known-good release metadata cache: written on every successful fetch,
    // read back only when GitHub's API is unavailable (403/429/exception). This
    // is what lets ffmpeg provisioning survive a GitHub API rate-limit without
    // ever downloading an asset from stale/untrusted data — the asset download
    // itself always uses BrowserDownloadUrl from this (possibly cached) release
    // info, and that CDN endpoint is not subject to the API's rate limit.
    // -------------------------------------------------------------------------

    private static string ReleaseCacheDirectory =>
        Path.Combine(path1: AppFiles.CachePath, path2: "releases-cache");

    private static string ReleaseCacheFilePath(string apiUrl) =>
        Path.Combine(path1: ReleaseCacheDirectory, path2: ReleaseCacheFileName(apiUrl: apiUrl));

    private static string ReleaseCacheFileName(string apiUrl)
    {
        byte[] hash = SHA256.HashData(source: Encoding.UTF8.GetBytes(s: apiUrl));
        return Convert.ToHexString(inArray: hash).ToLowerInvariant() + ".json";
    }

    private async Task WriteReleaseCacheAsync(string apiUrl, string json)
    {
        try
        {
            string directory = ReleaseCacheDirectory;
            if (!_driver.DirectoryExists(path: directory))
                _storage.CreateDirectory(path: directory);

            await _storage.WriteAllTextAsync(
                path: ReleaseCacheFilePath(apiUrl: apiUrl),
                contents: json,
                ct: CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            Logger.Setup(
                message: $"Failed to cache release metadata for {apiUrl}: {ex.Message}",
                level: LogEventLevel.Debug
            );
        }
    }

    private async Task<GithubReleaseResponse?> TryLoadCachedReleaseInfoAsync(string apiUrl)
    {
        string path = ReleaseCacheFilePath(apiUrl: apiUrl);
        if (!_storage.Exists(path: path))
            return null;

        try
        {
            string json = await _storage.ReadAllTextAsync(path: path, ct: CancellationToken.None);
            GithubReleaseResponse? cached = json.FromJson<GithubReleaseResponse>();
            return cached is { Assets.Length: > 0 } ? cached : null;
        }
        catch (Exception ex)
        {
            Logger.Setup(
                message: $"Failed to read cached release metadata for {apiUrl}: {ex.Message}",
                level: LogEventLevel.Debug
            );
            return null;
        }
    }

    /// <summary>
    /// Returns the newest published (non-draft, non-prerelease) release that is at
    /// least <paramref name="minAge"/> old. Used for third-party dependencies whose
    /// authenticity we cannot vouch for: a minimum age gives a bad upstream release
    /// time to be caught and pulled before a self-hosted server adopts it.
    /// </summary>
    /// <returns>
    /// The qualifying release, or an empty <see cref="GithubReleaseResponse"/> when
    /// none is old enough yet (the caller then keeps whatever is already installed).
    /// </returns>
    internal async Task<GithubReleaseResponse> GetLatestReleaseInfoOlderThan(
        string latestApiUrl,
        TimeSpan minAge
    )
    {
        // Derive the list endpoint from the "…/releases/latest" URL.
        const string latestSuffix = "/releases/latest";
        string listUrl = latestApiUrl.EndsWith(value: latestSuffix, comparisonType: StringComparison.OrdinalIgnoreCase)
            ? string.Concat(
                str0: latestApiUrl.AsSpan(start: 0, length: latestApiUrl.Length - "/latest".Length),
                str1: "?per_page=30"
            )
            : latestApiUrl;

        DateTimeOffset cutoff = DateTimeOffset.UtcNow - minAge;

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(requestUri: listUrl);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync();

            GithubReleaseResponse[] releases = json.FromJson<GithubReleaseResponse[]>() ?? [];

            GithubReleaseResponse? qualifying = SelectNewestPublishedBefore(releases: releases, cutoff: cutoff);

            if (qualifying is null)
            {
                Logger.Setup(
                    message: $"No release older than {minAge.TotalDays:F0} days found at {listUrl} — keeping current binary.",
                    level: LogEventLevel.Debug
                );
                return new();
            }

            return qualifying;
        }
        catch (Exception e)
        {
            Logger.Setup(
                message: $"Error fetching release list from {listUrl}: {e.Message}",
                level: LogEventLevel.Warning
            );
            return new();
        }
    }

    /// <summary>
    /// Selects the newest published (non-draft, non-prerelease) release whose
    /// <c>published_at</c> is at or before <paramref name="cutoff"/>. Pure so the
    /// age-gate policy can be unit-tested without the network.
    /// </summary>
    internal static GithubReleaseResponse? SelectNewestPublishedBefore(
        IEnumerable<GithubReleaseResponse> releases,
        DateTimeOffset cutoff
    )
    {
        return releases
            .Where(predicate: r => r is { Draft: false, Prerelease: false })
            .Where(predicate: r => r.PublishedAt != DateTimeOffset.MinValue && r.PublishedAt <= cutoff)
            .MaxBy(keySelector: r => r.PublishedAt);
    }

    /// <summary>
    /// Fails closed when <paramref name="asset"/> carries a GitHub SHA-256 digest and
    /// the file at <paramref name="path"/> does not match it. A missing digest (older
    /// releases) is tolerated. Used for third-party binaries downloaded outside
    /// <see cref="DownloadWithVerificationAsync"/> (which handle their own extraction).
    /// </summary>
    internal async Task VerifyAssetDigestOrThrow(string path, Asset? asset, string label)
    {
        string? expected = BinaryVerification.ExtractSha256FromDigest(digest: asset?.Digest);
        if (expected is null)
        {
            Logger.Setup(
                message: $"No GitHub digest for {label} — skipping integrity check",
                level: LogEventLevel.Debug
            );
            return;
        }

        if (!await BinaryVerification.VerifyFileSha256Async(filePath: path, expectedHex: expected))
        {
            _storage.Delete(path: path);
            throw new InvalidDataException(
                message: $"SHA-256 mismatch for {label}: the downloaded file does not match GitHub's asset digest"
            );
        }

        Logger.Setup(message: $"SHA-256 verified for {label} (github digest)", level: LogEventLevel.Verbose);
    }

    internal async Task DownloadApp()
    {
        if (ExistsInInstalledDirectory(executableName: "NoMercyApp" + Info.ExecSuffix))
        {
            Logger.Setup(
                message: "App found in installed directory, skipping download",
                level: LogEventLevel.Verbose
            );
            return;
        }

        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(apiUrl: GithubMediaServerApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup(message: "No assets found for App release.", level: LogEventLevel.Warning);
            return;
        }

        if (CheckLocalVersion(releaseInfo: releaseInfo, destination: AppFiles.AppExePath, version: out string version))
        {
            _binaryReport.Add(item: $"App = {version}");
            return;
        }

        await Downloader.DeleteSourceDownload(storage: _storage, filePath: AppFiles.AppExePath);

        Uri? downloadUrl = null;
        string? assetName = null;

        if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
        {
            assetName = "NoMercyApp-windows-x64.exe";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            assetName = "NoMercyApp-linux-arm64";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            assetName = "NoMercyApp-linux-x64";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX))
        {
            assetName = "NoMercyApp-macos-x64.dmg";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }

        if (downloadUrl == null || assetName is null)
        {
            Logger.Setup(
                message: "No suitable NoMercyApp asset found for the current platform.",
                level: LogEventLevel.Warning
            );
            return;
        }

        string path = await DownloadWithVerificationAsync(
            apiUrl: GithubMediaServerApiUrl,
            label: "NoMercyApp",
            downloadUrl: downloadUrl,
            destPath: AppFiles.AppExePath,
            releaseInfo: releaseInfo,
            assetName: assetName,
            enforceSignedManifest: true
        );

        await FileAttributes.SetCreatedAttribute(filePath: path, createdAt: releaseInfo.PublishedAt);

        await FilePermissions.SetExecutionPermissions(path: path);
    }

    internal async Task DownloadLauncher()
    {
        if (ExistsInInstalledDirectory(executableName: "NoMercyLauncher" + Info.ExecSuffix))
        {
            Logger.Setup(
                message: "Launcher found in installed directory, skipping download",
                level: LogEventLevel.Verbose
            );
            return;
        }

        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(apiUrl: GithubMediaServerApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup(message: "No assets found for Launcher release.", level: LogEventLevel.Warning);
            return;
        }

        if (CheckLocalVersion(releaseInfo: releaseInfo, destination: AppFiles.LauncherExePath, version: out string version))
        {
            _binaryReport.Add(item: $"Launcher = {version}");
            return;
        }

        await Downloader.DeleteSourceDownload(storage: _storage, filePath: AppFiles.LauncherExePath);

        Uri? downloadUrl = null;
        string? assetName = null;

        if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
        {
            assetName = "NoMercyLauncher-windows-x64.exe";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            assetName = "NoMercyLauncher-linux-arm64";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            assetName = "NoMercyLauncher-linux-x64";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX))
        {
            assetName = "NoMercyLauncher-macos-x64";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }

        if (downloadUrl == null || assetName is null)
        {
            Logger.Setup(
                message: "No suitable NoMercyLauncher asset found for the current platform.",
                level: LogEventLevel.Warning
            );
            return;
        }

        string path = await DownloadWithVerificationAsync(
            apiUrl: GithubMediaServerApiUrl,
            label: "NoMercyLauncher",
            downloadUrl: downloadUrl,
            destPath: AppFiles.LauncherExePath,
            releaseInfo: releaseInfo,
            assetName: assetName,
            enforceSignedManifest: true
        );

        await FileAttributes.SetCreatedAttribute(filePath: path, createdAt: releaseInfo.PublishedAt);

        await FilePermissions.SetExecutionPermissions(path: path);
    }

    internal async Task DownloadCli()
    {
        if (ExistsInInstalledDirectory(executableName: "nomercy" + Info.ExecSuffix))
        {
            Logger.Setup(
                message: "CLI found in installed directory, skipping download",
                level: LogEventLevel.Verbose
            );
            return;
        }

        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(apiUrl: GithubMediaServerApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup(message: "No assets found for CLI release.", level: LogEventLevel.Warning);
            return;
        }

        if (CheckLocalVersion(releaseInfo: releaseInfo, destination: AppFiles.CliExePath, version: out string version))
        {
            _binaryReport.Add(item: $"CLI = {version}");
            return;
        }

        await Downloader.DeleteSourceDownload(storage: _storage, filePath: AppFiles.CliExePath);

        Uri? downloadUrl = null;
        string? assetName = null;

        if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
        {
            assetName = "nomercy-windows-x64.exe";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            assetName = "nomercy-linux-arm64";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            assetName = "nomercy-linux-x64";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX))
        {
            assetName = "nomercy-macos-x64";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }

        if (downloadUrl == null || assetName is null)
        {
            Logger.Setup(
                message: "No suitable nomercy CLI asset found for the current platform.",
                level: LogEventLevel.Warning
            );
            return;
        }

        string path = await DownloadWithVerificationAsync(
            apiUrl: GithubMediaServerApiUrl,
            label: "nomercy",
            downloadUrl: downloadUrl,
            destPath: AppFiles.CliExePath,
            releaseInfo: releaseInfo,
            assetName: assetName,
            enforceSignedManifest: true
        );

        await FileAttributes.SetCreatedAttribute(filePath: path, createdAt: releaseInfo.PublishedAt);

        await FilePermissions.SetExecutionPermissions(path: path);
    }

    public async Task<ServerUpdateResult> DownloadServerUpdate()
    {
        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(apiUrl: GithubMediaServerApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup(message: "No assets found for Server release.", level: LogEventLevel.Warning);
            return ServerUpdateResult.NoAssetFound;
        }

        string latestVersion = releaseInfo.TagName.StartsWith(value: "v")
            ? releaseInfo.TagName[1..]
            : releaseInfo.TagName;

        string currentVersion = Software.GetReleaseVersion();

        if (string.Equals(a: latestVersion, b: currentVersion, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            _binaryReport.Add(item: $"Server = {currentVersion}");
            return ServerUpdateResult.AlreadyUpToDate;
        }

        if (
            Version.TryParse(input: latestVersion, result: out Version? latest)
            && Version.TryParse(input: currentVersion, result: out Version? current)
            && latest <= current
        )
        {
            _binaryReport.Add(item: $"Server = {currentVersion}");
            return ServerUpdateResult.AlreadyUpToDate;
        }

        // Installer deployment: the installer handles updates, don't download to binaries path
        string? installDir = Environment.GetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR");
        if (!string.IsNullOrEmpty(value: installDir))
        {
            Logger.Setup(
                message: $"Server update available: {currentVersion} -> {latestVersion} (use installer to update)"
            );
            return ServerUpdateResult.UseInstaller;
        }

        string? onDiskVersion = Software.GetFileVersion(driver: _driver, exePath: AppFiles.ServerExePath);
        if (
            onDiskVersion is not null
            && string.Equals(a: latestVersion, b: onDiskVersion, comparisonType: StringComparison.OrdinalIgnoreCase)
        )
        {
            Logger.Setup(
                message: $"Server binary on disk is already {onDiskVersion} (running {currentVersion}), restart needed to apply"
            );
            return ServerUpdateResult.RestartNeeded;
        }

        Logger.Setup(message: $"Server update available: {currentVersion} -> {latestVersion}");

        await Downloader.DeleteSourceDownload(storage: _storage, filePath: AppFiles.ServerTempExePath);

        Uri? downloadUrl = null;
        string? assetName = null;

        if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
        {
            assetName = "NoMercyMediaServer-windows-x64.exe";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            assetName = "NoMercyMediaServer-linux-arm64";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            assetName = "NoMercyMediaServer-linux-x64";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX))
        {
            assetName = "NoMercyMediaServer-macos-x64";
            downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;
        }

        if (downloadUrl == null || assetName is null)
        {
            Logger.Setup(
                message: "No suitable NoMercyMediaServer asset found for the current platform.",
                level: LogEventLevel.Warning
            );
            return ServerUpdateResult.NoAssetFound;
        }

        string path = await DownloadWithVerificationAsync(
            apiUrl: GithubMediaServerApiUrl,
            label: "NoMercyMediaServer Update",
            downloadUrl: downloadUrl,
            destPath: AppFiles.ServerTempExePath,
            releaseInfo: releaseInfo,
            assetName: assetName,
            enforceSignedManifest: true
        );

        // Wait for the file to become available (antivirus scanning can briefly lock/quarantine it)
        bool fileReady = false;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (_storage.Exists(path: path) && _storage.SizeOrZero(path: path) > 0)
            {
                fileReady = true;
                break;
            }

            Logger.Setup(
                message: $"Waiting for staged update file to become available (attempt {attempt + 1}/5)...",
                level: LogEventLevel.Debug
            );
            await Task.Delay(millisecondsDelay: 1000);
        }

        if (!fileReady)
        {
            Logger.Setup(
                message: $"Staged update file not available at {path} after download",
                level: LogEventLevel.Error
            );
            return ServerUpdateResult.NoAssetFound;
        }

        await FileAttributes.SetCreatedAttribute(filePath: path, createdAt: releaseInfo.PublishedAt);

        await FilePermissions.SetExecutionPermissions(path: path);

        Logger.Setup(message: $"Server update staged at {path} ({_storage.SizeOrZero(path: path)} bytes)");
        return ServerUpdateResult.Downloaded;
    }

    /// <summary>
    /// Internal (not private) so the degraded-mode recovery loop
    /// (<see cref="Boot.DegradedModeRecovery.TryProvisionBinariesAsync"/>) can retry
    /// ffmpeg provisioning alone instead of re-running <see cref="DownloadAll"/> — which
    /// would re-query all ten dependency repos' <c>releases/latest</c> endpoints on every
    /// backoff tick and, on a single rate-limited hiccup, keep re-triggering the same
    /// GitHub 403 for every other binary too.
    /// </summary>
    internal async Task DownloadFfmpeg()
    {
        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(apiUrl: GithubFfmpegApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            if (!_storage.Exists(path: AppFiles.FfmpegPath))
                throw new InvalidOperationException(
                    message: "FFmpeg is not installed and release info could not be fetched. Will retry."
                );

            Logger.Setup(
                message: "No assets found for FFMpeg release, keeping existing binaries.",
                level: LogEventLevel.Warning
            );
            return;
        }

        if (CheckLocalVersion(releaseInfo: releaseInfo, destination: AppFiles.FfmpegPath, version: out string version))
        {
            _binaryReport.Add(item: $"Ffmpeg = {version}");
            return;
        }

        // Skip the update when ffmpeg is locked by a running encode — the zip
        // extraction would fail mid-way, leave AppFiles.FfmpegFolder in a partial
        // state, and trip the Phase 4 "required startup task failed" alert. The
        // update will land on the next boot when no encode is in flight.
        if (_storage.Exists(path: AppFiles.FfmpegPath) && Locking.IsFileLocked(filePath: AppFiles.FfmpegPath))
        {
            Logger.Setup(
                message: "FFmpeg binary is in use by a running encode — deferring update to next boot.",
                level: LogEventLevel.Information
            );
            return;
        }

        await Downloader.DeleteSourceDownload(storage: _storage, filePath: AppFiles.FfmpegPath);
        await Downloader.DeleteSourceDownload(storage: _storage, filePath: AppFiles.FfProbePath);
        await Downloader.DeleteSourceDownload(storage: _storage, filePath: AppFiles.FfPlayPath);

        Asset? selectedAsset = null;

        if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
        {
            selectedAsset = releaseInfo.Assets.FirstOrDefault(predicate: a => a.Name.Contains(value: "windows"));
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            selectedAsset = releaseInfo.Assets.FirstOrDefault(predicate: a =>
                a.Name.Contains(value: "linux") && a.Name.Contains(value: "aarch64")
            );
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            selectedAsset = releaseInfo.Assets.FirstOrDefault(predicate: a =>
                a.Name.Contains(value: "linux") && a.Name.Contains(value: "x86_64")
            );
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            selectedAsset = releaseInfo.Assets.FirstOrDefault(predicate: a =>
                a.Name.Contains(value: "darwin") && a.Name.Contains(value: "arm64")
            );
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            selectedAsset = releaseInfo.Assets.FirstOrDefault(predicate: a =>
                a.Name.Contains(value: "darwin") && a.Name.Contains(value: "x86_64")
            );
        }

        if (selectedAsset is null)
        {
            Logger.Setup(
                message: "No suitable FFMpeg asset found for the current platform.",
                level: LogEventLevel.Warning
            );
            return;
        }

        string archiveDestPath = Path.Combine(path1: AppFiles.DependenciesPath, path2: selectedAsset.Name);

        string path = await DownloadWithVerificationAsync(
            apiUrl: GithubFfmpegApiUrl,
            label: "FFMpeg",
            downloadUrl: selectedAsset.BrowserDownloadUrl,
            destPath: archiveDestPath,
            releaseInfo: releaseInfo,
            assetName: selectedAsset.Name,
            enforceSignedManifest: true
        );

        // Re-check the lock right before extraction — the encoder worker may have
        // reserved a job and started running ffmpeg between the pre-download check
        // and the multi-minute zip download finishing. Without this gate, extraction
        // races the encode: ExtractToFile deletes ffmpeg.exe, in-flight Process.Start
        // hits ERROR_FILE_NOT_FOUND, encode fails. Keep the downloaded zip so the
        // update lands on the next boot when no encode is in flight.
        if (_storage.Exists(path: AppFiles.FfmpegPath) && Locking.IsFileLocked(filePath: AppFiles.FfmpegPath))
        {
            Logger.Setup(
                message: "FFmpeg binary became locked by a running encode while the update downloaded — "
                         + "deferring extraction to next boot.",
                level: LogEventLevel.Information
            );
            return;
        }

        List<string> files = await Archiving.ExtractArchive(storage: _storage, filePath: path, destination: AppFiles.FfmpegFolder);
        foreach (string file in files)
        {
            await FileAttributes.SetCreatedAttribute(filePath: file, createdAt: releaseInfo.PublishedAt);
            await FilePermissions.SetExecutionPermissions(path: file);
        }

        await Downloader.DeleteSourceDownload(storage: _storage, filePath: path);
    }

    /// <summary>
    /// Fetches an upstream checksum manifest (e.g. yt-dlp's <c>SHA2-256SUMS</c>) from
    /// the release assets and returns the hex SHA-256 for <paramref name="targetAssetName"/>.
    /// The file format is one <c>"&lt;hash&gt;  &lt;filename&gt;"</c> entry per line.
    /// </summary>
    /// <returns>The hex digest, or <c>null</c> when the sums file or entry is absent.</returns>
    internal async Task<string?> ResolveUpstreamSha256Async(
        GithubReleaseResponse releaseInfo,
        string sumsAssetName,
        string targetAssetName
    )
    {
        Uri? sumsUrl = releaseInfo
            .Assets.FirstOrDefault(predicate: a =>
                a.Name.Equals(value: sumsAssetName, comparisonType: StringComparison.OrdinalIgnoreCase)
            )
            ?.BrowserDownloadUrl;

        if (sumsUrl is null)
            return null;

        try
        {
            string sums = await _httpClient.GetStringAsync(requestUri: sumsUrl);
            return BinaryVerification.ParseSha256Sums(sumsContent: sums, targetFileName: targetAssetName);
        }
        catch (Exception ex)
        {
            Logger.Setup(
                message: $"Could not fetch upstream {sumsAssetName} for {targetAssetName}: {ex.Message}",
                level: LogEventLevel.Warning
            );
        }

        return null;
    }

    internal async Task DownloadYtdlp()
    {
        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfoOlderThan(
            latestApiUrl: GithubYtdlpApiUrl,
            minAge: ThirdPartyMinReleaseAge
        );
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup(message: "No assets found for yt-dlp release.", level: LogEventLevel.Warning);
            return;
        }

        if (CheckLocalVersion(releaseInfo: releaseInfo, destination: AppFiles.YtdlpPath, version: out string version))
        {
            _binaryReport.Add(item: $"Yt-dlp = {version}");
            return;
        }

        await Downloader.DeleteSourceDownload(storage: _storage, filePath: AppFiles.YtdlpPath);

        string? assetName = null;
        if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            assetName = "yt-dlp_x86.exe";
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
            assetName = "yt-dlp_linux_aarch64";
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
            assetName = "yt-dlp_linux";
        else if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX))
            assetName = "yt-dlp_macos";

        Uri? downloadUrl = assetName is null
            ? null
            : releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;

        if (downloadUrl is null || assetName is null)
        {
            Logger.Setup(
                message: "No suitable yt-dlp asset found for the current platform.",
                level: LogEventLevel.Warning
            );
            return;
        }

        // yt-dlp publishes its own SHA2-256SUMS — enforce the download against the
        // hash the yt-dlp maintainers listed for this exact asset. When that file is
        // unavailable, DownloadWithVerificationAsync still falls back to GitHub's
        // asset digest, so the binary is never accepted unverified.
        string? expectedSha256 = await ResolveUpstreamSha256Async(
            releaseInfo: releaseInfo,
            sumsAssetName: "SHA2-256SUMS",
            targetAssetName: assetName
        );

        string outputPath = await DownloadWithVerificationAsync(
            apiUrl: GithubYtdlpApiUrl,
            label: "yt-dlp",
            downloadUrl: downloadUrl,
            destPath: AppFiles.YtdlpPath,
            releaseInfo: releaseInfo,
            assetName: assetName,
            enforceSignedManifest: false,
            expectedSha256Override: expectedSha256
        );

        await FileAttributes.SetCreatedAttribute(filePath: outputPath, createdAt: releaseInfo.PublishedAt);

        await FilePermissions.SetExecutionPermissions(path: outputPath);

        Logger.Setup(message: $"Downloaded yt-dlp to {outputPath}");
    }

    internal async Task DownloadShakaPackager()
    {
        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfoOlderThan(
            latestApiUrl: GithubShakaPackagerApiUrl,
            minAge: ThirdPartyMinReleaseAge
        );
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup(message: "No assets found for shaka-packager release.", level: LogEventLevel.Warning);
            return;
        }

        if (CheckLocalVersion(releaseInfo: releaseInfo, destination: AppFiles.ShakaPackagerPath, version: out string version))
        {
            _binaryReport.Add(item: $"shaka-packager = {version}");
            return;
        }

        await Downloader.DeleteSourceDownload(storage: _storage, filePath: AppFiles.ShakaPackagerPath);

        // shaka-project release asset names per platform.
        string? assetName = null;
        if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            assetName = "packager-win-x64.exe";
        else if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            assetName =
                RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? "packager-linux-arm64"
                    : "packager-linux-x64";
        else if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX))
            assetName =
                RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? "packager-osx-arm64"
                    : "packager-osx-x64";

        Uri? downloadUrl = assetName is null
            ? null
            : releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: assetName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;

        if (downloadUrl is null || assetName is null)
        {
            Logger.Setup(
                message: "No suitable shaka-packager asset found for the current platform.",
                level: LogEventLevel.Warning
            );
            return;
        }

        string outputPath = await DownloadWithVerificationAsync(
            apiUrl: GithubShakaPackagerApiUrl,
            label: "shaka-packager",
            downloadUrl: downloadUrl,
            destPath: AppFiles.ShakaPackagerPath,
            releaseInfo: releaseInfo,
            assetName: assetName,
            enforceSignedManifest: false
        );

        await FileAttributes.SetCreatedAttribute(filePath: outputPath, createdAt: releaseInfo.PublishedAt);
        await FilePermissions.SetExecutionPermissions(path: outputPath);

        Logger.Setup(message: $"Downloaded shaka-packager to {outputPath}");
    }

    internal async Task DownloadCloudflared()
    {
        string destinationPath = AppFiles.CloudflareDPath;

        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfoOlderThan(
            latestApiUrl: GithubCloudflaredApiUrl,
            minAge: ThirdPartyMinReleaseAge
        );
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup(message: "No assets found for cloudflared release.", level: LogEventLevel.Warning);
            return;
        }

        if (CheckLocalVersion(releaseInfo: releaseInfo, destination: destinationPath, version: out string version))
        {
            _binaryReport.Add(item: $"Cloudflared = {version}");
            return;
        }

        await Downloader.DeleteSourceDownload(storage: _storage, filePath: destinationPath);

        Asset? selectedAsset = null;
        bool needsExtraction = false;

        if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
        {
            selectedAsset = releaseInfo.Assets.FirstOrDefault(predicate: a =>
                a.Name.Equals(value: "cloudflared-windows-amd64.exe")
            );
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            selectedAsset = releaseInfo.Assets.FirstOrDefault(predicate: a =>
                a.Name.Equals(value: "cloudflared-linux-arm")
            );
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            selectedAsset = releaseInfo.Assets.FirstOrDefault(predicate: a =>
                a.Name.Equals(value: "cloudflared-linux-amd64")
            );
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        )
        {
            selectedAsset = releaseInfo.Assets.FirstOrDefault(predicate: a =>
                a.Name.Equals(value: "cloudflared-darwin-arm64.tgz")
            );
            needsExtraction = true;
        }
        else if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX)
            && RuntimeInformation.ProcessArchitecture == Architecture.X64
        )
        {
            selectedAsset = releaseInfo.Assets.FirstOrDefault(predicate: a =>
                a.Name.Equals(value: "cloudflared-darwin-amd64.tgz")
            );
            needsExtraction = true;
        }

        if (selectedAsset is null)
        {
            Logger.Setup(
                message: "No suitable cloudflared asset found for the current platform.",
                level: LogEventLevel.Warning
            );
            return;
        }

        string path = await Downloader.DownloadFile(
            storage: _storage,
            name: "cloudflared",
            url: selectedAsset.BrowserDownloadUrl
        );

        // cloudflared publishes no checksums, so authenticity rests on the 14-day
        // release-age gate. We can still reject a corrupt or tampered download by
        // matching GitHub's own asset digest before we install or extract it.
        await VerifyAssetDigestOrThrow(path: path, asset: selectedAsset, label: "cloudflared");

        Logger.Setup(message: $"Downloaded cloudflared to {path}");

        if (needsExtraction)
        {
            List<string> files = await Archiving.ExtractArchive(
                storage: _storage,
                filePath: path,
                destination: AppFiles.DependenciesPath
            );
            foreach (string file in files)
            {
                await FileAttributes.SetCreatedAttribute(filePath: file, createdAt: releaseInfo.PublishedAt);
                await FilePermissions.SetExecutionPermissions(path: file);
            }
            await Downloader.DeleteSourceDownload(storage: _storage, filePath: path);
        }
        else
        {
            if (_storage.Exists(path: destinationPath))
                _storage.Delete(path: destinationPath);

            _storage.Move(from: path, to: destinationPath);

            await FileAttributes.SetCreatedAttribute(filePath: destinationPath, createdAt: releaseInfo.PublishedAt);

            await FilePermissions.SetExecutionPermissions(path: destinationPath);
        }
    }

    internal async Task DownloadWhisperModels(string modelName = "ggml-large-v3")
    {
        string destinationPath = Path.Combine(path1: AppFiles.FfmpegFolder, path2: modelName + ".bin");

        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(apiUrl: GithubWhisperModelApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup(
                message: "No assets found for nomercy-whisper-models release.",
                level: LogEventLevel.Warning
            );
            return;
        }

        if (CheckLocalVersion(releaseInfo: releaseInfo, destination: destinationPath, version: out string version))
        {
            _binaryReport.Add(item: $"Whisper = {version}");
            return;
        }

        await Downloader.DeleteSourceDownload(storage: _storage, filePath: destinationPath);

        List<Asset> modelAssets = releaseInfo
            .Assets.Where(predicate: a => a.Name.Contains(value: modelName, comparisonType: StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (modelAssets.Count == 0)
        {
            Logger.Setup(
                message: $"No assets found for model {modelName} in nomercy-whisper-models release.",
                level: LogEventLevel.Warning
            );
            return;
        }

        List<string> paths = [];
        foreach (Asset asset in modelAssets)
        {
            string partDestPath = Path.Combine(path1: AppFiles.DependenciesPath, path2: asset.Name);
            paths.Add(
                item: await DownloadWithVerificationAsync(
                    apiUrl: GithubWhisperModelApiUrl,
                    label: "nomercy-whisper-models",
                    downloadUrl: asset.BrowserDownloadUrl,
                    destPath: partDestPath,
                    releaseInfo: releaseInfo,
                    assetName: asset.Name,
                    enforceSignedManifest: true
                )
            );
        }

        if (modelAssets.Count > 1)
        {
            string outputPath = await ConcatenateModelParts(modelName: modelName, partPaths: paths);

            foreach (string path in paths)
            {
                await Downloader.DeleteSourceDownload(storage: _storage, filePath: path);
            }

            await FileAttributes.SetCreatedAttribute(filePath: outputPath, createdAt: releaseInfo.PublishedAt);

            Logger.Setup(message: $"Downloaded and concatenated Whisper model parts to {outputPath}");
        }
        else
        {
            Logger.Setup(message: $"Downloaded Whisper model to {paths[index: 0]}");
        }
    }

    internal async Task<string> ConcatenateModelParts(
        string modelName,
        IEnumerable<string> partPaths
    )
    {
        string destinationPath = Path.Combine(path1: AppFiles.FfmpegFolder, path2: modelName + ".bin");

        // Mirrors DownloadWithVerificationAsync's own directory-creation guard: normally
        // FfmpegFolder already exists (DownloadFfmpeg's extraction step creates it, and
        // runs before this in DownloadAll's sequence), but nothing enforces that
        // ordering for a caller that only ever invokes DownloadWhisperModels directly.
        if (!_driver.DirectoryExists(path: AppFiles.FfmpegFolder))
            _storage.CreateDirectory(path: AppFiles.FfmpegFolder);

        await using Stream destinationStream = _driver.OpenWrite(path: destinationPath, overwrite: true);

        // Concatenate the exact local files DownloadWithVerificationAsync already
        // hash-verified — never re-derive paths from URLs.
        foreach (string partPath in partPaths)
        {
            await using Stream partStream = _driver.OpenRead(path: partPath);
            await partStream.CopyToAsync(destination: destinationStream);
        }

        Logger.Setup(message: $"Concatenated model parts into {destinationPath}", level: LogEventLevel.Verbose);

        return destinationPath;
    }

    internal async Task DownloadTesseractData(IEnumerable<string> languages)
    {
        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(apiUrl: GithubTesseractApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup(message: "No assets found for TesseractData release.", level: LogEventLevel.Warning);
            return;
        }

        foreach (string lang in languages)
        {
            Uri? downloadUrl = releaseInfo
                .Assets.FirstOrDefault(predicate: a =>
                    a.Name.Equals(value: $"{lang}.traineddata", comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.BrowserDownloadUrl;

            if (downloadUrl == null)
            {
                Logger.Setup(
                    message: $"No asset found for language {lang} in TesseractData release.",
                    level: LogEventLevel.Warning
                );
                continue;
            }

            string destinationPath = Path.Combine(
                path1: AppFiles.TesseractModelsFolder,
                path2: $"{lang}.traineddata"
            );

            if (CheckLocalVersion(releaseInfo: releaseInfo, destination: destinationPath, version: out string version))
            {
                _binaryReport.Add(item: $"Tesseract[{lang}] = {version}");
                continue;
            }

            await Downloader.DeleteSourceDownload(storage: _storage, filePath: destinationPath);

            string assetName = $"{lang}.traineddata";
            string path = await DownloadWithVerificationAsync(
                apiUrl: GithubTesseractApiUrl,
                label: $"Tesseract data for {lang}",
                downloadUrl: downloadUrl,
                destPath: destinationPath,
                releaseInfo: releaseInfo,
                assetName: assetName,
                enforceSignedManifest: true
            );

            await FileAttributes.SetCreatedAttribute(filePath: path, createdAt: releaseInfo.PublishedAt);

            Logger.Setup(message: $"Downloaded Tesseract data for {lang} to {destinationPath}");
        }
    }
}
