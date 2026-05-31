using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Storage.Contract;

/// <summary>
/// Contract test driver for <see cref="LocalStorage"/>.
/// Uses a real temp directory as the backend; the <see cref="StoragePathGuard"/>
/// is configured with that directory as its single allowed root so all path
/// enforcement is live.
///
/// Seed* methods write directly via System.IO so no IStorage abstraction is
/// involved in setup — failures here are driver fidelity failures, not setup bugs.
/// BackendHasFile reads via System.IO for the same reason.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LocalStorageContractTests : IStorageContractTests
{
    private string _root = string.Empty;
    private LocalStorage? _storage;

    protected override IStorage CreateStorage()
    {
        _root = Path.Combine(Path.GetTempPath(), $"nm-contract-local-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);

        LocalStorageDriver driver = new();
        StoragePathGuard guard = new([_root], driver);
        _storage = new LocalStorage(driver, guard);
        return _storage;
    }

    protected override Task SeedFile(string relativePath, byte[] content)
    {
        string full = ToFull(relativePath);
        string? dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(full, content);
        return Task.CompletedTask;
    }

    protected override Task SeedDirectory(string relativePath)
    {
        Directory.CreateDirectory(ToFull(relativePath));
        return Task.CompletedTask;
    }

    protected override Task<bool> BackendHasFile(string relativePath)
    {
        bool exists = File.Exists(ToFull(relativePath));
        return Task.FromResult(exists);
    }

    protected override Task DisposeStorage()
    {
        try
        {
            if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best-effort
        }
        _storage = null;
        return Task.CompletedTask;
    }

    private string ToFull(string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/').TrimStart('/');
        return Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar));
    }

    // -----------------------------------------------------------------------
    // LocalStorage-specific: absolute path MUST throw StoragePathNotAllowedException
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public override async Task Exists_absolute_path_is_rejected_or_returns_false()
    {
        IStorage storage = CreateStorage();
        try
        {
            string absolutePath = OperatingSystem.IsWindows()
                ? @"C:\Windows\System32\drivers\etc\hosts"
                : "/etc/hosts";

            Func<Task> act = () => storage.ExistsAsync(absolutePath, CancellationToken.None);
            await act.Should()
                .ThrowAsync<StoragePathNotAllowedException>(
                    "LocalStorage with a configured root must reject any path outside that root"
                );
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // LocalStorage-specific: ".." traversal MUST throw StoragePathNotAllowedException
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public override async Task Exists_dotdot_traversal_throws()
    {
        IStorage storage = CreateStorage();
        try
        {
            Func<Task> act = () => storage.ExistsAsync("../escape", CancellationToken.None);
            await act.Should()
                .ThrowAsync<StoragePathNotAllowedException>(
                    "'..' escaping the allowed root must be rejected with StoragePathNotAllowedException"
                );
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // Rule 6 contract: StorageEntry.Path from List* is scope-relative
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task List_entries_are_scope_relative_and_round_trip_into_Exists()
    {
        IStorage storage = CreateStorage();
        try
        {
            await SeedFile("movies/avatar/avatar.mkv", [0x01, 0x02]);
            await SeedDirectory("movies/avatar");

            List<StorageEntry> entries = [];
            await foreach (
                StorageEntry e in storage.ListAsync(
                    "movies/avatar",
                    "*",
                    recursive: true,
                    CancellationToken.None
                )
            )
                entries.Add(e);

            entries.Should().NotBeEmpty();

            foreach (StorageEntry entry in entries)
            {
                entry
                    .Path.Should()
                    .NotContain(":\\", "StorageEntry.Path must not contain a Windows drive letter");
                entry
                    .Path.Should()
                    .NotStartWith("/", "StorageEntry.Path must not start with a leading slash");
                entry
                    .Path.ToLowerInvariant()
                    .Should()
                    .NotContain(
                        _root.ToLowerInvariant(),
                        "StorageEntry.Path must not contain the OS root prefix"
                    );

                bool roundTrip = await storage.ExistsAsync(entry.Path, CancellationToken.None);
                roundTrip
                    .Should()
                    .BeTrue(
                        $"StorageEntry.Path '{entry.Path}' returned from List must be passable back into Exists"
                    );
            }
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task List_sync_entries_are_scope_relative_and_round_trip_into_Exists()
    {
        IStorage storage = CreateStorage();
        try
        {
            await SeedFile("shows/breaking-bad/s01e01.mkv", [0x03, 0x04]);

            IReadOnlyList<StorageEntry> entries = storage.List(
                "shows/breaking-bad",
                "*",
                recursive: false
            );

            entries.Should().NotBeEmpty();

            foreach (StorageEntry entry in entries)
            {
                entry
                    .Path.Should()
                    .NotContain(":\\", "StorageEntry.Path must not contain a Windows drive letter");
                entry
                    .Path.Should()
                    .NotStartWith("/", "StorageEntry.Path must not start with a leading slash");
                entry
                    .Path.ToLowerInvariant()
                    .Should()
                    .NotContain(
                        _root.ToLowerInvariant(),
                        "StorageEntry.Path must not contain the OS root prefix"
                    );

                bool roundTrip = await storage.ExistsAsync(entry.Path, CancellationToken.None);
                roundTrip
                    .Should()
                    .BeTrue(
                        $"StorageEntry.Path '{entry.Path}' returned from List (sync) must be passable back into Exists"
                    );
            }
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // GetFullPath contract
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public void GetFullPath_scope_relative_returns_os_absolute_under_root()
    {
        IStorage storage = CreateStorage();
        try
        {
            string result = storage.GetFullPath("movies/avatar/avatar.mkv");

            Path.IsPathRooted(result)
                .Should()
                .BeTrue("GetFullPath must return an OS-absolute path");
            result
                .ToLowerInvariant()
                .Should()
                .StartWith(_root.ToLowerInvariant(), "result must be under the configured root");
        }
        finally
        {
            _storage = null;
            if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetFullPath_dotdot_traversal_throws()
    {
        IStorage storage = CreateStorage();
        try
        {
            Action act = () => storage.GetFullPath("../escape/secret.txt");
            act.Should()
                .Throw<StoragePathNotAllowedException>(
                    ".. traversal must be rejected by GetFullPath"
                );
        }
        finally
        {
            _storage = null;
            if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
