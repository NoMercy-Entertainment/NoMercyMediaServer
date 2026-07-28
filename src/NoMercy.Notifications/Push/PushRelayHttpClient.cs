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
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Notifications.Push;

internal static class PushRelayHttpClient
{
    private const int TimeoutSeconds = 10;

    // Push is fire and forget by design, so a retry here would turn one
    // unreachable relay into a storm of duplicate sends.
    private const int RetryCount = 0;

    internal static GenericHttpClient ForServer(string accessToken)
    {
        GenericHttpClient client = new(
            ExternalServicesConfig.Current.ApiServerBaseUrl,
            TimeoutSeconds,
            RetryCount
        );
        client.SetDefaultHeaders(ExternalServicesConfig.Current.UserAgent, accessToken);
        return client;
    }
}
