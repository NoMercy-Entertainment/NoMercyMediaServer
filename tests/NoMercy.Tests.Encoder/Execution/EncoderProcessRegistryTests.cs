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

        Assert.Equal([1234], _registry.GetProcessIds(42));
        Assert.Contains(42, _registry.ActiveJobIds);
    }

    [Fact]
    public void Register_SamePidTwice_Idempotent()
    {
        _registry.Register(jobId: 42, processId: 1234);
        _registry.Register(jobId: 42, processId: 1234);

        Assert.Single(_registry.GetProcessIds(42));
    }

    [Fact]
    public void Register_MultiplePidsForSameJob_AllTracked()
    {
        _registry.Register(jobId: 42, processId: 1234);
        _registry.Register(jobId: 42, processId: 5678);

        IReadOnlyCollection<int> pids = _registry.GetProcessIds(42);
        Assert.Equal(2, pids.Count);
        Assert.Contains(1234, pids);
        Assert.Contains(5678, pids);
    }

    [Fact]
    public void Register_NonPositiveProcessId_Ignored()
    {
        _registry.Register(jobId: 42, processId: 0);
        _registry.Register(jobId: 42, processId: -1);

        Assert.Empty(_registry.GetProcessIds(42));
    }

    [Fact]
    public void Unregister_RemovesOnlyThatPid()
    {
        _registry.Register(jobId: 42, processId: 1234);
        _registry.Register(jobId: 42, processId: 5678);

        _registry.Unregister(42, 1234);

        IReadOnlyCollection<int> pids = _registry.GetProcessIds(42);
        Assert.Single(pids);
        Assert.Contains(5678, pids);
    }

    [Fact]
    public void Unregister_LastPid_RemovesJobFromActive()
    {
        _registry.Register(jobId: 42, processId: 1234);

        _registry.Unregister(42, 1234);

        Assert.Empty(_registry.GetProcessIds(42));
        Assert.DoesNotContain(42, _registry.ActiveJobIds);
    }

    [Fact]
    public void UnregisterJob_RemovesAllPids()
    {
        _registry.Register(jobId: 42, processId: 1234);
        _registry.Register(jobId: 42, processId: 5678);

        _registry.UnregisterJob(42);

        Assert.Empty(_registry.GetProcessIds(42));
    }

    [Fact]
    public void GetProcessIds_UnknownJob_ReturnsEmpty()
    {
        Assert.Empty(_registry.GetProcessIds(99));
    }

    [Fact]
    public void DifferentJobs_TrackedIndependently()
    {
        _registry.Register(jobId: 1, processId: 100);
        _registry.Register(jobId: 2, processId: 200);

        Assert.Equal([100], _registry.GetProcessIds(1));
        Assert.Equal([200], _registry.GetProcessIds(2));
        Assert.Equal(2, _registry.ActiveJobIds.Count);
    }

    [Fact]
    public async Task Register_ConcurrentlyFromManyTasks_DoesNotCorruptState()
    {
        // Hammer the registry from many tasks — the lock must keep the set
        // consistent without dropping entries.
        const int taskCount = 20;
        const int pidsPerTask = 50;

        Task[] tasks = Enumerable
            .Range(0, taskCount)
            .Select(taskIndex =>
                Task.Run(() =>
                {
                    for (int i = 0; i < pidsPerTask; i++)
                    {
                        int pid = taskIndex * 1000 + i + 1;
                        _registry.Register(jobId: 1, processId: pid);
                    }
                })
            )
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(taskCount * pidsPerTask, _registry.GetProcessIds(1).Count);
    }
}
