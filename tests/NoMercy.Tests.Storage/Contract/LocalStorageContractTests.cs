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
}
