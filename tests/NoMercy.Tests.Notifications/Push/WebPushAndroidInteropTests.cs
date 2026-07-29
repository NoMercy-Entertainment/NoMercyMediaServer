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

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Moq;
using NoMercy.Notifications.Push;
using Xunit;

namespace NoMercy.Tests.Notifications.Push;

/// <summary>
/// Both this repo and nomercy-app-kmp pass the RFC 8291 vector independently,
/// which proves nothing about interoperability — each side can share a wrong
/// assumption about framing, the padding delimiter, or the wire base64
/// variant and still see its own test go green. These tests pin the shared
/// assumptions explicitly against the literal offsets WebPushUnsealer.kt
/// hardcodes, and drive the real PushDispatcher rather than a copy of it.
/// </summary>
public class WebPushAndroidInteropTests
{
    private sealed record Vector(
        string Plaintext,
        string UserAgentPublicKey,
        string UserAgentPrivateKey,
        string AuthSecret,
        string ApplicationServerPublicKey,
        string ApplicationServerPrivateKey,
        string ExpectedCiphertext
    );

    private static Vector LoadVector()
    {
        string json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Push", "rfc8291-example.json")
        );
        Vector? vector = JsonSerializer.Deserialize<Vector>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(vector);
        return vector!;
    }

    [Fact]
    public void Sealed_Body_Header_Lands_At_The_Exact_Offsets_The_Android_Unsealer_Hardcodes()
    {
        Vector vector = LoadVector();
        byte[] serverPrivateKey = Base64UrlCodec.Decode(vector.ApplicationServerPrivateKey);
        byte[] serverPublicKey = Base64UrlCodec.Decode(vector.ApplicationServerPublicKey);
        byte[] salt = Base64UrlCodec.Decode(vector.ExpectedCiphertext)[..16];

        WebPushEnvelope envelope = new(fixedSalt: salt, fixedServerKey: serverPrivateKey);

        byte[] sealedBody = envelope.Seal(
            Encoding.UTF8.GetBytes(vector.Plaintext),
            vector.UserAgentPublicKey,
            vector.AuthSecret
        );

        Assert.Equal(vector.ExpectedCiphertext, Base64UrlCodec.Encode(sealedBody));

        Assert.Equal(salt, sealedBody[..DeviceSideUnseal.SaltLength]);
        Assert.Equal(
            4096u,
            BinaryPrimitives.ReadUInt32BigEndian(sealedBody.AsSpan(DeviceSideUnseal.SaltLength, 4))
        );
        Assert.Equal(65, sealedBody[DeviceSideUnseal.ServerKeyLengthOffset]);
        Assert.Equal(
            serverPublicKey,
            sealedBody[DeviceSideUnseal.ServerKeyOffset..(DeviceSideUnseal.ServerKeyOffset + 65)]
        );
    }

    /// <summary>
    /// The Android unsealer finds the delimiter by scanning for the last
    /// non-zero byte, which only works if a plaintext ending in its own zero
    /// byte does not get mistaken for padding. It works here because Seal
    /// never appends padding after the delimiter in the single-record case —
    /// the delimiter is unconditionally the final byte — but that guarantee
    /// is exactly what this test locks in rather than assumes.
    /// </summary>
    [Fact]
    public void A_Plaintext_Ending_In_A_Zero_Byte_Still_Round_Trips_Through_The_Android_Trailing_Zero_Strip()
    {
        Vector vector = LoadVector();
        WebPushEnvelope envelope = new();
        byte[] prefix = Encoding.UTF8.GetBytes("device-token-");
        byte[] plaintext = new byte[prefix.Length + 1];
        prefix.CopyTo(plaintext, 0);
        plaintext[^1] = 0x00;

        byte[] sealedBody = envelope.Seal(plaintext, vector.UserAgentPublicKey, vector.AuthSecret);

        byte[] recovered = DeviceSideUnseal.Unseal(
            sealedBody,
            vector.UserAgentPrivateKey,
            vector.UserAgentPublicKey,
            vector.AuthSecret
        );

        Assert.Equal(plaintext, recovered);
    }

    [Fact]
    public async Task PushDispatcher_Produces_A_Body_The_Android_Unsealer_Can_Actually_Open()
    {
        Vector vector = LoadVector();

        Mock<IPushKeyClient> keys = new();
        keys.Setup(client => client.GetKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PushSubscriptionKey(1, vector.UserAgentPublicKey, vector.AuthSecret),
            ]);

        IReadOnlyList<PushRelayEntry>? captured = null;
        Mock<IPushRelayClient> relay = new();
        relay
            .Setup(r =>
                r.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<PushRelayEntry>>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, IReadOnlyList<PushRelayEntry>, string, string?, CancellationToken>(
                (_, entries, _, _, _) => captured = entries
            )
            .Returns(Task.CompletedTask);

        PushDispatcher dispatcher = new(keys.Object, new WebPushEnvelope(), relay.Object);
        PushPayload payload = new("Encoding finished", "Idiocracy finished encoding", "/movie/1");

        await dispatcher.DispatchAsync("encode-finished", payload, "token");

        PushRelayEntry entry = Assert.Single(captured!);

        // The relay forwards this string verbatim into the FCM data key "p",
        // which NoMercyMessagingService decodes with java.util.Base64.getDecoder()
        // — the standard alphabet. Convert.FromBase64String uses the same
        // alphabet; a base64url body would throw here on the first '-' or '_'.
        byte[] sealedBody = Convert.FromBase64String(entry.Ciphertext);

        byte[] recovered = DeviceSideUnseal.Unseal(
            sealedBody,
            vector.UserAgentPrivateKey,
            vector.UserAgentPublicKey,
            vector.AuthSecret
        );

        string expectedJson = JsonSerializer.Serialize(payload);
        Assert.Equal(expectedJson, Encoding.UTF8.GetString(recovered));
    }

    /// <summary>
    /// Image/Icon ride the same sealed body as every other field. This proves
    /// the URLs survive ECDH derivation, AES-128-GCM encryption and the
    /// Android-faithful decrypt byte-for-byte — not just that
    /// JsonSerializer round-trips them in memory.
    /// </summary>
    [Fact]
    public async Task PushDispatcher_Produces_A_Body_The_Android_Unsealer_Can_Open_With_Artwork_Urls_Intact()
    {
        Vector vector = LoadVector();

        Mock<IPushKeyClient> keys = new();
        keys.Setup(client => client.GetKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PushSubscriptionKey(1, vector.UserAgentPublicKey, vector.AuthSecret),
            ]);

        IReadOnlyList<PushRelayEntry>? captured = null;
        Mock<IPushRelayClient> relay = new();
        relay
            .Setup(r =>
                r.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<PushRelayEntry>>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, IReadOnlyList<PushRelayEntry>, string, string?, CancellationToken>(
                (_, entries, _, _, _) => captured = entries
            )
            .Returns(Task.CompletedTask);

        PushDispatcher dispatcher = new(keys.Object, new WebPushEnvelope(), relay.Object);
        PushPayload payload = new(
            "Encoding finished",
            "Idiocracy finished encoding",
            "/movie/1",
            Image: "https://app.nomercy.tv/tmdb-images/backdrop123.jpg?width=500",
            Icon: "https://app.nomercy.tv/tmdb-images/poster123.jpg?width=200"
        );

        await dispatcher.DispatchAsync("encode-finished", payload, "token");

        PushRelayEntry entry = Assert.Single(captured!);
        byte[] sealedBody = Convert.FromBase64String(entry.Ciphertext);

        byte[] recovered = DeviceSideUnseal.Unseal(
            sealedBody,
            vector.UserAgentPrivateKey,
            vector.UserAgentPublicKey,
            vector.AuthSecret
        );

        JsonDocument document = JsonDocument.Parse(Encoding.UTF8.GetString(recovered));
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
