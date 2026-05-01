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
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        AsymmetricCipherKeyPair pair = generator.GenerateKeyPair();
        return ((Ed25519PublicKeyParameters)pair.Public, (Ed25519PrivateKeyParameters)pair.Private);
    }

    /// <summary>Computes the fingerprint (lowercase hex SHA-256) of the public key bytes.</summary>
    private static string Fingerprint(Ed25519PublicKeyParameters pubKey)
    {
        byte[] pubBytes = pubKey.GetEncoded();
        byte[] hash = SHA256.HashData(pubBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Signs SHA-256(json) with the given private key and returns the base64 signature.</summary>
    private static string Sign(string json, Ed25519PrivateKeyParameters privateKey)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        Ed25519Signer signer = new();
        signer.Init(true, privateKey);
        signer.BlockUpdate(digest, 0, digest.Length);
        return Convert.ToBase64String(signer.GenerateSignature());
    }

    /// <summary>Builds a TrustedPublisherKey record from a BouncyCastle public key.</summary>
    private static TrustedPublisherKey MakeTrustedKey(Ed25519PublicKeyParameters pubKey)
    {
        return new TrustedPublisherKey
        {
            Fingerprint = Fingerprint(pubKey),
            Label = "Test Publisher",
            PublicKeyBase64 = Convert.ToBase64String(pubKey.GetEncoded()),
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
        string fingerprint = Fingerprint(pubKey);
        string sig = Sign(json, privKey);
        TrustedPublisherKey trustedKey = MakeTrustedKey(pubKey);

        EncoderRule? result = _verifier.Verify(
            json,
            fingerprint,
            sig,
            fp => fp == fingerprint ? trustedKey : null
        );

        result.Should().BeNull();
    }

    [Fact]
    public void Verify_returns_PublisherUntrusted_when_fingerprint_unknown()
    {
        string json = """{"name":"Test"}""";

        EncoderRule? result = _verifier.Verify(
            json,
            "deadbeef",
            Convert.ToBase64String(new byte[64]),
            _ => null
        );

        result.Should().NotBeNull();
        result!.Id.Should().Be(EncoderRuleId.ImportPublisherUntrusted);
    }

    [Fact]
    public void Verify_returns_SignatureInvalid_when_signature_corrupt()
    {
        (Ed25519PublicKeyParameters pubKey, Ed25519PrivateKeyParameters privKey) =
            GenerateKeyPair();
        string json = """{"name":"Test","format":"Hls"}""";
        string fingerprint = Fingerprint(pubKey);
        string sig = Sign(json, privKey);
        TrustedPublisherKey trustedKey = MakeTrustedKey(pubKey);

        // Flip a byte in the middle of the signature
        byte[] sigBytes = Convert.FromBase64String(sig);
        sigBytes[32] ^= 0xFF;
        string corruptedSig = Convert.ToBase64String(sigBytes);

        EncoderRule? result = _verifier.Verify(
            json,
            fingerprint,
            corruptedSig,
            fp => fp == fingerprint ? trustedKey : null
        );

        result.Should().NotBeNull();
        result!.Id.Should().Be(EncoderRuleId.ImportSignatureInvalid);
    }

    [Fact]
    public void Verify_returns_SignatureInvalid_when_payload_tampered()
    {
        (Ed25519PublicKeyParameters pubKey, Ed25519PrivateKeyParameters privKey) =
            GenerateKeyPair();
        string originalJson = """{"name":"Legit","format":"Hls"}""";
        string tamperedJson = """{"name":"Tampered","format":"Hls"}""";
        string fingerprint = Fingerprint(pubKey);
        string sig = Sign(originalJson, privKey);
        TrustedPublisherKey trustedKey = MakeTrustedKey(pubKey);

        // Verify the tampered payload against the original signature
        EncoderRule? result = _verifier.Verify(
            tamperedJson,
            fingerprint,
            sig,
            fp => fp == fingerprint ? trustedKey : null
        );

        result.Should().NotBeNull();
        result!.Id.Should().Be(EncoderRuleId.ImportSignatureInvalid);
    }
}
