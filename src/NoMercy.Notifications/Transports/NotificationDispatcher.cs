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

using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Notifications.Transports;

// The preference order is fixed here rather than left to DI registration order:
// SignalR first because a live socket gets an instant, ordered message for
// free, and pushing to that same person too would be the double notification
// this dispatcher exists to avoid. Transports not named in PreferenceOrder
// sort last, in whatever order they were supplied.
public sealed class NotificationDispatcher
{
    private static readonly string[] PreferenceOrder = ["SignalR", "Push"];

    private readonly List<INotificationTransport> _orderedTransports;

    public NotificationDispatcher(IEnumerable<INotificationTransport> transports)
    {
        _orderedTransports = [.. transports.OrderBy(transport => PreferenceIndex(transport.Name))];
    }

    public async Task DispatchAsync(UserNotification notification, CancellationToken ct)
    {
        foreach (INotificationTransport transport in _orderedTransports)
        {
            bool reachable;
            try
            {
                reachable = await transport.CanReachAsync(notification.UserId, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Logger.Notify(
                    $"{transport.Name} reachability check for user {notification.UserId} failed: {exception.Message}"
                );
                continue;
            }

            if (!reachable)
                continue;

            try
            {
                await transport.DeliverAsync(notification, ct);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Logger.Notify(
                    $"{transport.Name} delivery to user {notification.UserId} on channel {notification.Channel} failed: {exception.Message}"
                );
            }
        }
    }

    private static int PreferenceIndex(string name)
    {
        int index = Array.IndexOf(PreferenceOrder, name);
        return index < 0 ? PreferenceOrder.Length : index;
    }
}
