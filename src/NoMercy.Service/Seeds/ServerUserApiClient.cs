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
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.Service.Seeds.Dto;

namespace NoMercy.Service.Seeds;

public interface IServerUserApiClient
{
    /// <summary>
    /// Fetches the current accepted server-users list (including the caller,
    /// via <c>with_self=true</c>) from api.nomercy.tv for this device.
    /// </summary>
    Task<ServerUserDtoData[]> GetServerUsersAsync(
        string accessToken,
        CancellationToken cancellationToken = default
    );
}

public class ServerUserApiClient : IServerUserApiClient
{
    public async Task<ServerUserDtoData[]> GetServerUsersAsync(
        string accessToken,
        CancellationToken cancellationToken = default
    )
    {
        Dictionary<string, string> queryParams = new()
        {
            [key: "id"] = Info.DeviceId.ToString(),
            [key: "with_self"] = "true",
        };

        GenericHttpClient authClient = new(baseUrl: ExternalServicesConfig.Current.ApiServerBaseUrl, timeoutSeconds: 10, retryCount: 0);
        authClient.SetDefaultHeaders(userAgent: ExternalServicesConfig.Current.UserAgent, bearerToken: accessToken);

        string response = await authClient.SendAndReadAsync(
            method: HttpMethod.Get,
            endpoint: "server-users",
            content: null,
            queryParams: queryParams,
            cancellationToken: cancellationToken
        );

        return ParseResponse(response: response);
    }

    /// <summary>
    /// SendAndReadAsync returns "" (never null) for an empty 200 body, and
    /// JsonHelper.FromJson is a forgiving parser that swallows malformed/empty
    /// JSON and returns null rather than throwing. Both used to fall straight
    /// through to `?.Data ?? []` — a 200-with-bad-body from api.nomercy.tv was
    /// silently treated as "authoritative zero users", and the caller's revoke
    /// step then stripped every non-owner user's access. Treat "couldn't parse
    /// a real payload" as the same failure class as a network error: throw so
    /// SyncAsync aborts before touching the database. Internal (not private) so
    /// this parsing rule is unit-testable without a live HTTP round trip.
    /// </summary>
    internal static ServerUserDtoData[] ParseResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(value: response))
            throw new InvalidOperationException(message: "server-users response was empty or unparseable");

        ServerUserDto? parsed = response.FromJson<ServerUserDto>();
        if (parsed is null)
            throw new InvalidOperationException(message: "server-users response was empty or unparseable");

        return parsed.Data;
    }
}
