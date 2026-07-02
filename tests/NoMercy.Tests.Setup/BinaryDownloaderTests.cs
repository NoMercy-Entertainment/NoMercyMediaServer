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
using NoMercy.Setup.Dto;
using NoMercy.Setup.Server;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Setup;

/// <summary>
/// Tests for the download-and-verify pipeline in <see cref="Binaries"/>.
/// Uses a stub <see cref="HttpMessageHandler"/> so no network access is required.
/// File I/O is performed against the real filesystem in a per-test temp directory.
/// </summary>
[Trait("Category", "Unit")]
public class BinaryDownloaderTests : IDisposable
{
    private readonly string _tempDir;

    public BinaryDownloaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nomercy-dl-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // -------------------------------------------------------------------------
    // Happy path: no SHA-256 sidecar, no manifest → accepts file as-is
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Download_NoChecksumAvailable_SucceedsAndWritesFile()
    {
        byte[] payload = Encoding.UTF8.GetBytes("binary content");
        string destPath = Path.Combine(_tempDir, "asset.bin");
        string assetUrl = "https://example.com/asset.bin";

        GithubReleaseResponse release = BuildRelease(
            assetName: "asset.bin",
            assetUrl: assetUrl,
            sha256: null,
            includeManifest: false
        );

        FakeHttpHandler handler = new FakeHttpHandler();
        handler.Register(assetUrl, payload);
        handler.RegisterRelease(release);

        Binaries binaries = BuildBinaries(handler);

        string result = await binaries.DownloadWithVerificationAsync(
            "https://api.github.com/test",
            "asset",
            new Uri(assetUrl),
            destPath,
            release,
            "asset.bin"
        );

        result.Should().Be(destPath);
        File.Exists(destPath).Should().BeTrue();
        (await File.ReadAllBytesAsync(destPath)).Should().BeEquivalentTo(payload);
    }

    // -------------------------------------------------------------------------
    // SHA-256 sidecar: matching hash → accepts file
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Download_MatchingSha256Sidecar_SucceedsAndWritesFile()
    {
        byte[] payload = Encoding.UTF8.GetBytes("verified binary");
        string sha256 = Convert.ToHexString(SHA256.HashData(payload));
        string destPath = Path.Combine(_tempDir, "asset.bin");
        string assetUrl = "https://example.com/asset.bin";
        string sha256Url = assetUrl + ".sha256";

        GithubReleaseResponse release = BuildRelease(
            assetName: "asset.bin",
            assetUrl: assetUrl,
            sha256: sha256,
            sha256Url: sha256Url,
            includeManifest: false
        );

        FakeHttpHandler handler = new FakeHttpHandler();
        handler.Register(assetUrl, payload);
        handler.Register(sha256Url, Encoding.ASCII.GetBytes(sha256));
        handler.RegisterRelease(release);

        Binaries binaries = BuildBinaries(handler);

        string result = await binaries.DownloadWithVerificationAsync(
            "https://api.github.com/test",
            "asset",
            new Uri(assetUrl),
            destPath,
            release,
            "asset.bin"
        );

        result.Should().Be(destPath);
        (await File.ReadAllBytesAsync(destPath)).Should().BeEquivalentTo(payload);
    }

    // -------------------------------------------------------------------------
    // SHA-256 sidecar: mismatched hash → throws InvalidDataException
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Download_MismatchedSha256Sidecar_Throws()
    {
        byte[] payload = Encoding.UTF8.GetBytes("tampered binary");
        string wrongHash = new('a', 64);
        string destPath = Path.Combine(_tempDir, "asset.bin");
        string assetUrl = "https://example.com/asset.bin";
        string sha256Url = assetUrl + ".sha256";

        GithubReleaseResponse release = BuildRelease(
            assetName: "asset.bin",
            assetUrl: assetUrl,
            sha256: wrongHash,
            sha256Url: sha256Url,
            includeManifest: false
        );

        FakeHttpHandler handler = new FakeHttpHandler();
        handler.Register(assetUrl, payload);
        handler.Register(sha256Url, Encoding.ASCII.GetBytes(wrongHash));
        handler.RegisterRelease(release);

        Binaries binaries = BuildBinaries(handler);

        Func<Task> act = () =>
            binaries.DownloadWithVerificationAsync(
                "https://api.github.com/test",
                "asset",
                new Uri(assetUrl),
                destPath,
                release,
                "asset.bin"
            );

        await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*SHA-256 mismatch*");

        // temp file must be cleaned up
        File.Exists(destPath + ".tmp").Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Manifest: SHA-256 from manifest, matching → accepts file
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Download_MatchingSha256FromManifest_Succeeds()
    {
        byte[] payload = Encoding.UTF8.GetBytes("manifest-verified binary");
        string sha256 = Convert.ToHexString(SHA256.HashData(payload));
        string destPath = Path.Combine(_tempDir, "asset.bin");
        string assetUrl = "https://example.com/asset.bin";
        string manifestUrl = "https://example.com/manifest.json";

        ReleaseManifest manifest = new()
        {
            Version = "1.0",
            CommitSha = "abc",
            BuildTimestamp = "2025-01-01T00:00:00Z",
            Assets =
            [
                new ManifestAsset
                {
                    Name = "asset.bin",
                    Sha256 = sha256,
                    Size = payload.Length,
                },
            ],
        };
        string manifestJson = JsonConvert.SerializeObject(manifest);

        GithubReleaseResponse release = BuildRelease(
            assetName: "asset.bin",
            assetUrl: assetUrl,
            sha256: null,
            includeManifest: true,
            manifestUrl: manifestUrl
        );

        FakeHttpHandler handler = new FakeHttpHandler();
        handler.Register(assetUrl, payload);
        handler.Register(manifestUrl, Encoding.UTF8.GetBytes(manifestJson));
        handler.RegisterRelease(release);

        Binaries binaries = BuildBinaries(handler);

        string result = await binaries.DownloadWithVerificationAsync(
            "https://api.github.com/test",
            "asset",
            new Uri(assetUrl),
            destPath,
            release,
            "asset.bin"
        );

        result.Should().Be(destPath);
        (await File.ReadAllBytesAsync(destPath)).Should().BeEquivalentTo(payload);
    }

    // -------------------------------------------------------------------------
    // Atomic swap: existing file is backed up, restored on failure
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Download_ExistingFileBackedUp_RestoredOnHashMismatch()
    {
        byte[] originalContent = Encoding.UTF8.GetBytes("original content");
        byte[] newPayload = Encoding.UTF8.GetBytes("new binary that fails sha");
        string wrongHash = new('b', 64);
        string destPath = Path.Combine(_tempDir, "asset.bin");
        string assetUrl = "https://example.com/asset.bin";
        string sha256Url = assetUrl + ".sha256";

        await File.WriteAllBytesAsync(destPath, originalContent);

        GithubReleaseResponse release = BuildRelease(
            assetName: "asset.bin",
            assetUrl: assetUrl,
            sha256: wrongHash,
            sha256Url: sha256Url,
            includeManifest: false
        );

        FakeHttpHandler handler = new FakeHttpHandler();
        handler.Register(assetUrl, newPayload);
        handler.Register(sha256Url, Encoding.ASCII.GetBytes(wrongHash));
        handler.RegisterRelease(release);

        Binaries binaries = BuildBinaries(handler);

        Func<Task> act = () =>
            binaries.DownloadWithVerificationAsync(
                "https://api.github.com/test",
                "asset",
                new Uri(assetUrl),
                destPath,
                release,
                "asset.bin"
            );

        await act.Should().ThrowAsync<InvalidDataException>();

        // Original file must still be present (was not overwritten before verification)
        File.Exists(destPath).Should().BeTrue();
        (await File.ReadAllBytesAsync(destPath)).Should().BeEquivalentTo(originalContent);
    }

    // -------------------------------------------------------------------------
    // Integration scaffold (skip in normal CI — demonstrates signature flow)
    // -------------------------------------------------------------------------

    [Fact(Skip = "Integration scaffold — requires a real signed manifest from CI")]
    public async Task Integration_SignedManifest_VerifiesCorrectly()
    {
        // This test demonstrates the end-to-end flow when the org GPG key is
        // embedded via the build-executables.yml "Embed org GPG public key" step.
        //
        // To make this runnable locally:
        //   1. Export the org public key:
        //        gpg --armor --export <KEY_ID> > src/NoMercy.Setup/Resources/nomercy-public-key.asc
        //   2. Set env NOMERCY_TEST_MANIFEST_URL to a real manifest.json URL
        //   3. Set env NOMERCY_TEST_SIG_URL to the corresponding manifest.json.sig URL
        //   4. Remove the [Skip] attribute and run: dotnet test --filter BinaryDownloaderTests

        string manifestUrl =
            Environment.GetEnvironmentVariable("NOMERCY_TEST_MANIFEST_URL")
            ?? "https://github.com/NoMercy-Entertainment/nomercy-media-server/releases/latest/download/manifest.json";

        string sigUrl =
            Environment.GetEnvironmentVariable("NOMERCY_TEST_SIG_URL") ?? manifestUrl + ".sig";

        using HttpClient http = new();
        string manifestJson = await http.GetStringAsync(manifestUrl);
        string armoredSig = await http.GetStringAsync(sigUrl);

        bool verified = BinaryVerification.VerifyManifestSignature(manifestJson, armoredSig);

        verified
            .Should()
            .BeTrue("the org-signed manifest must verify against the embedded public key");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private Binaries BuildBinaries(FakeHttpHandler handler)
    {
        LocalStorageDriver driver = new();
        StoragePathGuard guard = new([], driver);
        LocalStorage storage = new(driver, guard);
        HttpClient http = new(handler);
        return new Binaries(driver, storage, http);
    }

    private static GithubReleaseResponse BuildRelease(
        string assetName,
        string assetUrl,
        string? sha256,
        string? sha256Url = null,
        bool includeManifest = false,
        string? manifestUrl = null
    )
    {
        List<Asset> assets =
        [
            new Asset
            {
                Name = assetName,
                BrowserDownloadUrl = new Uri(assetUrl),
                Size = 1,
            },
        ];

        if (sha256 is not null && sha256Url is not null)
        {
            assets.Add(
                new Asset
                {
                    Name = assetName + ".sha256",
                    BrowserDownloadUrl = new Uri(sha256Url),
                    Size = 64,
                }
            );
        }

        if (includeManifest && manifestUrl is not null)
        {
            assets.Add(
                new Asset
                {
                    Name = "manifest.json",
                    BrowserDownloadUrl = new Uri(manifestUrl),
                    Size = 100,
                }
            );
        }

        return new GithubReleaseResponse
        {
            TagName = "v1.0.0",
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Assets = assets.ToArray(),
        };
    }
}

/// <summary>
/// Stub <see cref="HttpMessageHandler"/> that serves pre-registered byte arrays for
/// registered URLs and returns 404 for everything else.
/// </summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, byte[]> _responses = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string url, byte[] body) => _responses[url] = body;

    public void RegisterRelease(GithubReleaseResponse release)
    {
        // Pre-register the GitHub API URL response as JSON
        // (not used by DownloadWithVerificationAsync directly — it receives the parsed object)
    }

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
