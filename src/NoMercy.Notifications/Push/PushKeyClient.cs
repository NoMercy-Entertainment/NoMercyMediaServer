// -----------------------------------------------------------------------------
//  Copyright (c) NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy and is proprietary and confidential.
//  Unauthorized copying, distribution, or use is prohibited. See LICENSE.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;

namespace NoMercy.Notifications.Push;

public class PushKeyClient : IPushKeyClient
{
    private sealed class Envelope
    {
        [JsonPropertyName("subscriptions")]
        public List<Entry>? Subscriptions { get; init; }
    }

    private sealed class Entry
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("p256dh")]
        public string? P256dh { get; init; }

        [JsonPropertyName("auth")]
        public string? Auth { get; init; }
    }

    public async Task<PushSubscriptionKey[]> GetKeysAsync(
        string accessToken,
        CancellationToken cancellationToken = default
    )
    {
        Dictionary<string, string> queryParams = new() { ["id"] = Info.DeviceId.ToString() };

        GenericHttpClient client = new(ExternalServicesConfig.Current.ApiServerBaseUrl, 10, 0);
        client.SetDefaultHeaders(ExternalServicesConfig.Current.UserAgent, accessToken);

        string response = await client.SendAndReadAsync(
            HttpMethod.Get,
            "push/keys",
            null,
            queryParams,
            cancellationToken
        );

        return ParseResponse(response);
    }

    /// <summary>
    /// A 200 whose body cannot be read as a subscriptions array must not be
    /// treated as "zero devices" — that would silently stop every push
    /// notification to this server's members. Throw instead, the same way
    /// <see cref="NoMercy.Service.Seeds.ServerUserApiClient.ParseResponse"/>
    /// throws rather than inventing an empty roster. Internal (not private) so
    /// this parsing rule is unit-testable without a live HTTP round trip.
    /// </summary>
    internal static PushSubscriptionKey[] ParseResponse(string response)
    {
        Envelope? envelope = JsonSerializer.Deserialize<Envelope>(response);

        if (envelope?.Subscriptions is null)
        {
            throw new InvalidOperationException(
                "push/keys returned a body without a subscriptions array; refusing to treat it as zero devices"
            );
        }

        return envelope
            .Subscriptions.Where(entry => entry.P256dh is not null && entry.Auth is not null)
            .Select(entry => new PushSubscriptionKey(entry.Id, entry.P256dh!, entry.Auth!))
            .ToArray();
    }
}
