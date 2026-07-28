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

using System.Formats.Asn1;
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

        byte[] first = envelope.Seal(
            "same"u8.ToArray(),
            vector.UserAgentPublicKey,
            vector.AuthSecret
        );
        byte[] second = envelope.Seal(
            "same"u8.ToArray(),
            vector.UserAgentPublicKey,
            vector.AuthSecret
        );

        Assert.NotEqual(Convert.ToBase64String(first), Convert.ToBase64String(second));
    }

    [Fact]
    public void Seal_Rejects_A_Public_Key_With_The_Wrong_Length_Or_Prefix()
    {
        WebPushEnvelope envelope = new();

        Assert.ThrowsAny<Exception>(() =>
            envelope.Seal("x"u8.ToArray(), "BAAAAAAAAAAAAAAAAAAA", "AAAAAAAAAAAAAAAAAAAAAA")
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
                "x"u8.ToArray(),
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
    /// A from-scratch receiver-side decrypt that shares no code with
    /// WebPushEnvelope.Seal, so the tamper assertion above proves AES-GCM's tag
    /// rejects a modified body — not merely that Seal agrees with itself.
    /// </summary>
    private static byte[] UnsealAsTheDeviceWould(byte[] body, Vector vector)
    {
        byte[] salt = body[..16];
        int keyIdLength = body[20];
        byte[] senderPublic = body[21..(21 + keyIdLength)];
        byte[] ciphertextAndTag = body[(21 + keyIdLength)..];
        byte[] ciphertext = ciphertextAndTag[..^16];
        byte[] tag = ciphertextAndTag[^16..];

        byte[] receiverPrivate = Base64UrlCodec.Decode(vector.UserAgentPrivateKey);
        byte[] receiverPublic = Base64UrlCodec.Decode(vector.UserAgentPublicKey);
        byte[] authSecret = Base64UrlCodec.Decode(vector.AuthSecret);

        using ECDiffieHellman receiverKey = ImportPrivateScalar(receiverPrivate);
        using ECDiffieHellman senderKey = ImportPublicPoint(senderPublic);
        byte[] sharedSecret = receiverKey.DeriveRawSecretAgreement(senderKey.PublicKey);

        byte[] label = Encoding.ASCII.GetBytes("WebPush: info\0");
        byte[] prkInfo = new byte[label.Length + receiverPublic.Length + senderPublic.Length];
        label.CopyTo(prkInfo, 0);
        receiverPublic.CopyTo(prkInfo, label.Length);
        senderPublic.CopyTo(prkInfo, label.Length + receiverPublic.Length);

        byte[] ikm = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            sharedSecret,
            32,
            authSecret,
            prkInfo
        );
        byte[] contentKey = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm,
            16,
            salt,
            Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0")
        );
        byte[] nonce = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm,
            12,
            salt,
            Encoding.ASCII.GetBytes("Content-Encoding: nonce\0")
        );

        byte[] padded = new byte[ciphertext.Length];
        using (AesGcm aes = new(contentKey, 16))
        {
            aes.Decrypt(nonce, ciphertext, tag, padded);
        }

        return padded[..^1];
    }

    private static ECDiffieHellman ImportPrivateScalar(byte[] d)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(1);
            writer.WriteOctetString(d);
            using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true)))
            {
                writer.WriteObjectIdentifier("1.2.840.10045.3.1.7");
            }
        }

        ECDiffieHellman key = ECDiffieHellman.Create();
        key.ImportECPrivateKey(writer.Encode(), out _);
        return key;
    }

    private static ECDiffieHellman ImportPublicPoint(byte[] uncompressed)
    {
        ECDiffieHellman key = ECDiffieHellman.Create();
        key.ImportParameters(
            new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = uncompressed[1..33], Y = uncompressed[33..65] },
            }
        );
        return key;
    }
}
