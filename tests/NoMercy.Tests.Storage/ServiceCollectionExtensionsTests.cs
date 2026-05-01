using Microsoft.Extensions.DependencyInjection;
using NoMercy.Storage;
using NoMercy.Storage.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Storage;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNoMercyStorage_registers_default_local_pipeline()
    {
        ServiceCollection services = new();
        services.AddNoMercyStorage();

        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStorage>().Should().BeOfType<LocalStorage>();
        provider.GetRequiredService<IStorageBackend>().Should().BeOfType<SystemIoStorageBackend>();
        provider.GetRequiredService<StorageOptions>().AllowedRoots.Should().BeEmpty();
        provider.GetRequiredService<StoragePathGuard>().Enforced.Should().BeFalse();
    }

    [Fact]
    public void AddNoMercyStorage_applies_options_callback()
    {
        ServiceCollection services = new();
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-svc-test-root"));

        services.AddNoMercyStorage(opts => opts.AllowedRoots.Add(root));

        ServiceProvider provider = services.BuildServiceProvider();
        StoragePathGuard guard = provider.GetRequiredService<StoragePathGuard>();

        guard.Enforced.Should().BeTrue();
        guard.AllowedRoots.Should().ContainSingle();
    }

    [Fact]
    public void AddNoMercyStorage_is_idempotent()
    {
        ServiceCollection services = new();
        services.AddNoMercyStorage();
        services.AddNoMercyStorage(opts => opts.AllowedRoots.Add("/should-be-ignored"));

        ServiceProvider provider = services.BuildServiceProvider();

        // First registration wins (TryAdd semantics).
        provider.GetRequiredService<StorageOptions>().AllowedRoots.Should().BeEmpty();
    }
}
