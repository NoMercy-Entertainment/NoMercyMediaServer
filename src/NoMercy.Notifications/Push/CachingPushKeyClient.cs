// -----------------------------------------------------------------------------
//  Copyright (c) NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy and is proprietary and confidential.
//  Unauthorized copying, distribution, or use is prohibited. See LICENSE.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

namespace NoMercy.Notifications.Push;

/// <summary>
/// Sends one notification per call site, and the SaaS's key set for this
/// server's members changes rarely, so a bare <see cref="IPushKeyClient"/>
/// would hit api.nomercy.tv on every single push. Wraps an inner client with a
/// lazily-refreshed TTL cache (no background timer — a stale cache is only
/// ever noticed, and refreshed, on the next call) and turns any failure to
/// reach the SaaS into an empty result: a self-hosted server that cannot
/// currently reach nomercy.tv must keep encoding, streaming, and everything
/// else — it just skips sending that one push. A caller-driven cancellation
/// is not "the SaaS is unreachable" and is left to propagate.
///
/// <see cref="PushDispatcher"/> can be invoked concurrently by unrelated
/// event producers (two notifications finishing at once), so a miss under
/// the TTL is not a single-caller scenario: the refresh is guarded by a lock
/// rather than trusting the last caller wins, otherwise two concurrent
/// misses would each fetch the same key set from the SaaS.
/// </summary>
public class CachingPushKeyClient(
    IPushKeyClient inner,
    TimeSpan? ttl = null,
    TimeProvider? timeProvider = null
) : IPushKeyClient
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(15);

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly TimeSpan _ttl = ttl ?? DefaultTtl;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private PushSubscriptionKey[] _cached = [];
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public async Task<PushSubscriptionKey[]> GetKeysAsync(
        string accessToken,
        CancellationToken cancellationToken = default
    )
    {
        if (_timeProvider.GetUtcNow() < _expiresAt)
            return _cached;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_timeProvider.GetUtcNow() < _expiresAt)
                return _cached;

            try
            {
                _cached = await inner.GetKeysAsync(accessToken, cancellationToken);
                _expiresAt = _timeProvider.GetUtcNow() + _ttl;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _cached = [];
                _expiresAt = DateTimeOffset.MinValue;
            }

            return _cached;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
