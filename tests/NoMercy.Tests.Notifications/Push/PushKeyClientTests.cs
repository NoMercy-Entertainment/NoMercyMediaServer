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

public class PushKeyClientTests
{
    [Fact]
    public void ParseResponse_Reads_The_Subscription_List()
    {
        PushSubscriptionKey[] keys = PushKeyClient.ParseResponse(
            """{"subscriptions":[{"id":7,"p256dh":"BJ1V","auth":"c2Vj"}]}"""
        );

        Assert.Single(keys);
        Assert.Equal(7, keys[0].Id);
        Assert.Equal("BJ1V", keys[0].P256dh);
        Assert.Equal("c2Vj", keys[0].Auth);
    }

    [Fact]
    public void ParseResponse_Reads_An_Empty_List_As_Empty()
    {
        Assert.Empty(PushKeyClient.ParseResponse("""{"subscriptions":[]}"""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    public void ParseResponse_Throws_Rather_Than_Inventing_An_Empty_List(string body)
    {
        Assert.ThrowsAny<Exception>(() => PushKeyClient.ParseResponse(body));
    }
}
