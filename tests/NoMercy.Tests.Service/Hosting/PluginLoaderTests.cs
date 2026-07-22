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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Hooks;
using NoMercy.Service.Hosting;
using Xunit;

namespace NoMercy.Tests.Service.Hosting;

/// <summary>
/// <see cref="PluginLoader"/> is the boot step that scans the plugins directory
/// and registers every plugin's cron jobs. A missing plugins directory must
/// return an empty list rather than throw (documented on <see cref="IPluginLoader.LoadPlugins"/>),
/// and cron registration must always run — even when zero plugins loaded — so a
/// plugin that only contributes a cron job without needing to "load" anything
/// still gets wired up.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class PluginLoaderTests
{
    private static PluginLoadResult FakeResult(string name, string version) =>
        new(PluginId: Guid.NewGuid(), Name: name, Version: version, Instance: Mock.Of<IPlugin>());

    [Fact]
    public async Task LoadPlugins_ReturnsWhatPluginManagerLoads()
    {
        List<PluginLoadResult> expected = [FakeResult(name: "Echo", version: "1.0.0")];
        Mock<IPluginManager> pluginManager = new();
        pluginManager
            .Setup(expression: m => m.LoadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: expected);
        Mock<IPluginCronRegistrar> cronRegistrar = new();

        PluginLoader loader = new(
            logger: NullLogger<PluginLoader>.Instance,
            pluginManager: pluginManager.Object,
            cronRegistrar: cronRegistrar.Object
        );

        IReadOnlyList<PluginLoadResult> result = await loader.LoadPlugins(ct: CancellationToken.None);

        result.Should().BeEquivalentTo(expectation: expected);
    }

    [Fact]
    public async Task LoadPlugins_NoPluginsFound_ReturnsEmptyListWithoutThrowing()
    {
        Mock<IPluginManager> pluginManager = new();
        pluginManager.Setup(expression: m => m.LoadAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(value: []);
        Mock<IPluginCronRegistrar> cronRegistrar = new();

        PluginLoader loader = new(
            logger: NullLogger<PluginLoader>.Instance,
            pluginManager: pluginManager.Object,
            cronRegistrar: cronRegistrar.Object
        );

        IReadOnlyList<PluginLoadResult> result = await loader.LoadPlugins(ct: CancellationToken.None);

        result.Should().BeEmpty();
        cronRegistrar.Verify(expression: r => r.RegisterAll(), times: Times.Once);
    }

    [Fact]
    public async Task LoadPlugins_AlwaysRegistersCronJobsAfterLoading()
    {
        Mock<IPluginManager> pluginManager = new();
        pluginManager
            .Setup(expression: m => m.LoadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: [FakeResult(name: "Sample", version: "2.0.0")]);
        Mock<IPluginCronRegistrar> cronRegistrar = new();

        PluginLoader loader = new(
            logger: NullLogger<PluginLoader>.Instance,
            pluginManager: pluginManager.Object,
            cronRegistrar: cronRegistrar.Object
        );
        await loader.LoadPlugins(ct: CancellationToken.None);

        cronRegistrar.Verify(expression: r => r.RegisterAll(), times: Times.Once);
    }

    [Fact]
    public async Task LoadPlugins_PassesCancellationTokenThrough()
    {
        Mock<IPluginManager> pluginManager = new();
        pluginManager.Setup(expression: m => m.LoadAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(value: []);
        Mock<IPluginCronRegistrar> cronRegistrar = new();
        PluginLoader loader = new(
            logger: NullLogger<PluginLoader>.Instance,
            pluginManager: pluginManager.Object,
            cronRegistrar: cronRegistrar.Object
        );
        using CancellationTokenSource cts = new();

        await loader.LoadPlugins(ct: cts.Token);

        pluginManager.Verify(expression: m => m.LoadAllAsync(cts.Token), times: Times.Once);
    }
}
