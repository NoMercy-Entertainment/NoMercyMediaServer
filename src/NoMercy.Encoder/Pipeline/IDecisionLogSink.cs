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

namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// Sink for <see cref="DecisionLog"/> entries emitted during pipeline
/// execution. Stages append to the sink; the orchestrator collects the
/// final list at the end and surfaces it on the encode result so the
/// dashboard can render the trace.
///
/// <para>Implementations must be thread-safe — multiple stages may
/// run concurrently in future phases (e.g. parallel two-pass + crop
/// detection).</para>
/// </summary>
public interface IDecisionLogSink
{
    /// <summary>Append a single decision entry.</summary>
    void Add(DecisionLog entry);

    /// <summary>Snapshot of every entry recorded so far.</summary>
    IReadOnlyList<DecisionLog> Snapshot();
}

/// <summary>
/// Default in-memory <see cref="IDecisionLogSink"/> scoped to a single
/// encode. Created per-job by the orchestrator and threaded through
/// <see cref="EncodingContext"/>.
/// </summary>
public sealed class ScopedDecisionLog : IDecisionLogSink
{
    private readonly object _gate = new();
    private readonly List<DecisionLog> _entries = [];

    public void Add(DecisionLog entry)
    {
        ArgumentNullException.ThrowIfNull(argument: entry);
        lock (_gate)
            _entries.Add(item: entry);
    }

    public IReadOnlyList<DecisionLog> Snapshot()
    {
        lock (_gate)
            return _entries.ToArray();
    }
}

/// <summary>
/// No-op sink used when callers don't care about decisions (e.g.
/// existing call sites that haven't been migrated yet, or unit tests
/// that don't assert on the log). Default value of
/// <see cref="EncodingContext.Decisions"/>.
/// </summary>
internal sealed class NullDecisionLogSink : IDecisionLogSink
{
    public static readonly NullDecisionLogSink Instance = new();

    public void Add(DecisionLog entry) { }

    public IReadOnlyList<DecisionLog> Snapshot() => [];
}
