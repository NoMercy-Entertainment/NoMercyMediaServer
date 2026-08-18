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

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NoMercy.Notifications.Push;
using Xunit;

namespace NoMercy.Tests.Notifications.Push;

public class WebPushEnvelopeTests
{
    private const int RecordSize = 4096;
    private const int MaxPlaintextLength = RecordSize - 16 - 1; // rs minus AEAD tag minus padding delimiter

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
    public void Seal_Reproduces_The_Rfc8291_Example()
    {
        Vector vector = LoadVector();
        WebPushEnvelope envelope = new(
            fixedSalt: Base64UrlCodec.Decode(vector.ExpectedCiphertext)[..16],
            fixedServerKey: Base64UrlCodec.Decode(vector.ApplicationServerPrivateKey)
        );

        byte[] sealedBody = envelope.Seal(
            Encoding.UTF8.GetBytes(vector.Plaintext),
            vector.UserAgentPublicKey,
            vector.AuthSecret
        );

        Assert.Equal(vector.ExpectedCiphertext, Base64UrlCodec.Encode(sealedBody));
    }

    [Fact]
    public void Seal_Produces_A_Different_Body_Each_Time_For_The_Same_Input()
    {
        Vector vector = LoadVector();
        WebPushEnvelope envelope = new();

        byte[] first = envelope.Seal([.. "same"u8], vector.UserAgentPublicKey, vector.AuthSecret);
        byte[] second = envelope.Seal([.. "same"u8], vector.UserAgentPublicKey, vector.AuthSecret);

        Assert.NotEqual(Convert.ToBase64String(first), Convert.ToBase64String(second));
    }

    [Fact]
    public void Seal_Rejects_A_Public_Key_With_The_Wrong_Length_Or_Prefix()
    {
        WebPushEnvelope envelope = new();

        Assert.ThrowsAny<Exception>(() =>
            envelope.Seal([.. "x"u8], "BAAAAAAAAAAAAAAAAAAA", "AAAAAAAAAAAAAAAAAAAAAA")
        );
    }

    [Fact]
    public void Seal_Rejects_A_Well_Formed_Point_That_Is_Not_On_The_Curve()
    {
        WebPushEnvelope envelope = new();

        byte[] offCurvePoint = new byte[65];
        offCurvePoint[0] = 0x04;
        Array.Fill(offCurvePoint, (byte)0x01, 1, 32);
        Array.Fill(offCurvePoint, (byte)0x02, 33, 32);

        Assert.Throws<CryptographicException>(() =>
            envelope.Seal(
                [.. "x"u8],
                Base64UrlCodec.Encode(offCurvePoint),
                "AAAAAAAAAAAAAAAAAAAAAA"
            )
        );
    }

    [Fact]
    public void Seal_Accepts_The_Largest_Payload_That_Still_Fits_One_Record()
    {
        Vector vector = LoadVector();
        WebPushEnvelope envelope = new();
        byte[] plaintext = new byte[MaxPlaintextLength];

        byte[] sealedBody = envelope.Seal(plaintext, vector.UserAgentPublicKey, vector.AuthSecret);

        // header (salt 16 + rs 4 + idlen 1 + as_public 65 = 86) + one full rs-sized record
        Assert.Equal(86 + RecordSize, sealedBody.Length);
    }

    [Fact]
    public void Seal_Rejects_A_Payload_One_Byte_Too_Large_For_One_Record()
    {
        Vector vector = LoadVector();
        WebPushEnvelope envelope = new();
        byte[] plaintext = new byte[MaxPlaintextLength + 1];

        Assert.Throws<ArgumentException>(() =>
            envelope.Seal(plaintext, vector.UserAgentPublicKey, vector.AuthSecret)
        );
    }

    /// <summary>
    /// A realistic action set (approve/deny/view, the shape a device-approval or
    /// cast-request notification actually sends) still fits one aes128gcm record.
    /// Sealing throws on overflow and PushDispatchQueue swallows that exception,
    /// so a payload that stops fitting here would silently never arrive.
    /// </summary>
    [Fact]
    public void Seal_Accepts_A_PushPayload_With_A_Realistic_Number_Of_Actions()
    {
        Vector vector = LoadVector();
        WebPushEnvelope envelope = new();

        PushPayload payload = new(
            "New device sign-in request",
            "A new device is requesting access to your account. Approve or deny.",
            "/security/devices",
            "security-new-device",
            [
                new PushAction(
                    "approve",
                    "Approve",
                    "/security/devices/approve/3f9a7c2e-1234-4a3b-9abc-1234567890ab"
                ),
                new PushAction(
                    "deny",
                    "Deny",
                    "/security/devices/deny/3f9a7c2e-1234-4a3b-9abc-1234567890ab"
                ),
                new PushAction(
                    "view",
                    "View details",
                    "/security/devices/3f9a7c2e-1234-4a3b-9abc-1234567890ab"
                ),
            ]
        );

        byte[] plaintext = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

        byte[] sealedBody = envelope.Seal(plaintext, vector.UserAgentPublicKey, vector.AuthSecret);

        Assert.NotEmpty(sealedBody);
    }

    [Fact]
    public void Tampering_The_Last_Byte_Makes_Unsealing_Throw_Instead_Of_Returning_Partial_Plaintext()
    {
        Vector vector = LoadVector();
        byte[] validBody = Base64UrlCodec.Decode(vector.ExpectedCiphertext);

        byte[] recovered = UnsealAsTheDeviceWould(validBody, vector);
        Assert.Equal(vector.Plaintext, Encoding.UTF8.GetString(recovered));

        byte[] tamperedBody = (byte[])validBody.Clone();
        tamperedBody[^1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() =>
            UnsealAsTheDeviceWould(tamperedBody, vector)
        );
    }

    /// <summary>
    /// Delegates to <see cref="DeviceSideUnseal"/>, the from-scratch receiver-side
    /// decrypt ported from the Android client's WebPushUnsealer.kt, so the tamper
    /// assertion above proves AES-GCM's tag rejects a modified body against the
    /// same algorithm the real device runs — not merely that Seal agrees with
    /// itself.
    /// </summary>
    private static byte[] UnsealAsTheDeviceWould(byte[] body, Vector vector) =>
        DeviceSideUnseal.Unseal(
            body,
            vector.UserAgentPrivateKey,
            vector.UserAgentPublicKey,
            vector.AuthSecret
        );
}
