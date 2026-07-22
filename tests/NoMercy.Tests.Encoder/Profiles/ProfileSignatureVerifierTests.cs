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
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace NoMercy.Tests.Encoder.Profiles;

public class ProfileSignatureVerifierTests
{
    private readonly ProfileSignatureVerifier _verifier = new();

    // -------------------------------------------------------------------------
    // Key generation helpers
    // -------------------------------------------------------------------------

    private static (
        Ed25519PublicKeyParameters PublicKey,
        Ed25519PrivateKeyParameters PrivateKey
    ) GenerateKeyPair()
    {
        Ed25519KeyPairGenerator generator = new();
        generator.Init(parameters: new Ed25519KeyGenerationParameters(random: new()));
        AsymmetricCipherKeyPair pair = generator.GenerateKeyPair();
        return ((Ed25519PublicKeyParameters)pair.Public, (Ed25519PrivateKeyParameters)pair.Private);
    }

    /// <summary>Computes the fingerprint (lowercase hex SHA-256) of the public key bytes.</summary>
    private static string Fingerprint(Ed25519PublicKeyParameters pubKey)
    {
        byte[] pubBytes = pubKey.GetEncoded();
        byte[] hash = SHA256.HashData(source: pubBytes);
        return Convert.ToHexString(inArray: hash).ToLowerInvariant();
    }

    /// <summary>Signs SHA-256(json) with the given private key and returns the base64 signature.</summary>
    private static string Sign(string json, Ed25519PrivateKeyParameters privateKey)
    {
        byte[] digest = SHA256.HashData(source: Encoding.UTF8.GetBytes(s: json));
        Ed25519Signer signer = new();
        signer.Init(forSigning: true, parameters: privateKey);
        signer.BlockUpdate(buf: digest, off: 0, len: digest.Length);
        return Convert.ToBase64String(inArray: signer.GenerateSignature());
    }

    /// <summary>Builds a TrustedPublisherKey record from a BouncyCastle public key.</summary>
    private static TrustedPublisherKey MakeTrustedKey(Ed25519PublicKeyParameters pubKey)
    {
        return new()
        {
            Fingerprint = Fingerprint(pubKey: pubKey),
            Label = "Test Publisher",
            PublicKeyBase64 = Convert.ToBase64String(inArray: pubKey.GetEncoded()),
            AddedAt = DateTime.UtcNow,
            AddedBy = "test",
        };
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Fact]
    public void Verify_returns_null_for_correctly_signed_profile()
    {
        (Ed25519PublicKeyParameters pubKey, Ed25519PrivateKeyParameters privKey) =
            GenerateKeyPair();
        string json = """{"name":"Test","format":"Hls"}""";
        string fingerprint = Fingerprint(pubKey: pubKey);
        string sig = Sign(json: json, privateKey: privKey);
        TrustedPublisherKey trustedKey = MakeTrustedKey(pubKey: pubKey);

        EncoderRule? result = _verifier.Verify(
            profileJson: json,
            fingerprint: fingerprint,
            base64Signature: sig,
            keyLookup: fp => fp == fingerprint ? trustedKey : null
        );

        result.Should().BeNull();
    }

    [Fact]
    public void Verify_returns_PublisherUntrusted_when_fingerprint_unknown()
    {
        string json = """{"name":"Test"}""";

        EncoderRule? result = _verifier.Verify(
            profileJson: json,
            fingerprint: "deadbeef",
            base64Signature: Convert.ToBase64String(inArray: new byte[64]),
            keyLookup: _ => null
        );

        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: EncoderRuleId.ImportPublisherUntrusted);
    }

    [Fact]
    public void Verify_returns_SignatureInvalid_when_signature_corrupt()
    {
        (Ed25519PublicKeyParameters pubKey, Ed25519PrivateKeyParameters privKey) =
            GenerateKeyPair();
        string json = """{"name":"Test","format":"Hls"}""";
        string fingerprint = Fingerprint(pubKey: pubKey);
        string sig = Sign(json: json, privateKey: privKey);
        TrustedPublisherKey trustedKey = MakeTrustedKey(pubKey: pubKey);

        // Flip a byte in the middle of the signature
        byte[] sigBytes = Convert.FromBase64String(s: sig);
        sigBytes[32] ^= 0xFF;
        string corruptedSig = Convert.ToBase64String(inArray: sigBytes);

        EncoderRule? result = _verifier.Verify(
            profileJson: json,
            fingerprint: fingerprint,
            base64Signature: corruptedSig,
            keyLookup: fp => fp == fingerprint ? trustedKey : null
        );

        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: EncoderRuleId.ImportSignatureInvalid);
    }

    [Fact]
    public void Verify_returns_SignatureInvalid_when_payload_tampered()
    {
        (Ed25519PublicKeyParameters pubKey, Ed25519PrivateKeyParameters privKey) =
            GenerateKeyPair();
        string originalJson = """{"name":"Legit","format":"Hls"}""";
        string tamperedJson = """{"name":"Tampered","format":"Hls"}""";
        string fingerprint = Fingerprint(pubKey: pubKey);
        string sig = Sign(json: originalJson, privateKey: privKey);
        TrustedPublisherKey trustedKey = MakeTrustedKey(pubKey: pubKey);

        // Verify the tampered payload against the original signature
        EncoderRule? result = _verifier.Verify(
            profileJson: tamperedJson,
            fingerprint: fingerprint,
            base64Signature: sig,
            keyLookup: fp => fp == fingerprint ? trustedKey : null
        );

        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: EncoderRuleId.ImportSignatureInvalid);
    }
}
