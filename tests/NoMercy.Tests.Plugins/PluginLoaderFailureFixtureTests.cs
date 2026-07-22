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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Events;
using NoMercy.Events.Plugins;
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Plugins;

/// <summary>
/// Stages the NoMercy.Plugin.Samples.Failures fixture assembly — three real
/// IPlugin types (a healthy one, one whose Initialize/Dispose both throw, and
/// one whose constructor throws) plus a healthy/abstract
/// IPluginServiceRegistrator pair — and drives them through
/// <c>PluginManager.LoadPluginAssemblyAsync</c> directly. That is the ONE
/// loader entry point with true per-type failure isolation (unlike the
/// manifest path, which represents a single logical plugin per manifest.Id),
/// so a single call here exercises the loader's full success AND malfunction
/// handling for a multi-type assembly in one pass.
///
/// The fixture project is referenced with ReferenceOutputAssembly="false" (its
/// DLL must never land in the default AssemblyLoadContext — see
/// NoMercy.Tests.Plugins.csproj), so its types are never usable at compile
/// time here. The fixed plugin ids below are copied from the fixture's own
/// source (NoMercy.Plugin.Samples.Failures/*.cs) rather than referenced.
/// </summary>
public class PluginLoaderFailureFixtureTests : IDisposable
{
    private static readonly Guid ConstructorThrowsPluginId = Guid.Parse(
        input: "11111111-0000-0000-0000-000000000001"
    );
    private static readonly Guid InitializeThrowsPluginId = Guid.Parse(
        input: "22222222-0000-0000-0000-000000000002"
    );
    private static readonly Guid ServiceRegistratorPluginId = Guid.Parse(
        input: "33333333-0000-0000-0000-000000000003"
    );
    private static readonly Guid InitializeThrowsDisposeSucceedsPluginId = Guid.Parse(
        input: "44444444-0000-0000-0000-000000000004"
    );
    private static readonly Guid TypeSignatureDependsOnMissingAssemblyPluginId = Guid.Parse(
        input: "55555555-0000-0000-0000-000000000005"
    );

    private readonly string _tempPluginsDir;
    private readonly InMemoryEventBus _eventBus;
    private readonly PluginManager _manager;

    public PluginLoaderFailureFixtureTests()
    {
        _tempPluginsDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nomercy-loader-failures-" + Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: _tempPluginsDir);

        _eventBus = new();
        _manager = new(
            eventBus: _eventBus,
            serviceProvider: new MinimalServiceProvider(),
            logger: NullLogger<PluginManager>.Instance,
            pluginsPath: _tempPluginsDir,
            storage: TestStorageHelper.CreateStorage(rootPath: _tempPluginsDir),
            driver: TestStorageHelper.CreateBackend()
        );
    }

    public void Dispose()
    {
        _manager.Dispose();

        // Force GC to collect the PluginLoadContext so Windows releases the DLL file lock.
        GC.Collect();
        GC.WaitForPendingFinalizers();

        try
        {
            if (Directory.Exists(path: _tempPluginsDir))
                Directory.Delete(path: _tempPluginsDir, recursive: true);
        }
        catch (Exception) { }
    }

    private static string GetFailuresPluginBinDir()
    {
        string testBinDir = Path.GetDirectoryName(
            path: typeof(PluginLoaderFailureFixtureTests).Assembly.Location
        )!;
        string tfmDir = testBinDir;
        string configDir = Path.GetDirectoryName(path: tfmDir)!;
        string buildConfig = Path.GetFileName(path: configDir);
        string repoRoot = Path.GetFullPath(path: Path.Combine(paths: [testBinDir, "..", "..", "..", "..", ".."]));

        return Path.Combine(paths: [repoRoot, "tests", "NoMercy.Plugin.Samples.Failures", "bin", buildConfig, "net10.0"]
        );
    }

    private string StageFailuresPluginDll()
    {
        string binDir = GetFailuresPluginBinDir();
        string dllSrc = Path.Combine(path1: binDir, path2: "NoMercy.Plugin.Samples.Failures.dll");

        if (!File.Exists(path: dllSrc))
            throw new FileNotFoundException(
                message: $"Failures plugin DLL not found at '{dllSrc}'. Build NoMercy.Plugin.Samples.Failures first."
            );

        string pluginDir = Path.Combine(path1: _tempPluginsDir, path2: "Failures");
        Directory.CreateDirectory(path: pluginDir);

        foreach (string file in Directory.EnumerateFiles(path: binDir, searchPattern: "*.dll"))
            File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: pluginDir, path2: Path.GetFileName(path: file)), overwrite: true);

        foreach (string file in Directory.EnumerateFiles(path: binDir, searchPattern: "*.deps.json"))
            File.Copy(sourceFileName: file, destFileName: Path.Combine(path1: pluginDir, path2: Path.GetFileName(path: file)), overwrite: true);

        return Path.Combine(path1: pluginDir, path2: "NoMercy.Plugin.Samples.Failures.dll");
    }

    // NoMercy.Plugins.Abstractions.dll is a real, validly-loadable .NET
    // assembly that defines zero concrete IPlugin implementations (only
    // interfaces, enums, and DTOs) — a real assembly with no plugin types is
    // exactly the case LoadPluginAssemblyAsync's `pluginTypes.Count == 0` guard
    // exists for, with no new fixture needed.
    private static string GetAbstractionsAssemblyPath()
    {
        return typeof(IPlugin).Assembly.Location;
    }

    [Fact]
    public async Task LoadPluginAssemblyAsync_MultiTypeAssembly_IsolatesEachTypesFailure()
    {
        string dllPath = StageFailuresPluginDll();

        await _manager.LoadPluginAssemblyAsync(assemblyPath: dllPath);

        IReadOnlyList<PluginInfo> installed = _manager.GetInstalledPlugins();

        // ConstructorThrowsPlugin never produced an instance, so SafePluginIdentity
        // read a null instance back — Id stayed Guid.Empty, and the loader's
        // `if (identity.Id != Guid.Empty)` guard means it was never recorded at all.
        installed.Should().NotContain(predicate: p => p.Id == ConstructorThrowsPluginId);

        // InitializeThrowsPlugin constructed fine (a real, non-empty Id was read),
        // so its failure IS recorded — as Malfunctioned, not silently dropped.
        // Its OWN Dispose() also throws, exercising the nested disposeEx catch.
        PluginInfo? malfunctioned = installed.FirstOrDefault(predicate: p => p.Id == InitializeThrowsPluginId);
        malfunctioned.Should().NotBeNull();
        malfunctioned!.Status.Should().Be(expected: PluginStatus.Malfunctioned);

        // Same Initialize-throws shape, but its Dispose() succeeds cleanly —
        // the complementary case to InitializeThrowsPlugin above.
        PluginInfo? malfunctionedCleanDispose = installed.FirstOrDefault(predicate: p =>
            p.Id == InitializeThrowsDisposeSucceedsPluginId
        );
        malfunctionedCleanDispose.Should().NotBeNull();
        malfunctionedCleanDispose!.Status.Should().Be(expected: PluginStatus.Malfunctioned);

        // ServiceRegistratorPlugin is one of two healthy types — it must load
        // Active with a live instance, proving the failing types never aborted
        // the rest of the assembly's load.
        PluginInfo? healthy = installed.FirstOrDefault(predicate: p => p.Id == ServiceRegistratorPluginId);
        healthy.Should().NotBeNull();
        healthy!.Status.Should().Be(expected: PluginStatus.Active);
        _manager.GetPluginInstance(pluginId: ServiceRegistratorPluginId).Should().NotBeNull();

        // The other healthy type — its own type SIGNATURE references
        // Newtonsoft.Json (present here), unlike ServiceRegistratorPlugin which
        // only references it from inside a method body.
        PluginInfo? otherHealthy = installed.FirstOrDefault(predicate: p =>
            p.Id == TypeSignatureDependsOnMissingAssemblyPluginId
        );
        otherHealthy.Should().NotBeNull();
        otherHealthy!.Status.Should().Be(expected: PluginStatus.Active);

        installed.Should().HaveCount(expected: 4, because: "ConstructorThrowsPlugin must never reach the registry");
    }

    [Fact]
    public async Task LoadPluginAssemblyAsync_AssemblyWithNoPluginTypes_UnloadsContextWithoutRegisteringAnything()
    {
        string abstractionsPath = GetAbstractionsAssemblyPath();

        Func<Task> act = () => _manager.LoadPluginAssemblyAsync(assemblyPath: abstractionsPath);

        await act.Should().NotThrowAsync();
        _manager.GetInstalledPlugins().Should().BeEmpty();
    }

    [Fact]
    public async Task GetPluginsOfType_MixedRegistry_ReturnsOnlyMatchingActiveInstances()
    {
        string dllPath = StageFailuresPluginDll();
        await _manager.LoadPluginAssemblyAsync(assemblyPath: dllPath);

        // Two Active instances (ServiceRegistratorPlugin,
        // TypeSignatureDependsOnMissingAssemblyPlugin) are in the registry —
        // both implement plain IPlugin, so GetPluginsOfType<IPlugin> must
        // return both, while a type NONE of them implement returns empty.
        // Proves the `is T` half of the predicate genuinely filters by type.
        IEnumerable<IPlugin> allPlugins = _manager.GetPluginsOfType<IPlugin>();
        allPlugins.Should().HaveCount(expected: 2);

        IEnumerable<IEncoderPlugin> encoderPlugins = _manager.GetPluginsOfType<IEncoderPlugin>();
        encoderPlugins.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPluginsOfType_DisabledPluginWithSurvivingInstance_IsExcluded()
    {
        // DisablePluginAsync disposes the instance but the LoadedPlugin record
        // (and its Instance reference) is immutable and stays in the registry —
        // this reproduces "type matches but status is no longer Active" without
        // needing a second fixture, isolating the `&& Info.Status == Active`
        // half of the predicate from the `is T` half.
        string dllPath = StageFailuresPluginDll();
        await _manager.LoadPluginAssemblyAsync(assemblyPath: dllPath);
        await _manager.DisablePluginAsync(pluginId: ServiceRegistratorPluginId);

        IEnumerable<IPlugin> allPlugins = _manager.GetPluginsOfType<IPlugin>();

        allPlugins
            .Should()
            .ContainSingle()
            .Which.Id.Should()
            .Be(expected: TypeSignatureDependsOnMissingAssemblyPluginId);
    }

    [Fact]
    public async Task DisablePluginAsync_KnownActivePlugin_TransitionsToDisabled()
    {
        string dllPath = StageFailuresPluginDll();
        await _manager.LoadPluginAssemblyAsync(assemblyPath: dllPath);

        await _manager.DisablePluginAsync(pluginId: ServiceRegistratorPluginId);

        PluginInfo? info = _manager
            .GetInstalledPlugins()
            .FirstOrDefault(predicate: p => p.Id == ServiceRegistratorPluginId);
        info.Should().NotBeNull();
        info!.Status.Should().Be(expected: PluginStatus.Disabled);
    }

    [Fact]
    public async Task UninstallPluginAsync_KnownActivePlugin_RemovesFromRegistry()
    {
        string dllPath = StageFailuresPluginDll();
        await _manager.LoadPluginAssemblyAsync(assemblyPath: dllPath);

        await _manager.UninstallPluginAsync(pluginId: ServiceRegistratorPluginId);

        _manager.GetInstalledPlugins().Should().NotContain(predicate: p => p.Id == ServiceRegistratorPluginId);
    }

    [Fact]
    public async Task LoadPluginAssemblyAsync_MultiTypeAssembly_PublishesLoadedAndErrorEvents()
    {
        string dllPath = StageFailuresPluginDll();
        List<PluginLoadedEvent> loaded = [];
        List<PluginErrorOccurredEvent> errors = [];
        _eventBus.Subscribe<PluginLoadedEvent>(
            handler: (evt, _) =>
            {
                loaded.Add(item: evt);
                return Task.CompletedTask;
            }
        );
        _eventBus.Subscribe<PluginErrorOccurredEvent>(
            handler: (evt, _) =>
            {
                errors.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await _manager.LoadPluginAssemblyAsync(assemblyPath: dllPath);

        loaded
            .Should()
            .Contain(predicate: e => e.PluginId == ServiceRegistratorPluginId.ToString())
            .And.Contain(predicate: e =>
                e.PluginId == TypeSignatureDependsOnMissingAssemblyPluginId.ToString()
            );
        errors
            .Should()
            .Contain(predicate: e => e.PluginId == InitializeThrowsPluginId.ToString())
            .And.Contain(predicate: e => e.PluginId == InitializeThrowsDisposeSucceedsPluginId.ToString())
            .And.Contain(predicate: e => e.PluginId == Guid.Empty.ToString());
    }

    [Fact]
    public async Task LoadPluginAssemblyAsync_HealthyRegistratorPlugin_IsDiscoverableViaGetServiceRegistrators()
    {
        string dllPath = StageFailuresPluginDll();

        await _manager.LoadPluginAssemblyAsync(assemblyPath: dllPath);

        IEnumerable<IPluginServiceRegistrator> registrators = _manager.GetServiceRegistrators();

        registrators.Should().ContainSingle();
    }

    [Fact]
    public async Task LoadPluginAssemblyAsync_HealthyRegistratorPlugin_RegisterPluginServices_InvokesIt()
    {
        // RegisterPluginServices' foreach body only runs when GetServiceRegistrators()
        // returns at least one ACTIVE registrator — this is the one path in this
        // suite that gets a real registrator instance through the full loader
        // pipeline into that method rather than calling RegisterServices directly.
        string dllPath = StageFailuresPluginDll();
        await _manager.LoadPluginAssemblyAsync(assemblyPath: dllPath);
        ServiceCollection services = new();

        services.RegisterPluginServices(pluginManager: _manager);

        services.Should().ContainSingle();
        services[index: 0]
            .ServiceType.FullName.Should()
            .Be(expected: "NoMercy.Plugin.Samples.Failures.FailuresPluginMarker");
    }

    [Fact]
    public async Task LoadPluginAssemblyAsync_NonExistentAssemblyPath_PublishesLoadContextErrorEvent()
    {
        // AssemblyDependencyResolver's constructor throws InvalidOperationException
        // for a path with nothing on disk — this exercises the loader's OWN
        // load-context-construction catch block (distinct from the later
        // "assembly failed to load" catches, which all require the load context
        // to have been constructed successfully first).
        string missingPath = Path.Combine(path1: _tempPluginsDir, path2: "totally-missing-plugin.dll");
        List<PluginErrorOccurredEvent> errors = [];
        _eventBus.Subscribe<PluginErrorOccurredEvent>(
            handler: (evt, _) =>
            {
                errors.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        Func<Task> act = () => _manager.LoadPluginAssemblyAsync(assemblyPath: missingPath);

        await act.Should().NotThrowAsync();
        errors.Should().ContainSingle();
        errors[index: 0].PluginName.Should().Be(expected: "totally-missing-plugin");
        errors[index: 0].ErrorMessage.Should().Contain(expected: "load context");
    }

    private sealed class MinimalServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
