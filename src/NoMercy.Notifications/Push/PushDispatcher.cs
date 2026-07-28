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

            // STANDARD base64, not base64url: the shipped relay decodes the
            // ciphertext with base64_decode($ciphertext, true), which rejects
            // the '-' and '_' of base64url and drops the send silently.
            List<PushRelayEntry> entries = keys.Select(key => new PushRelayEntry(
                    key.Id,
                    Convert.ToBase64String(envelope.Seal(plaintext, key.P256dh, key.Auth))
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
