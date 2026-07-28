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
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Notifications.Push;

namespace NoMercy.Notifications.Transports;

// Bypasses IPushDispatcher on purpose: that dispatcher seals for every
// subscription the calling server can see, which is correct for a broadcast
// channel but would mean encrypting this notification for people it is not
// for. Here the key set is filtered to one user's own subscriptions BEFORE
// sealing, so the entry list this posts never names another user's device —
// the relay's own audience check is a second gate, not the only one.
public sealed class PushNotificationTransport(
    IPushKeyClient keyClient,
    IWebPushEnvelope envelope,
    IPushRelayClient relayClient,
    IAuthTokenStore tokenStore
) : INotificationTransport
{
    public string Name => "Push";

    public async Task<bool> CanReachAsync(Guid userId, CancellationToken ct)
    {
        PushSubscriptionKey[] keys = await KeysForAsync(userId, ct);
        return keys.Length > 0;
    }

    public async Task DeliverAsync(UserNotification notification, CancellationToken ct)
    {
        try
        {
            PushSubscriptionKey[] keys = await KeysForAsync(notification.UserId, ct);

            if (keys.Length == 0)
                return;

            // Every key here was matched by UserId out of the same server's
            // key set, so they all carry the same UserRef. A missing one
            // means the relay stopped sending it for this member; dispatching
            // with no audience would silently broadcast to the whole server
            // instead, which is worse than sending nothing.
            string? userRef = keys[0].UserRef;

            if (string.IsNullOrEmpty(userRef))
            {
                Logger.Notify(
                    $"Push delivery to user {notification.UserId} skipped: matched subscriptions carry no user_ref, refusing to dispatch without an audience"
                );
                return;
            }

            byte[] plaintext = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(notification.Payload)
            );

            List<PushRelayEntry> entries = keys.Select(key => new PushRelayEntry(
                    key.Id,
                    Convert.ToBase64String(envelope.Seal(plaintext, key.P256dh, key.Auth))
                ))
                .ToList();

            await relayClient.DispatchAsync(
                notification.Channel,
                entries,
                tokenStore.AccessToken ?? string.Empty,
                userRef,
                ct
            );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.Notify(
                $"Push delivery to user {notification.UserId} on channel {notification.Channel} failed: {exception.Message}"
            );
        }
    }

    private async Task<PushSubscriptionKey[]> KeysForAsync(Guid userId, CancellationToken ct)
    {
        PushSubscriptionKey[] keys = await keyClient.GetKeysAsync(
            tokenStore.AccessToken ?? string.Empty,
            ct
        );

        return keys.Where(key => key.UserId == userId).ToArray();
    }
}
