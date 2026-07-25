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
        log.Add(new("analyze", "analyze.dv_present", "msg-1"));
        log.Add(
            new(
                "plan",
                "plan.encoder_resolved",
                "msg-2",
                new { encoder = "h264_nvenc" }
            )
        );

        IReadOnlyList<DecisionLog> snapshot = log.Snapshot();

        snapshot.Should().HaveCount(2);
        snapshot[0].Stage.Should().Be("analyze");
        snapshot[0].Key.Should().Be("analyze.dv_present");
        snapshot[1].Stage.Should().Be("plan");
        snapshot[1].Data.Should().NotBeNull();
    }

    [Fact]
    public void Snapshot_returns_independent_copy_safe_to_iterate_during_concurrent_add()
    {
        ScopedDecisionLog log = new();
        log.Add(new("analyze", "analyze.first", "first"));

        IReadOnlyList<DecisionLog> snap = log.Snapshot();
        log.Add(new("analyze", "analyze.second", "second"));

        snap.Should().ContainSingle();
    }

    [Fact]
    public void Add_is_thread_safe()
    {
        ScopedDecisionLog log = new();

        Parallel.For(0, 1000, i => log.Add(new("test", $"test.{i}", $"entry-{i}")));

        log.Snapshot().Should().HaveCount(1000);
    }

    [Fact]
    public void EncodingContext_DecisionsOrNoOp_falls_back_when_null()
    {
        EncodingContext ctx = new("x");

        ctx.DecisionsOrNoOp.Should().NotBeNull();
        // No-op sink swallows entries silently.
        ctx.DecisionsOrNoOp.Add(new("test", "test.x", "ignored"));
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
