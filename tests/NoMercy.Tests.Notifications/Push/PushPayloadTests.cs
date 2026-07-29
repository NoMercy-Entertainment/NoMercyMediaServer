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
using NoMercy.Notifications.Push;
using Xunit;

namespace NoMercy.Tests.Notifications.Push;

public class PushPayloadTests
{
    /// <summary>
    /// A client iterating Actions must never null-check it. The three-argument
    /// constructor is also exactly what every existing caller (PushNotificationEventHandler)
    /// still uses, so this doubles as the "old callers are unchanged on the wire" proof.
    /// </summary>
    [Fact]
    public void An_Ordinary_Notification_Serializes_Actions_As_An_Empty_Array_Never_Null()
    {
        PushPayload payload = new("Encoding finished", "Idiocracy finished encoding", "/movie/1");

        string json = JsonSerializer.Serialize(payload);
        JsonDocument document = JsonDocument.Parse(json);

        JsonElement actions = document.RootElement.GetProperty("Actions");
        Assert.Equal(JsonValueKind.Array, actions.ValueKind);
        Assert.Empty(actions.EnumerateArray());
    }

    [Fact]
    public void An_Ordinary_Notification_Keeps_The_Same_Title_Body_And_Route_On_The_Wire()
    {
        PushPayload payload = new("Encoding finished", "Idiocracy finished encoding", "/movie/1");

        string json = JsonSerializer.Serialize(payload);
        JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Equal("Encoding finished", root.GetProperty("Title").GetString());
        Assert.Equal("Idiocracy finished encoding", root.GetProperty("Body").GetString());
        Assert.Equal("/movie/1", root.GetProperty("Route").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("Category").ValueKind);
    }

    [Fact]
    public void A_Null_Route_Serializes_As_Json_Null_Not_Omitted()
    {
        PushPayload payload = new("Encoding failed", "ffmpeg exited with code 1", null);

        string json = JsonSerializer.Serialize(payload);
        JsonDocument document = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("Route").ValueKind);
    }

    /// <summary>
    /// The clients are written against the spec's exact casing
    /// (Title/Body/Route/Category/Actions, and Id/Title/Route per action). This
    /// repo's other JSON surfaces use Newtonsoft with camelCase or snake_case
    /// contracts elsewhere, so the names have to be pinned rather than left to
    /// whatever System.Text.Json's default happens to be today.
    /// </summary>
    [Fact]
    public void Property_Names_Match_The_Spec_Exactly_Including_Casing()
    {
        PushPayload payload = new(
            "New device sign-in request",
            "A new device is requesting access to your account.",
            "/security/devices",
            "security-new-device",
            [new PushAction("approve", "Approve", "/security/devices/approve/1")]
        );

        string json = JsonSerializer.Serialize(payload);
        JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.True(root.TryGetProperty("Title", out _));
        Assert.True(root.TryGetProperty("Body", out _));
        Assert.True(root.TryGetProperty("Route", out _));
        Assert.True(root.TryGetProperty("Category", out _));
        Assert.True(root.TryGetProperty("Actions", out JsonElement actions));

        JsonElement action = Assert.Single(actions.EnumerateArray());
        Assert.True(action.TryGetProperty("Id", out _));
        Assert.True(action.TryGetProperty("Title", out _));
        Assert.True(action.TryGetProperty("Route", out _));
    }

    [Fact]
    public void Category_Is_Carried_Through_When_Set()
    {
        PushPayload payload = new(
            "New device sign-in request",
            "A new device is requesting access to your account.",
            "/security/devices",
            "security-new-device"
        );

        string json = JsonSerializer.Serialize(payload);
        JsonDocument document = JsonDocument.Parse(json);

        Assert.Equal(
            "security-new-device",
            document.RootElement.GetProperty("Category").GetString()
        );
    }

    [Fact]
    public void Actions_Round_Trip_With_Their_Own_Id_Title_And_Route()
    {
        PushPayload payload = new(
            "Cast requested",
            "Someone wants to cast to this TV",
            null,
            null,
            [
                new PushAction("approve", "Approve", "/cast/approve/9"),
                new PushAction("deny", "Deny", null),
            ]
        );

        string json = JsonSerializer.Serialize(payload);
        JsonDocument document = JsonDocument.Parse(json);
        JsonElement actions = document.RootElement.GetProperty("Actions");

        Assert.Equal(2, actions.GetArrayLength());
        JsonElement first = actions[0];
        Assert.Equal("approve", first.GetProperty("Id").GetString());
        Assert.Equal("Approve", first.GetProperty("Title").GetString());
        Assert.Equal("/cast/approve/9", first.GetProperty("Route").GetString());

        JsonElement second = actions[1];
        Assert.Equal(JsonValueKind.Null, second.GetProperty("Route").ValueKind);
    }

    /// <summary>
    /// Every existing caller before artwork existed builds a payload without
    /// Image/Icon. Both must still serialize as explicit JSON null — never
    /// omitted, and never an empty string or other placeholder a client's
    /// image loader would try and fail to fetch.
    /// </summary>
    [Fact]
    public void A_Payload_With_No_Artwork_Serializes_Image_And_Icon_As_Json_Null_Not_Omitted()
    {
        PushPayload payload = new("Encoding finished", "Idiocracy finished encoding", "/movie/1");

        string json = JsonSerializer.Serialize(payload);
        JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.True(root.TryGetProperty("Image", out JsonElement image));
        Assert.Equal(JsonValueKind.Null, image.ValueKind);
        Assert.True(root.TryGetProperty("Icon", out JsonElement icon));
        Assert.Equal(JsonValueKind.Null, icon.ValueKind);
    }

    [Fact]
    public void A_Payload_With_Artwork_Carries_The_Image_And_Icon_Urls_On_The_Wire()
    {
        PushPayload payload = new(
            "Encoding finished",
            "Idiocracy finished encoding",
            "/movie/1",
            Image: "https://app.nomercy.tv/tmdb-images/backdrop123.jpg?width=500",
            Icon: "https://app.nomercy.tv/tmdb-images/poster123.jpg?width=200"
        );

        string json = JsonSerializer.Serialize(payload);
        JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Equal(
            "https://app.nomercy.tv/tmdb-images/backdrop123.jpg?width=500",
            root.GetProperty("Image").GetString()
        );
        Assert.Equal(
            "https://app.nomercy.tv/tmdb-images/poster123.jpg?width=200",
            root.GetProperty("Icon").GetString()
        );
    }
}
