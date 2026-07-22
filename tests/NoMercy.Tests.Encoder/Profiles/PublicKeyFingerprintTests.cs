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
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

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
        Array.Fill(array: publicKey, value: (byte)0x01);

        string expected = Convert.ToHexString(inArray: SHA256.HashData(source: publicKey)).ToLowerInvariant();

        string actual = PublicKeyFingerprint.Compute(publicKeyBytes: publicKey);

        Assert.Equal(expected: expected, actual: actual);
    }

    [Fact]
    public void Compute_KnownInput_Is64Chars()
    {
        byte[] publicKey = new byte[32];
        string fingerprint = PublicKeyFingerprint.Compute(publicKeyBytes: publicKey);

        Assert.Equal(expected: 64, actual: fingerprint.Length);
    }

    [Fact]
    public void Compute_KnownInput_IsLowercase()
    {
        byte[] publicKey = new byte[32];
        Array.Fill(array: publicKey, value: (byte)0xFF);

        string fingerprint = PublicKeyFingerprint.Compute(publicKeyBytes: publicKey);

        Assert.Equal(expected: fingerprint, actual: fingerprint.ToLowerInvariant());
    }

    [Fact]
    public void Compute_AllZeros_PinsKnownValue()
    {
        // SHA-256(32 zero bytes) = pinned constant.
        // Computed externally: echo -n -e '\x00\x00...(32)' | sha256sum
        // Value: 66687aadf862bd776c8fc18b8e9f8e20089714856ee233b3902a591d0d5f2925
        byte[] publicKey = new byte[32];
        string fingerprint = PublicKeyFingerprint.Compute(publicKeyBytes: publicKey);

        Assert.Equal(
            expected: "66687aadf862bd776c8fc18b8e9f8e20089714856ee233b3902a591d0d5f2925",
            actual: fingerprint
        );
    }

    [Fact]
    public void Compute_DifferentKeys_ProduceDifferentFingerprints()
    {
        byte[] keyA = new byte[32];
        byte[] keyB = new byte[32];
        Array.Fill(array: keyB, value: (byte)0x42);

        string fpA = PublicKeyFingerprint.Compute(publicKeyBytes: keyA);
        string fpB = PublicKeyFingerprint.Compute(publicKeyBytes: keyB);

        Assert.NotEqual(expected: fpA, actual: fpB);
    }

    [Fact]
    public void Compute_SameKeyTwice_IsDeterministic()
    {
        byte[] publicKey = new byte[32];
        Array.Fill(array: publicKey, value: (byte)0xAB);

        string fp1 = PublicKeyFingerprint.Compute(publicKeyBytes: publicKey);
        string fp2 = PublicKeyFingerprint.Compute(publicKeyBytes: publicKey);

        Assert.Equal(expected: fp1, actual: fp2);
    }
}
