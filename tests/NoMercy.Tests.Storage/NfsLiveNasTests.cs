using System.Net.Sockets;
using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Storage.Remote;

namespace NoMercy.Tests.Storage;

// ============================================================================
// Live NAS tests — target a real NFS server (defaults to Stoney's TrueNAS at
// 192.168.2.120:/mnt/vault/Media). Override via env vars NMS_NAS_HOST,
// NMS_NAS_EXPORT, NMS_NAS_VERSION.
//
// These tests skip when the NAS is unreachable, so they don't break CI on
// machines outside the LAN. They're meant to drive bug-hunting against the
// real libnfs↔TrueNAS interaction without round-tripping through the dev
// server / Rider.
//
// Path layout the tests rely on (relative to the export root):
//   Music/                       — directory
//   Music/B/bbno$/               — directory
//   Music/B/bbno$/[2025] bbno$/  — directory
//   Music/B/bbno$/[2025] bbno$/03 gigolo.mp3 — file ≥ 4 MB
//   Music/B/bbno$/[2025] bbno$/04 eat slay love [bbno$ & Käärijä].mp3
//                                — file with non-ASCII filename
// ============================================================================

public sealed class LiveNasFixture
{
    public string Host { get; } =
        Environment.GetEnvironmentVariable("NMS_NAS_HOST") ?? "192.168.2.120";
    public string Export { get; } =
        Environment.GetEnvironmentVariable("NMS_NAS_EXPORT") ?? "/mnt/vault/Media";
    public int Version { get; } =
        int.TryParse(Environment.GetEnvironmentVariable("NMS_NAS_VERSION"), out int v) ? v : 4;

    public bool? _reachable;

    public bool Reachable
    {
        get
        {
            if (_reachable.HasValue)
                return _reachable.Value;

            try
            {
                using TcpClient tcp = new();
                IAsyncResult result = tcp.BeginConnect(Host, 2049, null, null);
                bool ok = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
                if (ok)
                    tcp.EndConnect(result);
                _reachable = ok && tcp.Connected;
            }
            catch
            {
                _reachable = false;
            }
            return _reachable.Value;
        }
    }

    public NfsStorageDriver Mount()
    {
        NfsDriverConfig config = NfsDriverConfig.For(
            server: Host,
            export: Export,
            version: Version,
            uid: 0,
            gid: 0
        );
        return new NfsStorageDriver(config);
    }
}

public class NfsLiveNasTests(LiveNasFixture fix) : IClassFixture<LiveNasFixture>
{
    private const string Skip_Unreachable =
        "NAS not reachable — set NMS_NAS_HOST/NMS_NAS_EXPORT to point at a live NFS server";

    private const string KnownDir = "Music/B/bbno$";
    private const string KnownDeepDir = "Music/B/bbno$/[2025] bbno$";
    private const string KnownFile = "Music/B/bbno$/[2025] bbno$/03 gigolo.mp3";
    private const string KnownUtf8File =
        "Music/B/bbno$/[2025] bbno$/04 eat slay love [bbno$ & Käärijä].mp3";

    private NfsStorageDriver Mount()
    {
        Skip.If(!fix.Reachable, Skip_Unreachable);
        try
        {
            return fix.Mount();
        }
        catch (DllNotFoundException ex)
        {
            Skip.If(true, $"libnfs native library not loadable: {ex.Message}");
            throw;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Skip.If(true, $"NFS mount failed: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    [SkippableFact]
    public void Mount_succeeds()
    {
        using NfsStorageDriver driver = Mount();
        // Reaching here = mount completed without throwing
        driver.Should().NotBeNull();
    }

    [SkippableFact]
    public void EnumerateRoot_returns_entries()
    {
        using NfsStorageDriver driver = Mount();
        List<string> entries =
        [
            .. driver.EnumerateFileSystemEntries("/", "*", SearchOption.TopDirectoryOnly),
        ];
        entries.Should().NotBeEmpty();
    }

    [SkippableFact]
    public void EnumerateRoot_contains_Music_dir()
    {
        using NfsStorageDriver driver = Mount();
        List<string> entries =
        [
            .. driver.EnumerateFileSystemEntries("/", "*", SearchOption.TopDirectoryOnly),
        ];
        entries
            .Should()
            .Contain(e => e == "/Music" || e.EndsWith("/Music", StringComparison.Ordinal));
    }

    // --- Directory.Exists semantics — these are the bug-finder tests --------

    [SkippableFact]
    public void DirectoryExists_returns_true_for_root()
    {
        using NfsStorageDriver driver = Mount();
        driver.DirectoryExists("/").Should().BeTrue();
    }

    [SkippableFact]
    public void DirectoryExists_returns_true_for_top_level_dir()
    {
        using NfsStorageDriver driver = Mount();
        driver.DirectoryExists("Music").Should().BeTrue();
    }

    [SkippableFact]
    public void DirectoryExists_returns_true_for_nested_dir()
    {
        using NfsStorageDriver driver = Mount();
        driver.DirectoryExists(KnownDir).Should().BeTrue();
    }

    [SkippableFact]
    public void DirectoryExists_returns_true_for_dir_with_brackets_and_dollar()
    {
        using NfsStorageDriver driver = Mount();
        driver.DirectoryExists(KnownDeepDir).Should().BeTrue();
    }

    [SkippableFact]
    public void DirectoryExists_returns_false_for_nonexistent()
    {
        using NfsStorageDriver driver = Mount();
        driver.DirectoryExists("does/not/exist").Should().BeFalse();
    }

    // --- File.Exists / Size ---------------------------------------------------

    [SkippableFact]
    public void FileExists_returns_true_for_known_file()
    {
        using NfsStorageDriver driver = Mount();
        driver.FileExists(KnownFile).Should().BeTrue();
    }

    [SkippableFact]
    public void FileExists_returns_false_for_dir()
    {
        using NfsStorageDriver driver = Mount();
        driver.FileExists(KnownDir).Should().BeFalse();
    }

    [SkippableFact]
    public void GetFileSize_returns_positive_for_known_file()
    {
        using NfsStorageDriver driver = Mount();
        driver.GetFileSize(KnownFile).Should().BeGreaterThan(0);
    }

    [SkippableFact]
    public void OpenRead_returns_mp3_with_id3_signature()
    {
        using NfsStorageDriver driver = Mount();
        using Stream s = driver.OpenRead(KnownFile);
        byte[] head = new byte[8];
        int read = s.Read(head, 0, head.Length);
        read.Should().Be(8);
        // Either ID3v2 ("ID3...") or MP3 frame sync (0xFF 0xEx)
        bool isId3 = head[0] == 0x49 && head[1] == 0x44 && head[2] == 0x33;
        bool isSync = head[0] == 0xFF && (head[1] & 0xE0) == 0xE0;
        (isId3 || isSync).Should().BeTrue($"unexpected header: {BitConverter.ToString(head)}");
    }

    // --- UTF-8 path handling --------------------------------------------------

    [SkippableFact]
    public void FileExists_returns_true_for_utf8_filename()
    {
        using NfsStorageDriver driver = Mount();
        driver.FileExists(KnownUtf8File).Should().BeTrue();
    }

    [SkippableFact]
    public void EnumerateDirectory_preserves_utf8_filenames()
    {
        using NfsStorageDriver driver = Mount();
        List<string> entries =
        [
            .. driver.EnumerateFileSystemEntries(KnownDeepDir, "*", SearchOption.TopDirectoryOnly),
        ];

        entries
            .Should()
            .Contain(
                e => e.Contains("Käärijä", StringComparison.Ordinal),
                "libnfs returns NFS3/4 names as UTF-8; CharSet.Ansi mangled them to 'KÃ¤Ã¤rijÃ¤'"
            );
    }

    // --- Listing returns correct directory flag ------------------------------

    // --- RemoteStorage.List path (mirrors the API browser exactly) ----------

    [SkippableFact]
    public void RemoteStorage_List_marks_subdirs_correctly()
    {
        Skip.If(!fix.Reachable, Skip_Unreachable);
        using NfsStorageDriver driver = Mount();
        RemoteStorage rs = new(driver);

        IReadOnlyList<StorageEntry> entries = rs.List("Music", pattern: null, recursive: false);
        entries.Should().NotBeEmpty();

        // 'Music' on the export contains alphabet directories (#, A, B, ...).
        // Every direct child should report IsDirectory == true.
        IReadOnlyList<StorageEntry> dirs = [.. entries.Where(e => e.IsDirectory)];
        dirs.Should()
            .HaveCountGreaterThan(
                0,
                "RemoteStorage.List uses driver.DirectoryExists(entryPath) to set IsDirectory; "
                    + "if Stat64 returns wrong mode bits or fails for nested paths, this stays empty"
            );
    }

    [SkippableFact]
    public void ListDirectories_marks_subdirs_as_directories()
    {
        using NfsStorageDriver driver = Mount();
        List<(string Name, bool IsDirectory)> entries = driver.ListDirectories("Music/B/bbno$");

        // The bbno$ folder contains album subdirectories like "[2025] bbno$".
        // ListDirectories filters to only directories, so any returned entry
        // must be IsDirectory == true.
        entries.Should().NotBeEmpty();
        entries.Should().OnlyContain(e => e.IsDirectory);
        entries.Should().Contain(e => e.Name.Contains("[2025]"));
    }
}
