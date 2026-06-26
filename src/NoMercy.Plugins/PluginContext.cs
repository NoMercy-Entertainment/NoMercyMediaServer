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

using Microsoft.Extensions.Logging;
using NoMercy.Events;
using NoMercy.Plugins.Abstractions;
using NoMercy.Storage;

namespace NoMercy.Plugins;

public class PluginContext : IPluginContext
{
    public IEventBus EventBus { get; }
    public IServiceProvider Services { get; }
    public ILogger Logger { get; }
    public string DataFolderPath { get; }
    public IPluginConfiguration Configuration { get; }

    public PluginContext(
        IEventBus eventBus,
        IServiceProvider services,
        ILogger logger,
        string dataFolderPath,
        IStorage storage
    )
    {
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        DataFolderPath = dataFolderPath ?? throw new ArgumentNullException(nameof(dataFolderPath));
        Configuration = new PluginConfiguration(dataFolderPath, storage);
    }
}
