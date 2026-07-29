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

using System.Runtime.CompilerServices;
using FluentAssertions;
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Plugins;

/// <summary>
/// Whether a plugin can be replaced without stopping the server comes down to
/// whether its load context actually went. Anything still referencing into it
/// keeps it — and the answer the owner is given depends on looking rather than
/// on having called Unload.
/// </summary>
public class PluginAssemblyTrackerTests
{
    private static readonly Guid PluginId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [Fact]
    public void A_plugin_that_was_never_unloaded_is_not_lingering()
    {
        // Nothing was asked to unload, so nothing is hanging around. Reporting
        // "still loaded" here would make every fresh install claim it needs a
        // restart.
        PluginAssemblyTracker tracker = new();

        tracker.IsStillLoaded(PluginId).Should().BeFalse();
    }

    [Fact]
    public void A_context_nothing_references_is_reported_gone()
    {
        PluginAssemblyTracker tracker = new();
        TrackAndDrop(tracker);

        tracker.IsStillLoaded(PluginId).Should().BeFalse();
    }

    [Fact]
    public void A_context_something_still_holds_is_reported_resident()
    {
        // The case that matters: one live reference is enough, which is exactly
        // what a leftover cron executor or event subscription is.
        PluginAssemblyTracker tracker = new();
        object held = new();

        tracker.TrackUnload(PluginId, held);

        tracker.IsStillLoaded(PluginId).Should().BeTrue();
        GC.KeepAlive(held);
    }

    [Fact]
    public void An_uninstall_that_unloaded_cleanly_needs_no_restart()
    {
        PluginAssemblyTracker tracker = new();
        TrackAndDrop(tracker);

        PluginRestartRequirement requirement = new PluginRestartAdvisor(tracker).Evaluate(
            new()
            {
                Id = PluginId,
                Name = "Probe",
                Description = "",
                Version = new(1, 0, 0),
                Status = PluginStatus.Deleted,
            },
            PluginOperation.Uninstall
        );

        requirement.Required.Should().BeFalse();
    }

    [Fact]
    public void An_uninstall_whose_assembly_is_pinned_says_so()
    {
        PluginAssemblyTracker tracker = new();
        object held = new();
        tracker.TrackUnload(PluginId, held);

        new PluginRestartAdvisor(tracker)
            .Evaluate(
                new()
                {
                    Id = PluginId,
                    Name = "Probe",
                    Description = "",
                    Version = new(1, 0, 0),
                    Status = PluginStatus.Deleted,
                },
                PluginOperation.Uninstall
            )
            .Reasons.Should()
            .HaveFlag(PluginRestartReason.AssemblyStillLoaded);

        GC.KeepAlive(held);
    }

    /// <summary>
    /// Tracks an object that goes out of scope on return, in its own frame so
    /// no local in the caller keeps it alive.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TrackAndDrop(PluginAssemblyTracker tracker) =>
        tracker.TrackUnload(PluginId, new object());
}
