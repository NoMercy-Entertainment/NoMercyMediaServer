using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Plugins;
using Xunit;

namespace NoMercy.Tests.Plugins;

// Verifies RegisterPluginServicesFromManifests against the Echo sample plugin.
public class PluginServiceRegistrationTests : IDisposable
{
    private readonly string _tempPluginsDir;
    private readonly string _echoPluginDir;

    public PluginServiceRegistrationTests()
    {
        _tempPluginsDir = Path.Combine(
            Path.GetTempPath(),
            "nomercy-svc-reg-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(_tempPluginsDir);

        _echoPluginDir = Path.Combine(_tempPluginsDir, "Echo");
        Directory.CreateDirectory(_echoPluginDir);
    }

    public void Dispose()
    {
        // Force GC to collect any PluginLoadContext so Windows releases the DLL file lock.
        GC.Collect();
        GC.WaitForPendingFinalizers();

        try
        {
            if (Directory.Exists(_tempPluginsDir))
                Directory.Delete(_tempPluginsDir, recursive: true);
        }
        catch (Exception) { }
    }

    private static string GetEchoPluginBinDir()
    {
        // Navigate from the test bin dir to the Echo project's own output directory.
        // Echo must NOT be loaded into the default AssemblyLoadContext (no ProjectReference
        // copy) — loading it through PluginLoadContext requires clean isolation.
        string testBinDir = Path.GetDirectoryName(
            typeof(PluginServiceRegistrationTests).Assembly.Location
        )!;
        string repoRoot = Path.GetFullPath(Path.Combine(testBinDir, "..", "..", "..", "..", ".."));
        return Path.Combine(
            repoRoot,
            "tests",
            "NoMercy.Plugin.Samples.Echo",
            "bin",
            "Debug",
            "net10.0"
        );
    }

    private void StageEchoPlugin()
    {
        string binDir = GetEchoPluginBinDir();
        string dllSrc = Path.Combine(binDir, "NoMercy.Plugin.Samples.Echo.dll");
        string manifestSrc = Path.Combine(binDir, "plugin.json");

        if (!File.Exists(dllSrc))
            throw new FileNotFoundException(
                $"Echo plugin DLL not found at '{dllSrc}'. Build NoMercy.Plugin.Samples.Echo first."
            );

        foreach (string file in Directory.EnumerateFiles(binDir, "*.dll"))
            File.Copy(file, Path.Combine(_echoPluginDir, Path.GetFileName(file)), overwrite: true);

        if (File.Exists(manifestSrc))
            File.Copy(manifestSrc, Path.Combine(_echoPluginDir, "plugin.json"), overwrite: true);
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_EmptyDir_DoesNotThrow()
    {
        IServiceCollection services = new ServiceCollection();

        Action act = () => services.RegisterPluginServicesFromManifests(_tempPluginsDir);

        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_NonExistentDir_DoesNotThrow()
    {
        IServiceCollection services = new ServiceCollection();
        string missing = Path.Combine(
            Path.GetTempPath(),
            "no-such-" + Guid.NewGuid().ToString("N")
        );

        Action act = () => services.RegisterPluginServicesFromManifests(missing);

        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_EchoPlugin_DoesNotThrow()
    {
        StageEchoPlugin();

        IServiceCollection services = new ServiceCollection();

        Action act = () => services.RegisterPluginServicesFromManifests(_tempPluginsDir);

        // Echo plugin does not implement IPluginServiceRegistrator so no services are added,
        // but the scan itself must never throw.
        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_EchoPlugin_RegistrationCountUnchanged()
    {
        StageEchoPlugin();

        IServiceCollection services = new ServiceCollection();
        int countBefore = services.Count;

        services.RegisterPluginServicesFromManifests(_tempPluginsDir);

        int countAfter = services.Count;

        // Echo does not register any services — count stays the same.
        countAfter.Should().Be(countBefore);
    }

    [Fact]
    public void RegisterPluginServicesFromManifests_MalformedManifest_DoesNotThrow()
    {
        string badDir = Path.Combine(_tempPluginsDir, "Bad");
        Directory.CreateDirectory(badDir);
        File.WriteAllText(Path.Combine(badDir, "plugin.json"), "not json {{{{");

        IServiceCollection services = new ServiceCollection();

        Action act = () => services.RegisterPluginServicesFromManifests(_tempPluginsDir);

        act.Should().NotThrow();
    }
}
