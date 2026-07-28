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

using System.Text.Json;
using NoMercy.NmSystem.Configuration;
using NoMercy.Notifications.Push;
using Xunit;

namespace NoMercy.Tests.Notifications.Push;

public class PushRelayClientTests
{
    private static readonly Guid DeviceId = Guid.Parse("2f6d1a4e-0000-4000-8000-0000000000ff");

    /// <summary>
    /// The endpoint is relative to a base URL that already ends in
    /// /v1/server/, so a "server/" prefix here silently resolves to
    /// /v1/server/server/push/dispatch. Every dispatch 404s, and
    /// <see cref="PushDispatcher"/> swallows the failure, so nothing anywhere
    /// says so. This assertion is the only thing that does.
    /// </summary>
    [Fact]
    public void The_Dispatch_Endpoint_Resolves_Onto_The_Server_Base_Without_Doubling_It()
    {
        Uri baseUri = new(ExternalServicesConfig.Current.ApiServerBaseUrl);

        Uri resolved = new(baseUri, PushRelayClient.BuildEndpoint(DeviceId));

        Assert.Equal($"{baseUri.AbsolutePath}push/dispatch", resolved.AbsolutePath);
    }

    [Fact]
    public void The_Dispatch_Endpoint_Is_The_Relay_Route_On_The_Production_Base()
    {
        Uri resolved = new(
            new Uri("https://api.nomercy.tv/v1/server/"),
            PushRelayClient.BuildEndpoint(DeviceId)
        );

        Assert.Equal(
            "https://api.nomercy.tv/v1/server/push/dispatch",
            resolved.GetLeftPart(UriPartial.Path)
        );
        Assert.Equal($"?id={DeviceId}", resolved.Query);
    }

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
