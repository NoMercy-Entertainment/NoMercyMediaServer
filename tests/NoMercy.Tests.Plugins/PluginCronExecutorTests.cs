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

using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Hooks;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginCronExecutorTests
{
    private sealed class FakeTask : IScheduledTaskPlugin
    {
        public bool Ran;

        public string Name => "t";
        public string Description => "d";
        public Guid Id { get; } = Guid.Parse(input: "11111111-1111-1111-1111-111111111111");
        public Version Version { get; } = new(major: 1, minor: 0);
        public string CronExpression => "*/5 * * * *";

        public void Initialize(IPluginContext context) { }

        public void Dispose() { }

        public Task ExecuteAsync(CancellationToken ct = default)
        {
            Ran = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Adapter_ExposesPluginScheduleAndExecutes()
    {
        FakeTask task = new();
        PluginCronExecutor executor = new(plugin: task);

        Assert.Equal(expected: "plugin:11111111-1111-1111-1111-111111111111", actual: executor.JobName);
        Assert.Equal(expected: "*/5 * * * *", actual: executor.CronExpression);

        await executor.ExecuteAsync(parameters: string.Empty, cancellationToken: CancellationToken.None);

        Assert.True(condition: task.Ran);
    }

    [Fact]
    public async Task Adapter_ForwardsCancellationToken_IgnoringParametersString()
    {
        FakeCancellationObservingTask task = new();
        PluginCronExecutor executor = new(plugin: task);
        using CancellationTokenSource cts = new();

        await executor.ExecuteAsync(parameters: "ignored-parameters", cancellationToken: cts.Token);

        Assert.Equal(expected: cts.Token, actual: task.ReceivedToken);
    }

    private sealed class FakeCancellationObservingTask : IScheduledTaskPlugin
    {
        public CancellationToken ReceivedToken;

        public string Name => "observer";
        public string Description => "d";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(major: 1, minor: 0);
        public string CronExpression => "0 * * * *";

        public void Initialize(IPluginContext context) { }

        public void Dispose() { }

        public Task ExecuteAsync(CancellationToken ct = default)
        {
            ReceivedToken = ct;
            return Task.CompletedTask;
        }
    }
}
