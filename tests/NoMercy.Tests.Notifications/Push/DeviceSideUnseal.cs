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
using NoMercy.Notifications.Push;

namespace NoMercy.Tests.Notifications.Push;

/// <summary>
/// A from-scratch decrypt of an aes128gcm body that shares no code with
/// WebPushEnvelope.Seal, ported line for line from the Android client's
/// WebPushUnsealer.kt (nomercy-app-kmp), including its exact header offsets
/// and its "strip trailing zeros, then require the preceding byte is the
/// 0x02 delimiter" padding rule. A test built on this fails when the two
/// implementations disagree, not merely when Seal disagrees with itself.
/// </summary>
internal static class DeviceSideUnseal
{
    // Mirrors WebPushUnsealer.kt's SALT_LENGTH / SERVER_KEY_LENGTH_OFFSET / SERVER_KEY_OFFSET.
    internal const int SaltLength = 16;
    internal const int ServerKeyLengthOffset = 20;
    internal const int ServerKeyOffset = 21;
    private const byte PaddingDelimiter = 0x02;

    internal static byte[] Unseal(
        byte[] body,
        string userAgentPrivateKeyBase64Url,
        string userAgentPublicKeyBase64Url,
        string authSecretBase64Url
    )
    {
        byte[] padded = DecryptPadded(
            body,
            userAgentPrivateKeyBase64Url,
            userAgentPublicKeyBase64Url,
            authSecretBase64Url
        );

        int delimiterIndex = LastIndexNotZero(padded);
        if (delimiterIndex < 0 || padded[delimiterIndex] != PaddingDelimiter)
        {
            throw new CryptographicException("aes128gcm record has no padding delimiter");
        }

        return padded[..delimiterIndex];
    }

    internal static byte[] DecryptPadded(
        byte[] body,
        string userAgentPrivateKeyBase64Url,
        string userAgentPublicKeyBase64Url,
        string authSecretBase64Url
    )
    {
        byte[] salt = body[..SaltLength];
        int keyIdLength = body[ServerKeyLengthOffset];
        byte[] senderPublic = body[ServerKeyOffset..(ServerKeyOffset + keyIdLength)];
        byte[] ciphertextAndTag = body[(ServerKeyOffset + keyIdLength)..];
        byte[] ciphertext = ciphertextAndTag[..^16];
        byte[] tag = ciphertextAndTag[^16..];

        byte[] receiverPrivate = Base64UrlCodec.Decode(userAgentPrivateKeyBase64Url);
        byte[] receiverPublic = Base64UrlCodec.Decode(userAgentPublicKeyBase64Url);
        byte[] authSecret = Base64UrlCodec.Decode(authSecretBase64Url);

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

        return padded;
    }

    private static int LastIndexNotZero(byte[] value)
    {
        for (int index = value.Length - 1; index >= 0; index--)
        {
            if (value[index] != 0)
            {
                return index;
            }
        }

        return -1;
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
