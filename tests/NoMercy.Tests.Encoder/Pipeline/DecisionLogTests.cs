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

using NoMercy.Encoder.Pipeline;

namespace NoMercy.Tests.Encoder.Pipeline;

public class ScopedDecisionLogTests
{
    [Fact]
    public void Snapshot_returns_entries_in_insertion_order()
    {
        ScopedDecisionLog log = new();
        log.Add(entry: new(Stage: "analyze", Key: "analyze.dv_present", Message: "msg-1"));
        log.Add(
            entry: new(
                Stage: "plan",
                Key: "plan.encoder_resolved",
                Message: "msg-2",
                Data: new { encoder = "h264_nvenc" }
            )
        );

        IReadOnlyList<DecisionLog> snapshot = log.Snapshot();

        snapshot.Should().HaveCount(expected: 2);
        snapshot[index: 0].Stage.Should().Be(expected: "analyze");
        snapshot[index: 0].Key.Should().Be(expected: "analyze.dv_present");
        snapshot[index: 1].Stage.Should().Be(expected: "plan");
        snapshot[index: 1].Data.Should().NotBeNull();
    }

    [Fact]
    public void Snapshot_returns_independent_copy_safe_to_iterate_during_concurrent_add()
    {
        ScopedDecisionLog log = new();
        log.Add(entry: new(Stage: "analyze", Key: "analyze.first", Message: "first"));

        IReadOnlyList<DecisionLog> snap = log.Snapshot();
        log.Add(entry: new(Stage: "analyze", Key: "analyze.second", Message: "second"));

        snap.Should().ContainSingle();
    }

    [Fact]
    public void Add_is_thread_safe()
    {
        ScopedDecisionLog log = new();

        Parallel.For(fromInclusive: 0, toExclusive: 1000, body: i => log.Add(entry: new(Stage: "test", Key: $"test.{i}", Message: $"entry-{i}")));

        log.Snapshot().Should().HaveCount(expected: 1000);
    }

    [Fact]
    public void EncodingContext_DecisionsOrNoOp_falls_back_when_null()
    {
        EncodingContext ctx = new(CorrelationId: "x");

        ctx.DecisionsOrNoOp.Should().NotBeNull();
        // No-op sink swallows entries silently.
        ctx.DecisionsOrNoOp.Add(entry: new(Stage: "test", Key: "test.x", Message: "ignored"));
        ctx.DecisionsOrNoOp.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void EncodingContext_Create_wires_a_real_scoped_sink()
    {
        EncodingContext ctx = EncodingContext.Create();

        ctx.Decisions.Should().NotBeNull();
        ctx.Decisions.Should().BeOfType<ScopedDecisionLog>();
    }
}
