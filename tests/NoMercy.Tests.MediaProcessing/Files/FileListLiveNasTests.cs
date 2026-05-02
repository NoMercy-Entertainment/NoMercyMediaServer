using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.MediaProcessing.Files;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Storage.Remote;
using Xunit;

namespace NoMercy.Tests.MediaProcessing.Files;

// ============================================================================
// Live NAS tests — driver-aware GetFilesInDirectory against the Vault NFS.
//
// Skipped automatically when 192.168.2.120:2049 is unreachable so CI on
// machines outside the LAN is unaffected.
//
// Expected path on the export: Download/complete/[Sokudo] Kuroshitsuji v3
//   [1080p AV1][Dual Audio]/Season 05 - Emerald Witch Arc
// ============================================================================

public sealed class FileListLiveNasTests
{
    private const string NasHost = "192.168.2.120";
    private const string NasExport = "/mnt/vault/Media";
    private const int NasVersion = 4;

    private const string AnimeSeasonDir =
        "Download/complete/[Sokudo] Kuroshitsuji v3 [1080p AV1][Dual Audio]/Season 05 - Emerald Witch Arc";

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

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GetFilesInDirectory_NfsDriver_ReturnsEntries()
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

        DbContextOptions<MediaContext> opts = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase("FileListLiveNas_" + Guid.NewGuid())
            .Options;

        await using MediaContext context = new(opts);
        FileRepository repo = new(context, storage.Driver);

        List<FileItem> files = await repo.GetFilesInDirectory(
            AnimeSeasonDir,
            "anime",
            storage
        );

        Assert.NotEmpty(files);

        foreach (FileItem item in files)
        {
            string ext = Path.GetExtension(item.Path);
            Assert.True(
                ext == ".mkv" || ext == ".mp4",
                $"Unexpected extension '{ext}' on path '{item.Path}'"
            );

            Assert.False(
                string.IsNullOrWhiteSpace(item.Parsed?.Title),
                $"Parsed.Title is empty for '{item.Path}'"
            );
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GetFilesInDirectory_NfsDriver_PathsAreDriverRelative()
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

        DbContextOptions<MediaContext> opts = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase("FileListLiveNas_paths_" + Guid.NewGuid())
            .Options;

        await using MediaContext context = new(opts);
        FileRepository repo = new(context, storage.Driver);

        List<FileItem> files = await repo.GetFilesInDirectory(
            AnimeSeasonDir,
            "anime",
            storage
        );

        foreach (FileItem item in files)
        {
            Assert.False(
                Path.IsPathRooted(item.Path) && item.Path.Contains(':'),
                $"Path looks like a Windows absolute path: '{item.Path}' — should be driver-relative"
            );

            Assert.True(
                item.Path.StartsWith("Download/", StringComparison.Ordinal)
                    || item.Path.StartsWith("/Download/", StringComparison.Ordinal),
                $"Path '{item.Path}' does not start with expected driver-relative prefix"
            );
        }
    }
}
