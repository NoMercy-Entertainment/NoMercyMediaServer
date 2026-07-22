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
[Trait(name: "Category", value: "Unit")]
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
            GateAsked.Add(item: (entityType, entityId));
            return needsPalette;
        }

        public override void Dispatch(IShouldQueue job, string onQueue, int priority)
        {
            Dispatched.Add(item: ("dispatched", onQueue));
        }
    }

    [Fact]
    public void DispatchColorPaletteJob_WhenEntityAlreadyHasAPalette_QueuesNothing()
    {
        RecordingDispatcher dispatcher = new(needsPalette: false);

        dispatcher.DispatchColorPaletteJob(entityType: "person", entityId: "1445824");

        Assert.Empty(collection: dispatcher.Dispatched);
    }

    [Fact]
    public void DispatchColorPaletteJob_WhenEntityHasNoPalette_QueuesTheJob()
    {
        RecordingDispatcher dispatcher = new(needsPalette: true);

        dispatcher.DispatchColorPaletteJob(entityType: "person", entityId: "1445824");

        Assert.Single(collection: dispatcher.Dispatched);
        Assert.Equal(expected: "palette", actual: dispatcher.Dispatched[index: 0].EntityId);
    }

    [Fact]
    public void DispatchColorPaletteJob_ConsultsTheGateForTheRequestedEntity()
    {
        RecordingDispatcher dispatcher = new(needsPalette: true);

        dispatcher.DispatchColorPaletteJob(entityType: "episode", entityId: "550");

        Assert.Single(collection: dispatcher.GateAsked);
        Assert.Equal(expected: ("episode", "550"), actual: dispatcher.GateAsked[index: 0]);
    }

    [Fact]
    public void DispatchColorPaletteJob_AcrossAScanCastList_OnlyQueuesTheUnpainted()
    {
        // The shape that produced ~3.6k inserts/min on a real library: the same
        // cast dispatched again for every title they appear in.
        RecordingDispatcher painted = new(needsPalette: false);
        foreach (string id in new[] { "1", "2", "3", "4", "5" })
            painted.DispatchColorPaletteJob(entityType: "person", entityId: id);

        Assert.Empty(collection: painted.Dispatched);
        Assert.Equal(expected: 5, actual: painted.GateAsked.Count);
    }

    [Fact]
    public void NeedsPalette_ForAnUnknownEntityType_FailsOpen()
    {
        // No source resolves for a type the registry doesn't know, and the gate must
        // never be the thing that silently drops work — it defers to the job.
        JobDispatcher dispatcher = new();

        bool needs = InvokeNeedsPalette(dispatcher: dispatcher, entityType: "not-a-real-entity-type", entityId: "1");

        Assert.True(condition: needs);
    }

    private static bool InvokeNeedsPalette(
        JobDispatcher dispatcher,
        string entityType,
        string entityId
    )
    {
        System.Reflection.MethodInfo method = typeof(JobDispatcher).GetMethod(
            name: "NeedsPalette",
            bindingAttr: System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        )!;

        return (bool)method.Invoke(obj: dispatcher, parameters: [entityType, entityId])!;
    }
}
