// -----------------------------------------------------------------------------
//  Copyright (c) NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy and is proprietary and confidential.
//  Unauthorized copying, distribution, or use is prohibited. See LICENSE.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Notifications.Push;
using Xunit;

namespace NoMercy.Tests.Notifications.Push;

public class CachingPushKeyClientTests
{
    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }

    private static readonly PushSubscriptionKey[] SampleKeys = [new(7, "BJ1V", "c2Vj")];

    [Fact]
    public async Task GetKeysAsync_Within_Ttl_Does_Not_Call_The_Inner_Client_Again()
    {
        FakePushKeyClient fake = new(() => Task.FromResult(SampleKeys));
        ManualTimeProvider clock = new(DateTimeOffset.UnixEpoch);
        CachingPushKeyClient cache = new(fake, TimeSpan.FromMinutes(15), clock);

        PushSubscriptionKey[] first = await cache.GetKeysAsync("token");
        clock.Advance(TimeSpan.FromMinutes(1));
        PushSubscriptionKey[] second = await cache.GetKeysAsync("token");

        Assert.Equal(1, fake.CallCount);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetKeysAsync_After_Ttl_Expires_Calls_The_Inner_Client_Again()
    {
        FakePushKeyClient fake = new(() => Task.FromResult(SampleKeys));
        ManualTimeProvider clock = new(DateTimeOffset.UnixEpoch);
        CachingPushKeyClient cache = new(fake, TimeSpan.FromMinutes(15), clock);

        await cache.GetKeysAsync("token");
        clock.Advance(TimeSpan.FromMinutes(16));
        await cache.GetKeysAsync("token");

        Assert.Equal(2, fake.CallCount);
    }

    [Fact]
    public async Task GetKeysAsync_Returns_Empty_Rather_Than_Throwing_When_The_Saas_Is_Unreachable()
    {
        FakePushKeyClient fake = new(() =>
            Task.FromException<PushSubscriptionKey[]>(new HttpRequestException("offline"))
        );
        CachingPushKeyClient cache = new(fake);

        PushSubscriptionKey[] keys = await cache.GetKeysAsync("token");

        Assert.Empty(keys);
    }

    [Fact]
    public async Task GetKeysAsync_Retries_On_The_Next_Call_After_A_Failure()
    {
        int attempt = 0;
        FakePushKeyClient fake = new(() =>
        {
            attempt++;
            return attempt == 1
                ? Task.FromException<PushSubscriptionKey[]>(new HttpRequestException("offline"))
                : Task.FromResult(SampleKeys);
        });
        CachingPushKeyClient cache = new(fake);

        PushSubscriptionKey[] failed = await cache.GetKeysAsync("token");
        PushSubscriptionKey[] recovered = await cache.GetKeysAsync("token");

        Assert.Empty(failed);
        Assert.Same(SampleKeys, recovered);
    }
}
