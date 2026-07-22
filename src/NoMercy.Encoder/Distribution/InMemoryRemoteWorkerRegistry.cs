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

using System.Collections.Concurrent;
using NoMercy.Encoder.Jobs;

namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Thread-safe registry populated at runtime by the worker-registration
/// API. Workers self-register after booting, send periodic heartbeats to
/// stay "active", and drop off when they miss two consecutive intervals.
/// The dispatcher reads a snapshot of currently-active workers on every
/// dispatch — no subscription / streaming, so registration churn doesn't
/// affect mid-job dispatches.
///
/// Stale entries are pruned lazily on reads: <see cref="GetActiveWorkers"/>
/// filters out anything past the stale threshold and drops them from the
/// dictionary so stopped workers don't accumulate forever.
///
/// Health tracking: workers that fail <see cref="FailureThreshold"/> tasks
/// in a row enter a cooldown window during which they're hidden from
/// <see cref="GetActiveWorkers"/>. A single success clears the counter.
/// Prevents one bad GPU / flaky network link from soaking up every
/// assignment while slowing every user's encode.
/// </summary>
public class InMemoryRemoteWorkerRegistry : IRemoteWorkerRegistry
{
    private const int FailureThreshold = 3;

    private readonly ConcurrentDictionary<string, RegisteredWorker> _workers = new();
    private readonly TimeSpan _staleAfter;
    private readonly TimeSpan _cooldownDuration;
    private readonly Func<DateTime> _clock;

    public InMemoryRemoteWorkerRegistry(
        TimeSpan? staleAfter = null,
        TimeSpan? cooldownDuration = null,
        Func<DateTime>? clock = null
    )
    {
        _staleAfter = staleAfter ?? TimeSpan.FromSeconds(seconds: 60);
        _cooldownDuration = cooldownDuration ?? TimeSpan.FromMinutes(minutes: 2);
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Adds a new worker or refreshes an existing one. Idempotent — workers
    /// that re-register after a restart replace their own entry. Re-register
    /// also clears any outstanding cooldown since the worker is explicitly
    /// announcing it's healthy again.
    /// </summary>
    public void Register(IRemoteWorker worker)
    {
        _workers[key: worker.WorkerId] = new(
            Worker: worker,
            LastSeenUtc: _clock(),
            ConsecutiveFailures: 0,
            CooldownUntilUtc: null
        );
    }

    /// <summary>
    /// Called by the dispatcher after each task attempt. A successful task
    /// resets the failure counter; enough failures push the worker into
    /// cooldown where it's hidden from <see cref="GetActiveWorkers"/>
    /// until the cooldown window elapses.
    /// </summary>
    public void RecordTaskOutcome(string workerId, bool success)
    {
        if (!_workers.TryGetValue(key: workerId, value: out RegisteredWorker? existing))
            return;

        if (success)
        {
            if (existing is { ConsecutiveFailures: 0, CooldownUntilUtc: null })
                return; // Already healthy — avoid needless dictionary write.

            _workers[key: workerId] = existing with { ConsecutiveFailures = 0, CooldownUntilUtc = null };
            return;
        }

        int failures = existing.ConsecutiveFailures + 1;
        DateTime? cooldown =
            failures >= FailureThreshold ? _clock() + _cooldownDuration : existing.CooldownUntilUtc;

        _workers[key: workerId] = existing with
        {
            ConsecutiveFailures = failures,
            CooldownUntilUtc = cooldown,
        };
    }

    /// <summary>
    /// Marks the worker as still alive. Returns false if the worker isn't
    /// registered (caller should treat as "re-register required").
    /// </summary>
    public bool Heartbeat(string workerId)
    {
        if (!_workers.TryGetValue(key: workerId, value: out RegisteredWorker? existing))
            return false;

        _workers[key: workerId] = existing with { LastSeenUtc = _clock() };
        return true;
    }

    /// <summary>
    /// Removes a worker explicitly — called when a worker shuts down
    /// cleanly so the dispatcher stops trying to reach it.
    /// </summary>
    public bool Unregister(string workerId) => _workers.TryRemove(key: workerId, value: out _);

    public IReadOnlyList<IRemoteWorker> GetActiveWorkers()
    {
        DateTime now = _clock();
        DateTime cutoff = now - _staleAfter;

        // Evict stale entries lazily — keeps the dictionary from growing
        // unbounded when workers come and go.
        foreach (
            KeyValuePair<string, RegisteredWorker> kvp in _workers
                .Where(predicate: kvp => kvp.Value.LastSeenUtc < cutoff)
                .ToArray()
        )
        {
            _workers.TryRemove(key: kvp.Key, value: out _);
        }

        // Hide workers still in their cooldown window. They stay registered
        // (so the operator sees them in /workers with a cooldown status),
        // but the dispatcher doesn't pick them until the window elapses.
        // Cooldown lifts automatically on the next read after the deadline.
        return _workers
            .Values.Where(predicate: rw => !IsInCooldown(rw: rw, now: now))
            .Select(selector: rw => rw.Worker)
            .ToArray();
    }

    /// <summary>
    /// Diagnostic accessor — returns every registered worker regardless
    /// of cooldown state, along with the health counters. Used by the
    /// /workers list endpoint so operators can see which workers are
    /// currently benched.
    /// </summary>
    public IReadOnlyList<WorkerHealthSnapshot> GetAllWorkersWithHealth()
    {
        DateTime now = _clock();
        return _workers
            .Values.Select(selector: rw => new WorkerHealthSnapshot(
                Worker: rw.Worker,
                LastSeenUtc: rw.LastSeenUtc,
                ConsecutiveFailures: rw.ConsecutiveFailures,
                CooldownUntilUtc: IsInCooldown(rw: rw, now: now) ? rw.CooldownUntilUtc : null
            ))
            .ToArray();
    }

    private static bool IsInCooldown(RegisteredWorker rw, DateTime now) =>
        rw.CooldownUntilUtc is DateTime until && now < until;

    private sealed record RegisteredWorker(
        IRemoteWorker Worker,
        DateTime LastSeenUtc,
        int ConsecutiveFailures,
        DateTime? CooldownUntilUtc
    );
}

/// <summary>
/// Snapshot of a worker's health for the dashboard. CooldownUntilUtc is
/// non-null only when the worker is currently benched.
/// </summary>
public record WorkerHealthSnapshot(
    IRemoteWorker Worker,
    DateTime LastSeenUtc,
    int ConsecutiveFailures,
    DateTime? CooldownUntilUtc
);
