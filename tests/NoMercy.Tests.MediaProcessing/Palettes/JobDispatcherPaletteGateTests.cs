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

using NoMercy.MediaProcessing.Jobs;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Tests.MediaProcessing.Palettes;

/// <summary>
/// A library scan dispatches one palette job per cast member per stored title, and
/// people are shared across titles, so nearly every one targets an already-painted
/// entity. ColorPaletteJob no-ops on those, so queueing them only burns queue
/// throughput. These pin the gate that keeps them out of the queue.
/// </summary>
[Trait("Category", "Unit")]
public class JobDispatcherPaletteGateTests
{
    /// <summary>
    /// Captures what reached the queue and lets each test state whether the entity
    /// already has a palette, without touching a database.
    /// </summary>
    private sealed class RecordingDispatcher(bool needsPalette) : JobDispatcher
    {
        public List<(string EntityType, string EntityId)> Dispatched { get; } = [];
        public List<(string EntityType, string EntityId)> GateAsked { get; } = [];

        protected override bool NeedsPalette(string entityType, string entityId)
        {
            GateAsked.Add((entityType, entityId));
            return needsPalette;
        }

        public override void Dispatch(IShouldQueue job, string onQueue, int priority)
        {
            Dispatched.Add(("dispatched", onQueue));
        }
    }

    [Fact]
    public void DispatchColorPaletteJob_WhenEntityAlreadyHasAPalette_QueuesNothing()
    {
        RecordingDispatcher dispatcher = new(false);

        dispatcher.DispatchColorPaletteJob("person", "1445824");

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void DispatchColorPaletteJob_WhenEntityHasNoPalette_QueuesTheJob()
    {
        RecordingDispatcher dispatcher = new(true);

        dispatcher.DispatchColorPaletteJob("person", "1445824");

        Assert.Single(dispatcher.Dispatched);
        Assert.Equal("palette", dispatcher.Dispatched[0].EntityId);
    }

    [Fact]
    public void DispatchColorPaletteJob_ConsultsTheGateForTheRequestedEntity()
    {
        RecordingDispatcher dispatcher = new(true);

        dispatcher.DispatchColorPaletteJob("episode", "550");

        Assert.Single(dispatcher.GateAsked);
        Assert.Equal(("episode", "550"), dispatcher.GateAsked[0]);
    }

    [Fact]
    public void DispatchColorPaletteJob_AcrossAScanCastList_OnlyQueuesTheUnpainted()
    {
        // The shape that produced ~3.6k inserts/min on a real library: the same
        // cast dispatched again for every title they appear in.
        RecordingDispatcher painted = new(false);
        foreach (string id in new[] { "1", "2", "3", "4", "5" })
            painted.DispatchColorPaletteJob("person", id);

        Assert.Empty(painted.Dispatched);
        Assert.Equal(5, painted.GateAsked.Count);
    }

    [Fact]
    public void NeedsPalette_ForAnUnknownEntityType_FailsOpen()
    {
        // No source resolves for a type the registry doesn't know, and the gate must
        // never be the thing that silently drops work — it defers to the job.
        JobDispatcher dispatcher = new();

        bool needs = InvokeNeedsPalette(dispatcher, "not-a-real-entity-type", "1");

        Assert.True(needs);
    }

    private static bool InvokeNeedsPalette(
        JobDispatcher dispatcher,
        string entityType,
        string entityId
    )
    {
        System.Reflection.MethodInfo method = typeof(JobDispatcher).GetMethod(
            "NeedsPalette",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        )!;

        return (bool)method.Invoke(dispatcher, [entityType, entityId])!;
    }
}
