namespace NoMercy.Tests.Encoder.Profiles;

using System.Security.Cryptography;
using NoMercy.Encoder.Profiles;

/// <summary>
/// Pins the fingerprint algorithm to SHA-256 lowercase hex so drift is caught
/// immediately — any change to the computation would break all stored keys.
/// </summary>
public class PublicKeyFingerprintTests
{
    [Fact]
    public void Compute_KnownInput_MatchesSha256Hex()
    {
        // 32 bytes of deterministic input (all 0x01).
        byte[] publicKey = new byte[32];
        Array.Fill(publicKey, (byte)0x01);

        string expected = Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();

        string actual = PublicKeyFingerprint.Compute(publicKey);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Compute_KnownInput_Is64Chars()
    {
        byte[] publicKey = new byte[32];
        string fingerprint = PublicKeyFingerprint.Compute(publicKey);

        Assert.Equal(64, fingerprint.Length);
    }

    [Fact]
    public void Compute_KnownInput_IsLowercase()
    {
        byte[] publicKey = new byte[32];
        Array.Fill(publicKey, (byte)0xFF);

        string fingerprint = PublicKeyFingerprint.Compute(publicKey);

        Assert.Equal(fingerprint, fingerprint.ToLowerInvariant());
    }

    [Fact]
    public void Compute_AllZeros_PinsKnownValue()
    {
        // SHA-256(32 zero bytes) = pinned constant.
        // Computed externally: echo -n -e '\x00\x00...(32)' | sha256sum
        // Value: 66687aadf862bd776c8fc18b8e9f8e20089714856ee233b3902a591d0d5f2925
        byte[] publicKey = new byte[32];
        string fingerprint = PublicKeyFingerprint.Compute(publicKey);

        Assert.Equal(
            "66687aadf862bd776c8fc18b8e9f8e20089714856ee233b3902a591d0d5f2925",
            fingerprint
        );
    }

    [Fact]
    public void Compute_DifferentKeys_ProduceDifferentFingerprints()
    {
        byte[] keyA = new byte[32];
        byte[] keyB = new byte[32];
        Array.Fill(keyB, (byte)0x42);

        string fpA = PublicKeyFingerprint.Compute(keyA);
        string fpB = PublicKeyFingerprint.Compute(keyB);

        Assert.NotEqual(fpA, fpB);
    }

    [Fact]
    public void Compute_SameKeyTwice_IsDeterministic()
    {
        byte[] publicKey = new byte[32];
        Array.Fill(publicKey, (byte)0xAB);

        string fp1 = PublicKeyFingerprint.Compute(publicKey);
        string fp2 = PublicKeyFingerprint.Compute(publicKey);

        Assert.Equal(fp1, fp2);
    }
}
