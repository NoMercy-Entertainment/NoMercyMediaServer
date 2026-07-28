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

using System.Threading.Channels;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Notifications.Push;

/// <summary>
/// Keeps push off the event-publishing path. IEventBus.PublishAsync awaits its
/// subscribers one after another, so a dispatcher that talks to nomercy.tv
/// inline makes every publisher wait on a relay round trip — and a server that
/// cannot currently reach nomercy.tv would stall on the timeout, once per
/// event, while encoding and streaming wait behind it.
///
/// Bounded and dropping: a self-hosted server that has been offline for an hour
/// must not wake up to an hour of stale notifications, and it must not have
/// spent that hour growing a queue in memory.
/// </summary>
public class PushDispatchQueue(IPushDispatcher dispatcher) : IPushDispatchQueue
{
    private const int Capacity = 256;

    private readonly Channel<PushDispatchRequest> _queue =
        Channel.CreateBounded<PushDispatchRequest>(
            new BoundedChannelOptions(Capacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
            }
        );

    public void Enqueue(PushDispatchRequest request)
    {
        _queue.Writer.TryWrite(request);
    }

    public async Task DrainAsync(CancellationToken cancellationToken)
    {
        await foreach (PushDispatchRequest request in _queue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await dispatcher.DispatchAsync(
                    request.Channel,
                    request.Payload,
                    request.AccessToken,
                    request.Audience,
                    cancellationToken
                );
            }
            // An HttpClient timeout surfaces as a TaskCanceledException, which
            // is an OperationCanceledException. Letting it out would end the
            // drain loop for the lifetime of the process over one slow relay.
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Logger.Notify($"Push dispatch to channel {request.Channel} timed out");
            }
        }
    }
}
