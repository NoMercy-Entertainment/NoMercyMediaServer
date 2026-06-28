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
using System.Diagnostics.CodeAnalysis;

namespace NoMercy.Plugins;

internal sealed class PluginRegistry : IPluginRegistry
{
    private readonly ConcurrentDictionary<Guid, LoadedPlugin> _plugins = new();

    public LoadedPlugin this[Guid id]
    {
        set => _plugins[id] = value;
    }

    public bool TryGetValue(Guid id, [MaybeNullWhen(false)] out LoadedPlugin plugin)
    {
        return _plugins.TryGetValue(id, out plugin);
    }

    public bool TryRemove(Guid id, [MaybeNullWhen(false)] out LoadedPlugin plugin)
    {
        return _plugins.TryRemove(id, out plugin);
    }

    public ICollection<LoadedPlugin> Values => _plugins.Values;

    public void Clear()
    {
        _plugins.Clear();
    }
}
