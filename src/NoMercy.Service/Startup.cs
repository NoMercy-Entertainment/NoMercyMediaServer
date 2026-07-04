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

using Asp.Versioning.ApiExplorer;
using NoMercy.Service.Configuration;

namespace NoMercy.Service;

public class Startup
{
    private readonly IApiVersionDescriptionProvider _provider;
    private readonly StartupOptions _options;
    private readonly IConfiguration _configuration;

    public Startup(IApiVersionDescriptionProvider provider, StartupOptions options, IConfiguration configuration)
    {
        _provider = provider;
        _options = options;
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        ServiceConfiguration.ConfigureServices(services, _configuration);

        services.AddSingleton(_options);
    }

    public void Configure(IApplicationBuilder app)
    {
        ApplicationConfiguration.ConfigureApp(app, _provider);
    }
}
