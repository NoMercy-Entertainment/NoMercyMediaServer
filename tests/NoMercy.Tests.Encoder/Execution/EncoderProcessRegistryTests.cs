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

using NoMercy.Encoder.Execution;

namespace NoMercy.Tests.Encoder.Execution;

public class EncoderProcessRegistryTests
{
    private readonly EncoderProcessRegistry _registry = new();

    [Fact]
    public void Register_UnknownJob_AddsProcessId()
    {
        _registry.Register(jobId: 42, processId: 1234);

        Assert.Equal(expected: [1234], actual: _registry.GetProcessIds(jobId: 42));
        Assert.Contains(expected: 42, collection: _registry.ActiveJobIds);
    }

    [Fact]
    public void Register_SamePidTwice_Idempotent()
    {
        _registry.Register(jobId: 42, processId: 1234);
        _registry.Register(jobId: 42, processId: 1234);

        Assert.Single(collection: _registry.GetProcessIds(jobId: 42));
    }

    [Fact]
    public void Register_MultiplePidsForSameJob_AllTracked()
    {
        _registry.Register(jobId: 42, processId: 1234);
        _registry.Register(jobId: 42, processId: 5678);

        IReadOnlyCollection<int> pids = _registry.GetProcessIds(jobId: 42);
        Assert.Equal(expected: 2, actual: pids.Count);
        Assert.Contains(expected: 1234, collection: pids);
        Assert.Contains(expected: 5678, collection: pids);
    }

    [Fact]
    public void Register_NonPositiveProcessId_Ignored()
    {
        _registry.Register(jobId: 42, processId: 0);
        _registry.Register(jobId: 42, processId: -1);

        Assert.Empty(collection: _registry.GetProcessIds(jobId: 42));
    }

    [Fact]
    public void Unregister_RemovesOnlyThatPid()
    {
        _registry.Register(jobId: 42, processId: 1234);
        _registry.Register(jobId: 42, processId: 5678);

        _registry.Unregister(jobId: 42, processId: 1234);

        IReadOnlyCollection<int> pids = _registry.GetProcessIds(jobId: 42);
        Assert.Single(collection: pids);
        Assert.Contains(expected: 5678, collection: pids);
    }

    [Fact]
    public void Unregister_LastPid_RemovesJobFromActive()
    {
        _registry.Register(jobId: 42, processId: 1234);

        _registry.Unregister(jobId: 42, processId: 1234);

        Assert.Empty(collection: _registry.GetProcessIds(jobId: 42));
        Assert.DoesNotContain(expected: 42, collection: _registry.ActiveJobIds);
    }

    [Fact]
    public void UnregisterJob_RemovesAllPids()
    {
        _registry.Register(jobId: 42, processId: 1234);
        _registry.Register(jobId: 42, processId: 5678);

        _registry.UnregisterJob(jobId: 42);

        Assert.Empty(collection: _registry.GetProcessIds(jobId: 42));
    }

    [Fact]
    public void GetProcessIds_UnknownJob_ReturnsEmpty()
    {
        Assert.Empty(collection: _registry.GetProcessIds(jobId: 99));
    }

    [Fact]
    public void DifferentJobs_TrackedIndependently()
    {
        _registry.Register(jobId: 1, processId: 100);
        _registry.Register(jobId: 2, processId: 200);

        Assert.Equal(expected: [100], actual: _registry.GetProcessIds(jobId: 1));
        Assert.Equal(expected: [200], actual: _registry.GetProcessIds(jobId: 2));
        Assert.Equal(expected: 2, actual: _registry.ActiveJobIds.Count);
    }

    [Fact]
    public async Task Register_ConcurrentlyFromManyTasks_DoesNotCorruptState()
    {
        // Hammer the registry from many tasks — the lock must keep the set
        // consistent without dropping entries.
        const int taskCount = 20;
        const int pidsPerTask = 50;

        Task[] tasks = Enumerable
            .Range(start: 0, count: taskCount)
            .Select(selector: taskIndex =>
                Task.Run(action: () =>
                {
                    for (int i = 0; i < pidsPerTask; i++)
                    {
                        int pid = taskIndex * 1000 + i + 1;
                        _registry.Register(jobId: 1, processId: pid);
                    }
                })
            )
            .ToArray();

        await Task.WhenAll(tasks: tasks);

        Assert.Equal(expected: taskCount * pidsPerTask, actual: _registry.GetProcessIds(jobId: 1).Count);
    }
}
