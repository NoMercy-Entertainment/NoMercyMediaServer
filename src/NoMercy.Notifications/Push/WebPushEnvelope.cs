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
using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace NoMercy.Notifications.Push;

public class WebPushEnvelope : IWebPushEnvelope
{
    private const int SaltLength = 16;
    private const int KeyLength = 16;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int RecordSize = 4096;
    private const int PaddingDelimiterLength = 1;
    private const int MaxPlaintextLength = RecordSize - TagLength - PaddingDelimiterLength;
    private const string P256CurveOid = "1.2.840.10045.3.1.7";

    private static readonly BigInteger P256Prime = new(
        Convert.FromHexString("FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF"),
        isUnsigned: true,
        isBigEndian: true
    );

    private static readonly BigInteger P256B = new(
        Convert.FromHexString("5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B"),
        isUnsigned: true,
        isBigEndian: true
    );

    private readonly byte[]? _fixedSalt;
    private readonly byte[]? _fixedServerKey;

    public WebPushEnvelope()
        : this(null, null) { }

    /// <summary>
    /// Internal on purpose. A reused salt under a fixed server key is
    /// catastrophic for AES-GCM — the same key and nonce across two records
    /// leaks the XOR of both plaintexts and the authentication key. This seam
    /// exists only so the RFC 8291 vector can be reproduced in tests; production
    /// always takes the parameterless constructor and the random path.
    /// </summary>
    internal WebPushEnvelope(byte[]? fixedSalt, byte[]? fixedServerKey)
    {
        _fixedSalt = fixedSalt;
        _fixedServerKey = fixedServerKey;
    }

    public byte[] Seal(byte[] plaintext, string p256dhBase64Url, string authBase64Url)
    {
        if (plaintext.Length > MaxPlaintextLength)
        {
            throw new ArgumentException(
                $"Plaintext is {plaintext.Length} bytes, but RFC 8188's single "
                    + $"{RecordSize}-byte record (minus the padding delimiter and the "
                    + $"AEAD tag) holds at most {MaxPlaintextLength} bytes. A push message "
                    + "cannot span multiple records.",
                nameof(plaintext)
            );
        }

        byte[] userAgentPublic = Base64UrlCodec.Decode(p256dhBase64Url);
        byte[] authSecret = Base64UrlCodec.Decode(authBase64Url);

        using ECDiffieHellman serverKey = CreateServerKey();
        byte[] serverPublic = ExportUncompressedPoint(serverKey);

        using ECDiffieHellman userAgentKey = ImportPublicPoint(userAgentPublic);
        byte[] sharedSecret = serverKey.DeriveRawSecretAgreement(userAgentKey.PublicKey);

        byte[] salt = _fixedSalt ?? RandomNumberGenerator.GetBytes(SaltLength);

        byte[] prkInfo = BuildKeyInfo(userAgentPublic, serverPublic);
        byte[] ikm = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            sharedSecret,
            32,
            authSecret,
            prkInfo
        );
        CryptographicOperations.ZeroMemory(sharedSecret);

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
        CryptographicOperations.ZeroMemory(ikm);

        byte[] padded = new byte[plaintext.Length + PaddingDelimiterLength];
        plaintext.CopyTo(padded, 0);
        padded[^1] = 0x02;

        byte[] ciphertext = new byte[padded.Length];
        byte[] tag = new byte[TagLength];
        try
        {
            using (AesGcm aes = new(contentKey, TagLength))
            {
                aes.Encrypt(nonce, padded, ciphertext, tag);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(contentKey);
            CryptographicOperations.ZeroMemory(padded);
        }

        return BuildBody(salt, serverPublic, ciphertext, tag);
    }

    private ECDiffieHellman CreateServerKey() =>
        _fixedServerKey is null
            ? ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256)
            : ImportPrivateScalar(_fixedServerKey);

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

        byte[] x = uncompressed[1..33];
        byte[] y = uncompressed[33..65];
        EnsureOnP256Curve(x, y);

        ECDiffieHellman key = ECDiffieHellman.Create();
        key.ImportParameters(
            new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = x, Y = y },
            }
        );
        return key;
    }

    /// <summary>
    /// Whether the underlying platform provider rejects an off-curve point on
    /// import is not portable — confirmed to throw on Windows/CNG, unconfirmed
    /// elsewhere — and an unvalidated point here is the classic invalid-curve
    /// attack against Web Push. Checking y^2 = x^3 - 3x + b (mod p) ourselves
    /// makes rejection independent of which OS this runs on.
    /// </summary>
    private static void EnsureOnP256Curve(byte[] x, byte[] y)
    {
        BigInteger xCoordinate = new(x, isUnsigned: true, isBigEndian: true);
        BigInteger yCoordinate = new(y, isUnsigned: true, isBigEndian: true);

        BigInteger left = BigInteger.ModPow(yCoordinate, 2, P256Prime);
        BigInteger right =
            (
                (BigInteger.ModPow(xCoordinate, 3, P256Prime) - 3 * xCoordinate + P256B) % P256Prime
                + P256Prime
            ) % P256Prime;

        if (left != right)
        {
            throw new CryptographicException("Public key point is not on the P-256 curve");
        }
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
        Span<byte> recordSize = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(recordSize, RecordSize);

        using MemoryStream body = new();
        body.Write(salt);
        body.Write(recordSize);
        body.WriteByte((byte)serverPublic.Length);
        body.Write(serverPublic);
        body.Write(ciphertext);
        body.Write(tag);
        return body.ToArray();
    }
}
