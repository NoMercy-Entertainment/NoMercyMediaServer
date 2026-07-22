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
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Plugins.Abstractions;

namespace NoMercy.Plugins;

/// <summary>
/// Creates plugin instances through the dependency-injection container instead of
/// <see cref="Activator"/>, so a plugin's constructor receives the services
/// registered for it (via <see cref="IPluginServiceRegistrator"/> at startup)
/// together with the shared host services. The container does not take ownership
/// of the returned instance; the caller remains responsible for disposing it.
/// </summary>
internal static class PluginInstanceFactory
{
    internal static IPlugin Create(IServiceProvider services, Type pluginType)
    {
        return (IPlugin)ActivatorUtilities.CreateInstance(provider: services, instanceType: pluginType);
    }
}
