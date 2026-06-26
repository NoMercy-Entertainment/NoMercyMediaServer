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

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Execution;

namespace NoMercy.Tests.Encoder.Execution;

/// <summary>
/// ProcessThrottle wraps OS-specific suspend/resume primitives (SIGSTOP/SIGCONT
/// on Unix, NtSuspendProcess on Windows) and tracks suspended PIDs in-process.
/// The OS calls can't be exercised in unit tests against a non-existent PID,
/// but the state-tracking contract IS testable and is what the BufferManager
/// path relies on to avoid double-suspend / double-resume.
/// </summary>
public class ProcessThrottleTests
{
    private const int FakePid = 999999;

    private ProcessThrottle Build() => new(NullLogger<ProcessThrottle>.Instance);

    [Fact]
    public void IsSuspended_InitiallyFalse()
    {
        ProcessThrottle throttle = Build();
        throttle.IsSuspended(12345).Should().BeFalse();
    }

    [Fact]
    public void IsSuspended_DifferentPidNotTracked_StaysFalse()
    {
        ProcessThrottle throttle = Build();
        throttle.Suspend(FakePid);

        throttle.IsSuspended(FakePid).Should().BeTrue();
        throttle.IsSuspended(FakePid + 1).Should().BeFalse();
    }

    [Fact]
    public void Suspend_MarksSuspended()
    {
        // OS-side effect can't be observed against a missing PID, but the
        // state map MUST update so a double-suspend short-circuits and a
        // matching resume can flip it back.
        ProcessThrottle throttle = Build();

        throttle.Suspend(FakePid);

        throttle.IsSuspended(FakePid).Should().BeTrue();
    }

    [Fact]
    public void Resume_AfterSuspend_ClearsState()
    {
        ProcessThrottle throttle = Build();
        throttle.Suspend(FakePid);

        throttle.Resume(FakePid);

        throttle.IsSuspended(FakePid).Should().BeFalse();
    }

    [Fact]
    public void Resume_WithoutPriorSuspend_NoOp()
    {
        // Resume on a PID we never suspended must not throw and must leave
        // state empty — the BufferManager loop can call Resume on every tick
        // without remembering whether it ever called Suspend.
        ProcessThrottle throttle = Build();

        Action act = () => throttle.Resume(FakePid);

        act.Should().NotThrow();
        throttle.IsSuspended(FakePid).Should().BeFalse();
    }

    [Fact]
    public void Suspend_Twice_StaysIdempotent()
    {
        // Double-suspend must not double-track — IsSuspended is a single
        // boolean per PID, and Resume should clear it on the first call.
        ProcessThrottle throttle = Build();

        throttle.Suspend(FakePid);
        throttle.Suspend(FakePid);
        throttle.Resume(FakePid);

        throttle.IsSuspended(FakePid).Should().BeFalse();
    }

    [Fact]
    public void MultiplePidsTrackedIndependently()
    {
        ProcessThrottle throttle = Build();

        throttle.Suspend(100);
        throttle.Suspend(200);
        throttle.Suspend(300);
        throttle.Resume(200);

        throttle.IsSuspended(100).Should().BeTrue();
        throttle.IsSuspended(200).Should().BeFalse();
        throttle.IsSuspended(300).Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentSuspendResume_LeavesConsistentState()
    {
        // ProcessThrottle is a DI singleton — multiple worker threads hit it
        // simultaneously. Without the internal lock the HashSet would corrupt
        // under contention. Pid 999999 doesn't exist, so each NtOpenProcess
        // throws + is caught — keep iterations small to bound wall time.
        ProcessThrottle throttle = Build();
        const int pidCount = 8;
        const int iterations = 50;

        Task[] tasks = Enumerable
            .Range(1, pidCount)
            .Select(p =>
                Task.Run(() =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        throttle.Suspend(p);
                        throttle.Resume(p);
                    }
                })
            )
            .ToArray();

        await Task.WhenAll(tasks);

        for (int p = 1; p <= pidCount; p++)
            throttle.IsSuspended(p).Should().BeFalse($"pid {p} must be resumed");
    }

    [Fact]
    public async Task ConcurrentDifferentPids_AllRemainSuspendedUntilResumed()
    {
        // Distinct pids in flight at once — the set must end up containing
        // exactly the suspended ones, no phantom membership from races.
        ProcessThrottle throttle = Build();
        const int pidCount = 16;

        Task[] suspendTasks = Enumerable
            .Range(1, pidCount)
            .Select(p => Task.Run(() => throttle.Suspend(p)))
            .ToArray();

        await Task.WhenAll(suspendTasks);

        for (int p = 1; p <= pidCount; p++)
            throttle.IsSuspended(p).Should().BeTrue($"pid {p} must be suspended");

        Task[] resumeTasks = Enumerable
            .Range(1, pidCount)
            .Select(p => Task.Run(() => throttle.Resume(p)))
            .ToArray();

        await Task.WhenAll(resumeTasks);

        for (int p = 1; p <= pidCount; p++)
            throttle.IsSuspended(p).Should().BeFalse($"pid {p} must be resumed");
    }
}
