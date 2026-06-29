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

using System;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace NoMercy.Monitoring;

/// <summary>
/// Collects CPU/GPU/memory resource samples from the OS-appropriate provider.
/// Resolved from DI as a singleton; the provider is created on construction.
/// </summary>
public sealed class ResourceMonitor : IDisposable
{
    private readonly ILogger<ResourceMonitor> _logger;
    private IResourceProvider? _provider;

    public ResourceMonitor(ILogger<ResourceMonitor> logger)
    {
        _logger = logger;
        _logger.LogInformation("Initializing Resource Monitor");
        Start();
    }

    public Resource Monitor()
    {
        if (_provider is null)
            return new();

        try
        {
            return _provider.Collect();
        }
        catch (Exception)
        {
            return new();
        }
    }

    public void Start()
    {
        if (_provider is not null)
            return;

        if (OperatingSystem.IsWindows())
            _provider = CreateWindowsProvider();
        else if (OperatingSystem.IsLinux())
            _provider = CreateLinuxProvider();

        _logger.LogInformation("Resource Monitor started");
    }

    public void Stop()
    {
        if (_provider is IDisposable disposable)
            disposable.Dispose();
        _provider = null;
        _logger.LogInformation("Resource Monitor stopped");
    }

    public void Dispose()
    {
        // Dispose the provider without logging: the logger may already be torn
        // down during container disposal.
        if (_provider is IDisposable disposable)
            disposable.Dispose();
        _provider = null;
    }

    [SupportedOSPlatform("windows")]
    private static IResourceProvider CreateWindowsProvider() => new WindowsResourceProvider();

    [SupportedOSPlatform("linux")]
    private static IResourceProvider CreateLinuxProvider() => new LinuxResourceProvider();
}
