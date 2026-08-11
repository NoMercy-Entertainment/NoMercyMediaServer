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

using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Hooks;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Workers;
using Xunit;

namespace NoMercy.Tests.Plugins;

/// <summary>
/// Drives <see cref="PluginCronRegistrar"/> against a REAL <see cref="CronWorker"/>
/// (mirroring the pattern already used by
/// NoMercy.Tests.Queue.CronWorkerRegisterExecutorTests) rather than a fake — the
/// requirement under test is that a declared IScheduledTaskPlugin actually reaches
/// CronWorker's own job registry, not merely that some method was called on a
/// test double. IQueueContext is mocked with Moq because RegisterExecutor never
/// touches it; CronWorker still requires a non-null instance to construct.
/// </summary>
public class PluginCronRegistrarTests
{
    private static List<CronJobModel> GetCodeDefinedJobs(CronWorker cronWorker)
    {
        FieldInfo field = typeof(CronWorker).GetField(
            "_codeDefinedJobs",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!;

        return (List<CronJobModel>)field.GetValue(cronWorker)!;
    }

    private static CronWorker BuildCronWorker()
    {
        ServiceCollection services = new();
        services.AddLogging();
        ServiceProvider provider = services.BuildServiceProvider();

        return new(
            provider,
            provider.GetRequiredService<ILogger<CronWorker>>(),
            Mock.Of<IQueueContext>()
        );
    }

    [Fact]
    public async Task RegisterAll_PluginDeclaringScheduledTaskHook_RegistersItOnCronWorker()
    {
        FakeScheduledTaskPlugin plugin = new("*/5 * * * *");
        FakePluginManager manager = FakePluginManager.WithScheduledTask(plugin, declaresHook: true);
        CronWorker cronWorker = BuildCronWorker();
        PluginCronRegistrar registrar = new(manager, cronWorker);

        registrar.RegisterAll();

        // PluginCronExecutor.JobName (and therefore CronJobModel.JobType, which
        // RegisterExecutor sets from it) is "plugin:{plugin.Id}" — the registrar
        // itself never touches JobType, so this also pins that the executor
        // adapter's naming convention reached CronWorker unchanged.
        GetCodeDefinedJobs(cronWorker)
            .Should()
            .ContainSingle(job => job.JobType == $"plugin:{plugin.Id}");

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await cronWorker.StopAsync(cts.Token);
    }

    [Fact]
    public async Task RegisterAll_PluginWithoutScheduledTaskHook_IsSkipped()
    {
        FakeScheduledTaskPlugin plugin = new("0 * * * *");
        FakePluginManager manager = FakePluginManager.WithScheduledTask(
            plugin,
            declaresHook: false
        );
        CronWorker cronWorker = BuildCronWorker();
        PluginCronRegistrar registrar = new(manager, cronWorker);

        registrar.RegisterAll();

        GetCodeDefinedJobs(cronWorker).Should().BeEmpty();

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await cronWorker.StopAsync(cts.Token);
    }

    [Fact]
    public async Task RegisterAll_PluginMissingFromInstalledList_IsIgnored()
    {
        // GetPluginsOfType and GetInstalledPlugins are two independent reads —
        // a plugin returned by the former with no matching entry in the latter
        // must fall back to null capabilities (`?.Capabilities`) rather than
        // throw, and null capabilities never declares the scheduledTask hook.
        FakeScheduledTaskPlugin plugin = new("*/10 * * * *");
        FakePluginManager manager = FakePluginManager.WithScheduledTaskNotInInstalledList(plugin);
        CronWorker cronWorker = BuildCronWorker();
        PluginCronRegistrar registrar = new(manager, cronWorker);

        Action act = () => registrar.RegisterAll();

        act.Should().NotThrow();
        GetCodeDefinedJobs(cronWorker).Should().BeEmpty();

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await cronWorker.StopAsync(cts.Token);
    }

    [Fact]
    public async Task RegisterAll_NoScheduledTaskPlugins_DoesNothing()
    {
        FakePluginManager manager = FakePluginManager.Empty();
        CronWorker cronWorker = BuildCronWorker();
        PluginCronRegistrar registrar = new(manager, cronWorker);

        Action act = () => registrar.RegisterAll();

        act.Should().NotThrow();
        GetCodeDefinedJobs(cronWorker).Should().BeEmpty();

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await cronWorker.StopAsync(cts.Token);
    }

    private sealed class FakeScheduledTaskPlugin(string cronExpression) : IScheduledTaskPlugin
    {
        public string Name => "fake-scheduled";
        public string Description => "d";
        public Ulid Id { get; } = Ulid.NewUlid();
        public Version Version { get; } = new(1, 0);
        public string CronExpression => cronExpression;

        public void Initialize(IPluginContext context) { }

        public void Dispose() { }

        public Task ExecuteAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>
    /// The bug this exists for: start-up's one registration pass runs before a slow
    /// server has finished loading its plugins, so a plugin that turns up afterwards
    /// had no cron executors at all - pages rendering, endpoints answering, nothing
    /// ever ticking.
    /// </summary>
    [Fact]
    public async Task RegisterPlugin_APluginThatMissedStartupsPass_StillGetsItsJobs()
    {
        FakeScheduledTaskPlugin plugin = new("*/5 * * * *");
        FakePluginManager manager = FakePluginManager.Empty();
        CronWorker cronWorker = BuildCronWorker();
        PluginCronRegistrar registrar = new(manager, cronWorker);

        // The pass start-up makes, against a manager that knows of nothing yet.
        registrar.RegisterAll();
        GetCodeDefinedJobs(cronWorker).Should().BeEmpty();

        manager.Add(plugin, declaresHook: true);
        registrar.RegisterPlugin(plugin.Id);

        GetCodeDefinedJobs(cronWorker)
            .Should()
            .ContainSingle(job => job.JobType == $"plugin:{plugin.Id}");

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await cronWorker.StopAsync(cts.Token);
    }

    /// <summary>
    /// Both registration paths still run, so a plugin present at start-up is registered
    /// once by the pass and again by the event. Two entries would be one schedule running
    /// its work twice, forever.
    /// </summary>
    [Fact]
    public async Task RegisterPlugin_AlreadyRegistered_ReplacesRatherThanAddsASecondSet()
    {
        FakeScheduledTaskPlugin plugin = new("*/5 * * * *");
        FakePluginManager manager = FakePluginManager.WithScheduledTask(plugin, declaresHook: true);
        CronWorker cronWorker = BuildCronWorker();
        PluginCronRegistrar registrar = new(manager, cronWorker);

        registrar.RegisterAll();
        registrar.RegisterPlugin(plugin.Id);
        registrar.RegisterPlugin(plugin.Id);

        GetCodeDefinedJobs(cronWorker)
            .Should()
            .ContainSingle(job => job.JobType == $"plugin:{plugin.Id}");

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await cronWorker.StopAsync(cts.Token);
    }

    [Fact]
    public async Task RegisterPlugin_APluginNotDeclaringTheHook_IsStillLeftAlone()
    {
        FakeScheduledTaskPlugin plugin = new("*/5 * * * *");
        FakePluginManager manager = FakePluginManager.WithScheduledTask(plugin, declaresHook: false);
        CronWorker cronWorker = BuildCronWorker();

        new PluginCronRegistrar(manager, cronWorker).RegisterPlugin(plugin.Id);

        GetCodeDefinedJobs(cronWorker).Should().BeEmpty();

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await cronWorker.StopAsync(cts.Token);
    }

    [Fact]
    public async Task RegisterPlugin_AnIdNoPluginHas_DoesNothing()
    {
        FakeScheduledTaskPlugin plugin = new("*/5 * * * *");
        FakePluginManager manager = FakePluginManager.WithScheduledTask(plugin, declaresHook: true);
        CronWorker cronWorker = BuildCronWorker();

        new PluginCronRegistrar(manager, cronWorker).RegisterPlugin(Ulid.NewUlid());

        GetCodeDefinedJobs(cronWorker).Should().BeEmpty();

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await cronWorker.StopAsync(cts.Token);
    }

    private sealed class FakePluginManager : IPluginManager
    {
        private readonly List<IScheduledTaskPlugin> _plugins = [];
        private readonly Dictionary<Ulid, PluginCapabilities?> _capabilities = [];

        public static FakePluginManager Empty() => new();

        /// <summary>A plugin the manager only learns about after start-up has been and gone.</summary>
        public void Add(IScheduledTaskPlugin plugin, bool declaresHook)
        {
            _plugins.Add(plugin);
            _capabilities[plugin.Id] = declaresHook
                ? new() { Hooks = [PluginHookCapability.ScheduledTask] }
                : new() { Hooks = [] };
        }

        public static FakePluginManager WithScheduledTask(
            IScheduledTaskPlugin plugin,
            bool declaresHook
        )
        {
            FakePluginManager manager = new();
            manager._plugins.Add(plugin);
            manager._capabilities[plugin.Id] = declaresHook
                ? new() { Hooks = [PluginHookCapability.ScheduledTask] }
                : new() { Hooks = [] };
            return manager;
        }

        // Adds the plugin to _plugins (so GetPluginsOfType returns it) WITHOUT a
        // corresponding _capabilities entry, so GetInstalledPlugins omits it.
        public static FakePluginManager WithScheduledTaskNotInInstalledList(
            IScheduledTaskPlugin plugin
        )
        {
            FakePluginManager manager = new();
            manager._plugins.Add(plugin);
            return manager;
        }

        public IReadOnlyList<PluginInfo> GetInstalledPlugins() =>
            _plugins
                .Where(plugin => _capabilities.ContainsKey(plugin.Id))
                .Select(plugin => new PluginInfo
                {
                    Id = plugin.Id,
                    Name = plugin.Name,
                    Description = plugin.Description,
                    Version = plugin.Version,
                    Status = PluginStatus.Active,
                    Capabilities = _capabilities[plugin.Id],
                })
                .ToList();

        public IEnumerable<T> GetPluginsOfType<T>()
            where T : IPlugin => _plugins.OfType<T>();

        public Task InstallPluginAsync(string packageUrl, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task EnablePluginAsync(Ulid pluginId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task DisablePluginAsync(Ulid pluginId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task UninstallPluginAsync(Ulid pluginId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<PluginLoadResult>> LoadAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PluginLoadResult>>([]);
    }
}
