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

using NoMercy.Plugins.Capabilities;

namespace NoMercy.Tests.Plugins;

internal sealed class InMemoryConsentStore : IPluginConsentStore
{
    private readonly HashSet<Guid> _granted = [];

    public bool Contains(Guid pluginId) => _granted.Contains(item: pluginId);

    public void Add(Guid pluginId) => _granted.Add(item: pluginId);

    public void Remove(Guid pluginId) => _granted.Remove(item: pluginId);
}
