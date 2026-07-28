// -----------------------------------------------------------------------------
//  Copyright (c) NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy and is proprietary and confidential.
//  Unauthorized copying, distribution, or use is prohibited. See LICENSE.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;

namespace NoMercy.Notifications.Push;

public class PushRelayClient : IPushRelayClient
{
    private sealed class RequestBody
    {
        [JsonPropertyName("channel")]
        public required string Channel { get; init; }

        [JsonPropertyName("entries")]
        public required List<RequestEntry> Entries { get; init; }
    }

    private sealed class RequestEntry
    {
        [JsonPropertyName("subscription_id")]
        public required long SubscriptionId { get; init; }

        [JsonPropertyName("ciphertext")]
        public required string Ciphertext { get; init; }
    }

    public async Task DispatchAsync(
        string channel,
        IReadOnlyList<PushRelayEntry> entries,
        string accessToken,
        CancellationToken cancellationToken = default
    )
    {
        GenericHttpClient client = new(ExternalServicesConfig.Current.ApiServerBaseUrl, 10, 0);
        client.SetDefaultHeaders(ExternalServicesConfig.Current.UserAgent, accessToken);

        // GenericHttpClient.SendAndReadAsync silently drops the HttpContent
        // whenever a non-empty queryParams dictionary is also supplied (it
        // takes the queryParams overload, which never attaches content). The
        // "id" query param has to travel on the URL itself so both the body
        // and the auth-carrying id survive the call.
        string endpoint =
            $"server/push/dispatch?id={Uri.EscapeDataString(Info.DeviceId.ToString())}";

        using StringContent content = new(
            BuildRequestBody(channel, entries),
            Encoding.UTF8,
            "application/json"
        );

        await client.SendAndReadAsync(HttpMethod.Post, endpoint, content, null, cancellationToken);
    }

    internal static string BuildRequestBody(string channel, IReadOnlyList<PushRelayEntry> entries)
    {
        RequestBody requestBody = new()
        {
            Channel = channel,
            Entries = entries
                .Select(entry => new RequestEntry
                {
                    SubscriptionId = entry.SubscriptionId,
                    Ciphertext = entry.Ciphertext,
                })
                .ToList(),
        };

        return JsonSerializer.Serialize(requestBody);
    }
}
