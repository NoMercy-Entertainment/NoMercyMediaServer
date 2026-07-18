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
using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Verification;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginVerifierTests
{
    private static string WriteTempDll(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), $"plugin-{Guid.NewGuid():N}.dll");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static PluginManifest Manifest(string? abi) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "n",
            Description = "d",
            Version = "1.0.0",
            Assembly = "x.dll",
            TargetAbi = abi,
        };

    [Fact]
    public void Verify_AbiMismatch_FailsAndNotVerified()
    {
        string dll = WriteTempDll([1, 2, 3]);
        PluginVerifier verifier = new();
        PluginVerificationResult result = verifier.Verify(Manifest("11.0"), dll, null);
        Assert.False(result.Verified);
        Assert.Contains(result.Failures, f => f.Contains("ABI"));
    }

    [Fact]
    public void Verify_ChecksumMatch_TrustedAndVerified()
    {
        byte[] bytes = [10, 20, 30, 40];
        string dll = WriteTempDll(bytes);
        string sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        PluginVerifier verifier = new();
        PluginVerificationResult result = verifier.Verify(Manifest("10.0"), dll, sha);
        Assert.True(result.Verified);
        Assert.True(result.Trusted);
    }

    [Fact]
    public void Verify_ChecksumMismatch_NotVerified()
    {
        string dll = WriteTempDll([1, 1, 1]);
        PluginVerifier verifier = new();
        PluginVerificationResult result = verifier.Verify(Manifest("10.0"), dll, "deadbeef");
        Assert.False(result.Verified);
        Assert.Contains(result.Failures, f => f.Contains("checksum"));
    }

    [Fact]
    public void Verify_NoExpectedChecksum_VerifiedButNotTrusted()
    {
        string dll = WriteTempDll([5, 5]);
        PluginVerifier verifier = new();
        PluginVerificationResult result = verifier.Verify(Manifest("10.0"), dll, null);
        Assert.True(result.Verified);
        Assert.False(result.Trusted);
    }
}
