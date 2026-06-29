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
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Files.Parsing;
using NoMercy.MediaProcessing.Files.Parsing.Adapters;
using NoMercy.Setup.Server;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Storage.Remote;
using Xunit;

using Microsoft.Extensions.Logging.Abstractions;
namespace NoMercy.Tests.MediaProcessing.Files;

// ============================================================================
// Live NAS tests for the slice 16.6 file lister. Split so the storage and
// non-fatal-listing behaviours are verified without TMDB; only the
// "identification actually resolved" assertions require a configured token.
// Skipped automatically when the Vault NFS is unreachable (CI off-LAN).
//
// Override the target via NMS_NAS_HOST / NMS_NAS_EXPORT / NMS_NAS_VERSION /
// NMS_NAS_TEST_DIR (export-relative, forward slashes).
// ============================================================================
public sealed class FileListLiveNasTests
{
    private static readonly string NasHost =
        Environment.GetEnvironmentVariable("NMS_NAS_HOST") ?? "192.168.2.120";
    private static readonly string NasExport =
        Environment.GetEnvironmentVariable("NMS_NAS_EXPORT") ?? "/mnt/vault/Media";
    private static readonly int NasVersion = int.TryParse(
        Environment.GetEnvironmentVariable("NMS_NAS_VERSION"),
        out int v
    )
        ? v
        : 4;

    // Default sample dir; override with NMS_NAS_TEST_DIR to point at media that
    // currently exists on your export.
    private static readonly string TestDir =
        Environment.GetEnvironmentVariable("NMS_NAS_TEST_DIR")
        ?? "Anime/Download/[Sokudo] Kyokou Suiri [1080p BD][AV1][dual audio]";

    private const string LibraryType = "anime";

    private const string Skip_Unreachable =
        "NAS not reachable — connect to the home LAN before running live storage tests";

    private static bool IsNasReachable()
    {
        try
        {
            using TcpClient tcp = new();
            IAsyncResult r = tcp.BeginConnect(NasHost, 2049, null, null);
            bool ok = r.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
            if (ok)
                tcp.EndConnect(r);
            return ok && tcp.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static IStorage MountNfs()
    {
        NfsDriverConfig config = NfsDriverConfig.For(
            server: NasHost,
            export: NasExport,
            version: NasVersion,
            uid: 0,
            gid: 0
        );
        NfsStorageDriver driver = new(config);
        return new RemoteStorage(driver);
    }

    private static MediaContext NewContext()
    {
        DbContextOptions<MediaContext> opts = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase("FileListLiveNas_" + Guid.NewGuid())
            .Options;
        return new MediaContext(opts);
    }

    // Storage layer only — no TMDB. Proves enumeration returns the video files.
    [SkippableFact]
    [Trait("Category", "Integration")]
    public void StorageEnumeration_NfsDriver_ReturnsVideoFiles()
    {
        Skip.If(!IsNasReachable(), Skip_Unreachable);

        IStorage storage;
        try
        {
            storage = MountNfs();
        }
        catch (DllNotFoundException ex)
        {
            Skip.If(true, $"libnfs not available: {ex.Message}");
            throw;
        }
        catch (IOException ex)
        {
            Skip.If(true, $"NFS mount failed: {ex.Message}");
            throw;
        }

        IReadOnlyList<StorageEntry> entries = storage.List(
            TestDir,
            pattern: null,
            recursive: false
        );
        StorageEntry[] videos = entries
            .Where(e => !e.IsDirectory && Path.GetExtension(e.Path) is ".mkv" or ".mp4")
            .ToArray();

        Skip.If(
            videos.Length == 0,
            $"No video files under '{TestDir}' on {NasHost}:{NasExport} — set NMS_NAS_TEST_DIR."
        );
        Assert.All(
            videos,
            e =>
                Assert.False(
                    Path.IsPathRooted(e.Path) && e.Path.Contains(':'),
                    $"Path looks like a Windows absolute path: '{e.Path}'"
                )
        );
    }

    // The fix: every video file is listed even when TMDB cannot identify it.
    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task FileList_NfsDriver_ReturnsFileItems_EvenWithoutTmdb()
    {
        Skip.If(!IsNasReachable(), Skip_Unreachable);

        IStorage storage;
        try
        {
            storage = MountNfs();
        }
        catch (DllNotFoundException ex)
        {
            Skip.If(true, $"libnfs not available: {ex.Message}");
            throw;
        }
        catch (IOException ex)
        {
            Skip.If(true, $"NFS mount failed: {ex.Message}");
            throw;
        }

        await using MediaContext context = NewContext();
        MediaIdentificationService identification = new(context);
        FileListService service = new(storage.Driver, identification, new FilenameParserPipeline(new IFilenameParseAdapter[] { new EpisodePrefixAdapter(), new EpisodeWordAdapter(), new SeasonEpisodeAdapter(), new MovieDetectorAdapter() }), NullLogger<FileListService>.Instance);

        List<FileItem> files = await service.GetFilesInDirectory(TestDir, LibraryType, storage);

        Skip.If(
            files.Count == 0,
            $"No media under '{TestDir}' on {NasHost}:{NasExport} — set NMS_NAS_TEST_DIR."
        );
        Assert.All(
            files,
            file =>
            {
                string ext = Path.GetExtension(file.Path);
                Assert.True(
                    ext is ".mkv" or ".mp4",
                    $"Unexpected extension '{ext}' on '{file.Path}'"
                );
                Assert.False(
                    string.IsNullOrWhiteSpace(file.Parsed?.Title),
                    $"Parsed.Title is empty for '{file.Path}'"
                );
            }
        );
    }

    // Identification actually resolved — only meaningful with a TMDB token.
    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task FileList_NfsDriver_ResolvesMatch_WhenTmdbConfigured()
    {
        Skip.If(!IsNasReachable(), Skip_Unreachable);
        Skip.If(
            string.IsNullOrEmpty(ApiKeyStore.Current.TmdbToken),
            "TMDB token not configured — skipping identification assertions."
        );

        IStorage storage;
        try
        {
            storage = MountNfs();
        }
        catch (DllNotFoundException ex)
        {
            Skip.If(true, $"libnfs not available: {ex.Message}");
            throw;
        }
        catch (IOException ex)
        {
            Skip.If(true, $"NFS mount failed: {ex.Message}");
            throw;
        }

        await using MediaContext context = NewContext();
        FileListService service = new(storage.Driver, new MediaIdentificationService(context), new FilenameParserPipeline(new IFilenameParseAdapter[] { new EpisodePrefixAdapter(), new EpisodeWordAdapter(), new SeasonEpisodeAdapter(), new MovieDetectorAdapter() }), NullLogger<FileListService>.Instance);

        List<FileItem> files = await service.GetFilesInDirectory(TestDir, LibraryType, storage);

        Skip.If(files.Count == 0, $"No media under '{TestDir}'.");
        Assert.Contains(files, file => !string.IsNullOrEmpty(file.Match.Title));
    }
}
