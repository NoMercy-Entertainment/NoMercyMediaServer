// -----------------------------------------------------------------------------
//  Copyright (c) NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy and is proprietary and confidential.
//  Unauthorized copying, distribution, or use is prohibited. See LICENSE.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Text.Json;
using NoMercy.Notifications.Push;
using Xunit;

namespace NoMercy.Tests.Notifications.Push;

public class PushRelayClientTests
{
    [Fact]
    public void BuildRequestBody_Uses_The_Hyphenated_Channel_And_Snake_Case_Entry_Fields()
    {
        string body = PushRelayClient.BuildRequestBody("encode-finished", [new(7, "c2VhbGVk")]);

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;

        Assert.Equal("encode-finished", root.GetProperty("channel").GetString());

        JsonElement entry = root.GetProperty("entries")[0];
        Assert.Equal(7, entry.GetProperty("subscription_id").GetInt64());
        Assert.Equal("c2VhbGVk", entry.GetProperty("ciphertext").GetString());
    }

    [Fact]
    public void BuildRequestBody_Carries_Every_Entry()
    {
        string body = PushRelayClient.BuildRequestBody(
            "library-updated",
            [new(1, "aaaa"), new(2, "bbbb")]
        );

        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal(2, document.RootElement.GetProperty("entries").GetArrayLength());
    }
}
