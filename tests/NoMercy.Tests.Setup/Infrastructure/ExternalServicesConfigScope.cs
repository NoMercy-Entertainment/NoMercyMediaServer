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

using NoMercy.NmSystem.Configuration;

namespace NoMercy.Tests.Setup.Infrastructure;

/// <summary>
/// Temporarily repoints <see cref="ExternalServicesConfig.Current"/>'s auth/API base
/// URLs at a <see cref="LoopbackHttpServer"/> (or any URL) and restores the original
/// values on dispose. <c>ExternalServicesConfig.Current</c> is a shared static instance,
/// so every test using this scope must dispose it before the next test reads the config —
/// xUnit's per-test-class instantiation with IDisposable already guarantees this as long
/// as the scope is created and disposed within a single test method.
/// </summary>
public sealed class ExternalServicesConfigScope : IDisposable
{
    private readonly string _originalAuthBaseUrl;
    private readonly string _originalApiBaseUrl;
    private readonly string _originalApiServerBaseUrl;

    public ExternalServicesConfigScope(
        string? authBaseUrl = null,
        string? apiBaseUrl = null,
        string? apiServerBaseUrl = null
    )
    {
        _originalAuthBaseUrl = ExternalServicesConfig.Current.AuthBaseUrl;
        _originalApiBaseUrl = ExternalServicesConfig.Current.ApiBaseUrl;
        _originalApiServerBaseUrl = ExternalServicesConfig.Current.ApiServerBaseUrl;

        if (authBaseUrl is not null)
            ExternalServicesConfig.Current.AuthBaseUrl = authBaseUrl;
        if (apiBaseUrl is not null)
            ExternalServicesConfig.Current.ApiBaseUrl = apiBaseUrl;
        if (apiServerBaseUrl is not null)
            ExternalServicesConfig.Current.ApiServerBaseUrl = apiServerBaseUrl;
    }

    public void Dispose()
    {
        ExternalServicesConfig.Current.AuthBaseUrl = _originalAuthBaseUrl;
        ExternalServicesConfig.Current.ApiBaseUrl = _originalApiBaseUrl;
        ExternalServicesConfig.Current.ApiServerBaseUrl = _originalApiServerBaseUrl;
    }
}
