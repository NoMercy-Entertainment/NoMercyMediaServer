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
using System.Runtime.CompilerServices;

namespace NoMercy.Plugins;

/// <summary>
/// Whether a plugin's assembly has actually gone.
/// <para>
/// <c>AssemblyLoadContext.Unload()</c> asks; it does not promise. The context
/// only goes when nothing references anything inside it, and the collection is
/// asynchronous, so "did it unload" is a question that can only be answered by
/// looking rather than by having called Unload.
/// </para>
/// <para>
/// It matters because the answer is what an owner is told. Assuming it is still
/// loaded means saying "restart required" after every uninstall, including the
/// ones that were clean; assuming it unloaded means promising a file is
/// replaceable when Windows still has it locked.
/// </para>
/// </summary>
public interface IPluginAssemblyTracker
{
    /// <summary>Watches a load context that has just been asked to unload.</summary>
    void TrackUnload(Guid pluginId, object loadContext);

    /// <summary>
    /// Whether anything of <paramref name="pluginId"/> is still resident.
    /// <para>False for a plugin never tracked: nothing was unloaded, so nothing
    /// is lingering.</para>
    /// </summary>
    bool IsStillLoaded(Guid pluginId);
}

public class PluginAssemblyTracker : IPluginAssemblyTracker
{
    private readonly ConcurrentDictionary<Guid, WeakReference> _unloading = new();

    public void TrackUnload(Guid pluginId, object loadContext) =>
        _unloading[pluginId] = new(loadContext, trackResurrection: true);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool IsStillLoaded(Guid pluginId)
    {
        if (!_unloading.TryGetValue(pluginId, out WeakReference? reference))
            return false;

        // A collect before looking. Without it the answer is "yes" for every
        // context that simply has not been collected yet, which is the
        // pessimistic answer this exists to stop giving.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        if (reference.IsAlive)
            return true;

        // Gone for good; stop holding the entry.
        _unloading.TryRemove(pluginId, out _);
        return false;
    }
}
