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

namespace NoMercy.Notifications.Push;

/// <summary>
/// The salt and server key are injectable only so the RFC 8291 example can be
/// reproduced exactly. Production always takes the random path.
/// </summary>
public class WebPushEnvelope(byte[]? fixedSalt = null, byte[]? fixedServerKey = null)
    : IWebPushEnvelope
{
    private const int SaltLength = 16;
    private const int KeyLength = 16;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int RecordSize = 4096;
    private const string P256CurveOid = "1.2.840.10045.3.1.7";

    public byte[] Seal(byte[] plaintext, string p256dhBase64Url, string authBase64Url)
    {
        byte[] userAgentPublic = DecodeBase64Url(p256dhBase64Url);
        byte[] authSecret = DecodeBase64Url(authBase64Url);

        using ECDiffieHellman serverKey = CreateServerKey();
        byte[] serverPublic = ExportUncompressedPoint(serverKey);

        using ECDiffieHellman userAgentKey = ImportPublicPoint(userAgentPublic);
        byte[] sharedSecret = serverKey.DeriveRawSecretAgreement(userAgentKey.PublicKey);

        byte[] salt = fixedSalt ?? RandomNumberGenerator.GetBytes(SaltLength);

        byte[] prkInfo = BuildKeyInfo(userAgentPublic, serverPublic);
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
            KeyLength,
            salt,
            Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0")
        );
        byte[] nonce = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm,
            NonceLength,
            salt,
            Encoding.ASCII.GetBytes("Content-Encoding: nonce\0")
        );

        byte[] padded = new byte[plaintext.Length + 1];
        plaintext.CopyTo(padded, 0);
        padded[^1] = 0x02;

        byte[] ciphertext = new byte[padded.Length];
        byte[] tag = new byte[TagLength];
        using (AesGcm aes = new(contentKey, TagLength))
        {
            aes.Encrypt(nonce, padded, ciphertext, tag);
        }

        return BuildBody(salt, serverPublic, ciphertext, tag);
    }

    private ECDiffieHellman CreateServerKey() =>
        fixedServerKey is null
            ? ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256)
            : ImportPrivateScalar(fixedServerKey);

    /// <summary>
    /// .NET has no direct API to compute a P-256 public point from a raw private
    /// scalar. Round-tripping the scalar through ImportParameters/ExportECPrivateKey
    /// with a mismatched Q does not recompute it either — the provider keeps
    /// whatever (wrong) Q it started with, which was verified against the RFC 8291
    /// vector before landing here. A SEC1 ECPrivateKey with the optional public-key
    /// field left out forces the provider to derive Q from D itself; that is the
    /// only path confirmed to reproduce the RFC's byte-identical output.
    /// </summary>
    private static ECDiffieHellman ImportPrivateScalar(byte[] d)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(1);
            writer.WriteOctetString(d);
            using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true)))
            {
                writer.WriteObjectIdentifier(P256CurveOid);
            }
        }

        ECDiffieHellman key = ECDiffieHellman.Create();
        key.ImportECPrivateKey(writer.Encode(), out _);
        return key;
    }

    private static ECDiffieHellman ImportPublicPoint(byte[] uncompressed)
    {
        if (uncompressed.Length != 65 || uncompressed[0] != 0x04)
        {
            throw new CryptographicException("Public key is not an uncompressed P-256 point");
        }

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

    private static byte[] ExportUncompressedPoint(ECDiffieHellman key)
    {
        ECParameters parameters = key.ExportParameters(false);
        byte[] point = new byte[65];
        point[0] = 0x04;
        parameters.Q.X!.CopyTo(point, 1);
        parameters.Q.Y!.CopyTo(point, 33);
        return point;
    }

    private static byte[] BuildKeyInfo(byte[] userAgentPublic, byte[] serverPublic)
    {
        byte[] label = Encoding.ASCII.GetBytes("WebPush: info\0");
        byte[] info = new byte[label.Length + userAgentPublic.Length + serverPublic.Length];
        label.CopyTo(info, 0);
        userAgentPublic.CopyTo(info, label.Length);
        serverPublic.CopyTo(info, label.Length + userAgentPublic.Length);
        return info;
    }

    private static byte[] BuildBody(byte[] salt, byte[] serverPublic, byte[] ciphertext, byte[] tag)
    {
        using MemoryStream body = new();
        body.Write(salt);
        body.Write(
            BitConverter.IsLittleEndian
                ? BitConverter.GetBytes(RecordSize).Reverse().ToArray()
                : BitConverter.GetBytes(RecordSize)
        );
        body.WriteByte((byte)serverPublic.Length);
        body.Write(serverPublic);
        body.Write(ciphertext);
        body.Write(tag);
        return body.ToArray();
    }

    public static byte[] DecodeBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - (padded.Length % 4)) % 4);
        return Convert.FromBase64String(padded);
    }

    public static string EncodeBase64Url(byte[] value) =>
        Convert.ToBase64String(value).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
