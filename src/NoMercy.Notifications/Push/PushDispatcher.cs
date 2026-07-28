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
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Notifications.Push;

public class PushDispatcher(
    IPushKeyClient keyClient,
    IWebPushEnvelope envelope,
    IPushRelayClient relayClient
) : IPushDispatcher
{
    public async Task DispatchAsync(
        string channel,
        PushPayload payload,
        string accessToken,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            PushSubscriptionKey[] keys = await keyClient.GetKeysAsync(
                accessToken,
                cancellationToken
            );

            if (keys.Length == 0)
                return;

            byte[] plaintext = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

            List<PushRelayEntry> entries = keys.Select(key => new PushRelayEntry(
                    key.Id,
                    Base64UrlCodec.Encode(envelope.Seal(plaintext, key.P256dh, key.Auth))
                ))
                .ToList();

            await relayClient.DispatchAsync(channel, entries, accessToken, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.Notify($"Push dispatch to channel {channel} failed: {exception.Message}");
        }
    }
}
