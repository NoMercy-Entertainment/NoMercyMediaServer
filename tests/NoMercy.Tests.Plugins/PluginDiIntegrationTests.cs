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
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginDiIntegrationTests : IDisposable
{
    private readonly string _tempPluginsDir;

    public PluginDiIntegrationTests()
    {
        _tempPluginsDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nomercy-di-tests-" + Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: _tempPluginsDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(path: _tempPluginsDir))
            {
                Directory.Delete(path: _tempPluginsDir, recursive: true);
            }
        }
        catch (IOException) { }
    }

    [Fact]
    public void AddPluginSystem_RegistersPluginManagerAsSingleton()
    {
        ServiceCollection services = new();
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddLogging();
        services.AddSingleton(implementationInstance: TestStorageHelper.CreateBackend());

        services.AddPluginSystem(pluginsPath: _tempPluginsDir);

        ServiceProvider provider = services.BuildServiceProvider();
        IPluginManager manager1 = provider.GetRequiredService<IPluginManager>();
        IPluginManager manager2 = provider.GetRequiredService<IPluginManager>();

        manager1.Should().NotBeNull();
        manager1.Should().BeSameAs(expected: manager2);
        manager1.Should().BeOfType<PluginManager>();

        (manager1 as IDisposable)?.Dispose();
    }

    [Fact]
    public void AddPluginSystem_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;

        Action act = () => services!.AddPluginSystem(pluginsPath: "/tmp");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddPluginSystem_NullPath_ThrowsArgumentException()
    {
        ServiceCollection services = new();

        Action act = () => services.AddPluginSystem(pluginsPath: null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddPluginSystem_EmptyPath_ThrowsArgumentException()
    {
        ServiceCollection services = new();

        Action act = () => services.AddPluginSystem(pluginsPath: "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterPluginServices_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;
        InMemoryEventBus bus = new();
        PluginManager manager = new(
            eventBus: bus,
            serviceProvider: new MinimalServiceProvider(),
            logger: NullLogger<PluginManager>.Instance,
            pluginsPath: _tempPluginsDir,
            storage: TestStorageHelper.CreateStorage(rootPath: _tempPluginsDir),
            driver: TestStorageHelper.CreateBackend()
        );

        Action act = () => services!.RegisterPluginServices(pluginManager: manager);

        act.Should().Throw<ArgumentNullException>();
        manager.Dispose();
    }

    [Fact]
    public void RegisterPluginServices_NullManager_ThrowsArgumentNullException()
    {
        ServiceCollection services = new();

        Action act = () => services.RegisterPluginServices(pluginManager: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterPluginServices_NoPlugins_DoesNothing()
    {
        ServiceCollection services = new();
        InMemoryEventBus bus = new();
        PluginManager manager = new(
            eventBus: bus,
            serviceProvider: new MinimalServiceProvider(),
            logger: NullLogger<PluginManager>.Instance,
            pluginsPath: _tempPluginsDir,
            storage: TestStorageHelper.CreateStorage(rootPath: _tempPluginsDir),
            driver: TestStorageHelper.CreateBackend()
        );

        services.RegisterPluginServices(pluginManager: manager);

        services.Should().BeEmpty();
        manager.Dispose();
    }

    [Fact]
    public void GetServiceRegistrators_NoPlugins_ReturnsEmpty()
    {
        InMemoryEventBus bus = new();
        PluginManager manager = new(
            eventBus: bus,
            serviceProvider: new MinimalServiceProvider(),
            logger: NullLogger<PluginManager>.Instance,
            pluginsPath: _tempPluginsDir,
            storage: TestStorageHelper.CreateStorage(rootPath: _tempPluginsDir),
            driver: TestStorageHelper.CreateBackend()
        );

        IEnumerable<IPluginServiceRegistrator> registrators = manager.GetServiceRegistrators();

        registrators.Should().BeEmpty();
        manager.Dispose();
    }

    [Fact]
    public void AddPluginSystem_ReturnsServiceCollectionForChaining()
    {
        ServiceCollection services = new();
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddLogging();

        IServiceCollection result = services.AddPluginSystem(pluginsPath: _tempPluginsDir);

        result.Should().BeSameAs(expected: services);
    }

    [Fact]
    public void AddPluginSystem_ManagerGetsCorrectDependencies()
    {
        ServiceCollection services = new();
        InMemoryEventBus bus = new();
        services.AddSingleton<IEventBus>(implementationInstance: bus);
        services.AddLogging();
        services.AddSingleton(implementationInstance: TestStorageHelper.CreateBackend());

        services.AddPluginSystem(pluginsPath: _tempPluginsDir);

        ServiceProvider provider = services.BuildServiceProvider();
        IPluginManager manager = provider.GetRequiredService<IPluginManager>();

        manager.Should().NotBeNull();
        manager.GetInstalledPlugins().Should().BeEmpty();

        (manager as IDisposable)?.Dispose();
    }

    [Fact]
    public void IPluginServiceRegistrator_CanRegisterServices()
    {
        TestServiceRegistrator registrator = new();
        ServiceCollection services = new();

        registrator.RegisterServices(services: services);

        services.Should().ContainSingle();
        ServiceProvider provider = services.BuildServiceProvider();
        ITestService service = provider.GetRequiredService<ITestService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<TestService>();
    }

    public interface ITestService
    {
        string GetValue();
    }

    private sealed class TestService : ITestService
    {
        public string GetValue() => "from-plugin";
    }

    private sealed class TestServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<ITestService, TestService>();
        }
    }

    private sealed class MinimalServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
