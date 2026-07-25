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

using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMercy.Api.Hubs;
using NoMercy.Api.Services.LiveTranscode;
using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Service.Extensions;

public static class LiveTranscodeHubServiceExtensions
{
    public static IServiceCollection AddLiveTranscodeHubServices(this IServiceCollection services)
    {
        services.AddScoped<LiveTranscodeHub>();

        // Replace the NoOp transport registered by AddNoMercyEncoder with the real
        // SignalR-backed implementation. Uses Replace (not TryAdd) so the Api-layer
        // transport wins over the encoder's TryAddSingleton default.
        services.Replace(
            ServiceDescriptor.Singleton<ILiveSessionTransport, SignalRLiveSessionTransport>()
        );

        return services;
    }
}
