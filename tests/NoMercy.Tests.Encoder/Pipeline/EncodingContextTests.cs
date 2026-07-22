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

public class EncodingContextTests
{
    [Fact]
    public void Context_HasCorrelationId()
    {
        EncodingContext ctx = EncodingContext.Create();
        ctx.CorrelationId.Should().NotBeNullOrEmpty();
        ctx.CorrelationId.Should().HaveLength(expected: 26); // ULID length
    }

    [Fact]
    public void Context_TwoInstances_HaveDifferentIds()
    {
        EncodingContext ctx1 = EncodingContext.Create();
        EncodingContext ctx2 = EncodingContext.Create();
        ctx1.CorrelationId.Should().NotBe(unexpected: ctx2.CorrelationId);
    }

    [Fact]
    public void DecisionsOrNoOp_NoDecisionsSet_ReturnsNullSink()
    {
        EncodingContext ctx = new(CorrelationId: "test-id");

        IDecisionLogSink sink = ctx.DecisionsOrNoOp;

        sink.Should().NotBeNull();
        // Null sink swallows adds — verify by calling and not throwing.
        Action act = () => sink.Add(entry: new(Stage: "plan", Key: "test.key", Message: "msg", Data: new { x = 1 }));
        act.Should().NotThrow();
    }

    [Fact]
    public void DecisionsOrNoOp_DecisionsSet_ReturnsTheProvidedSink()
    {
        ScopedDecisionLog provided = new();
        EncodingContext ctx = new(CorrelationId: "test-id", Decisions: provided);

        IDecisionLogSink sink = ctx.DecisionsOrNoOp;

        sink.Should().BeSameAs(expected: provided);
    }

    [Fact]
    public void Create_PopulatesScopedDecisionLog()
    {
        // Default-create path attaches a real ScopedDecisionLog so the
        // pipeline can capture decisions without callers passing one in.
        EncodingContext ctx = EncodingContext.Create();

        ctx.Decisions.Should().BeOfType<ScopedDecisionLog>();
        ctx.DecisionsOrNoOp.Should().BeSameAs(expected: ctx.Decisions);
    }

    [Fact]
    public void Context_DefaultProperties_AreNull()
    {
        EncodingContext ctx = new(CorrelationId: "test-id");

        ctx.MediaInfo.Should().BeNull();
        ctx.Decisions.Should().BeNull();
        ctx.SourceStorage.Should().BeNull();
        ctx.DestinationStorage.Should().BeNull();
        ctx.MediaItem.Should().BeNull();
        ctx.SourceTracks.Should().BeNull();
        ctx.DbTracks.Should().BeNull();
    }
}
