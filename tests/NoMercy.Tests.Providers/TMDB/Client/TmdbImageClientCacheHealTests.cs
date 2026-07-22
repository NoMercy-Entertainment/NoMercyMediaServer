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

using FluentAssertions;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// A 200 response from image.tmdb.org can still carry a non-image body (an HTML
/// error page, a truncated CDN response). The client used to persist those bytes
/// before decoding, poisoning the cache: the bad file then satisfied the Exists
/// check on every later scan and the decode failed forever. The cached-load path
/// must discard an undecodable cache entry so the next run re-downloads.
/// </summary>
[Trait(name: "Category", value: "Unit")]
[Collection(name: "TmdbImageClient")]
public class TmdbImageClientCacheHealTests
{
    private static IStorage RealScopedStorage()
    {
        LocalStorageDriver driver = new();
        StoragePathGuard guard = new(allowedRoots: [AppFiles.AppPath], driver: driver);
        return new LocalStorage(driver: driver, guard: guard);
    }

    [Fact]
    public async Task Download_UndecodableCachedFile_IsDeletedSoItCanBeRefetched()
    {
        IStorage storage = RealScopedStorage();
        TmdbImageClient.Initialize(storage: storage);

        string folder = Path.Join(path1: AppFiles.ImagesPath, path2: "original");
        Directory.CreateDirectory(path: folder);

        // A poison cache entry: a .jpg name holding a non-image body, exactly the
        // shape an older build would have written from a bad 200 response.
        const string fileName = "poison-cache-heal-test.jpg";
        string filePath = Path.Join(path1: folder, path2: fileName);
        await File.WriteAllTextAsync(path: filePath, contents: "<html><body>Not Found</body></html>");

        File.Exists(path: filePath).Should().BeTrue(because: "the poison file is seeded before the call");

        // Download is keyed by "/{fileName}" -> path.Replace("/","") == fileName.
        // After discarding the poison entry the client re-downloads; with no
        // real HTTP backend configured that GET throws, which is fine — the
        // behavior under test is that the poison cache entry was discarded
        // before the re-fetch. Swallow the expected network failure.
        try
        {
            Task<SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>?>? download =
                TmdbImageClient.Download(path: "/" + fileName);
            if (download is not null)
                await download;
        }
        catch (InvalidOperationException)
        {
            // No BaseAddress on the test HttpClient — the re-download leg fails
            // after the poison entry was already deleted. Expected.
        }

        // The poison bytes must no longer be the cache entry. The self-heal path
        // deletes the undecodable file before re-downloading, so it is gone (the
        // failed re-fetch wrote nothing) — never left as the original HTML poison,
        // which would make every later scan fail forever.
        File.Exists(path: filePath)
            .Should()
            .BeFalse(
                because: "the undecodable poison cache entry must be discarded so the next run re-downloads a clean copy"
            );
    }
}
