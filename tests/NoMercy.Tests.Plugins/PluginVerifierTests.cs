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
        string path = Path.Combine(path1: Path.GetTempPath(), path2: $"plugin-{Guid.NewGuid():N}.dll");
        File.WriteAllBytes(path: path, bytes: bytes);
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
        string dll = WriteTempDll(bytes: [1, 2, 3]);
        PluginVerifier verifier = new();
        PluginVerificationResult result = verifier.Verify(manifest: Manifest(abi: "11.0"), assemblyPath: dll, expectedChecksum: null);
        Assert.False(condition: result.Verified);
        Assert.Contains(collection: result.Failures, filter: f => f.Contains(value: "ABI"));
    }

    [Fact]
    public void Verify_ChecksumMatch_TrustedAndVerified()
    {
        byte[] bytes = [10, 20, 30, 40];
        string dll = WriteTempDll(bytes: bytes);
        string sha = Convert.ToHexString(inArray: SHA256.HashData(source: bytes)).ToLowerInvariant();
        PluginVerifier verifier = new();
        PluginVerificationResult result = verifier.Verify(manifest: Manifest(abi: "10.0"), assemblyPath: dll, expectedChecksum: sha);
        Assert.True(condition: result.Verified);
        Assert.True(condition: result.Trusted);
    }

    [Fact]
    public void Verify_ChecksumMismatch_NotVerified()
    {
        string dll = WriteTempDll(bytes: [1, 1, 1]);
        PluginVerifier verifier = new();
        PluginVerificationResult result = verifier.Verify(manifest: Manifest(abi: "10.0"), assemblyPath: dll, expectedChecksum: "deadbeef");
        Assert.False(condition: result.Verified);
        Assert.Contains(collection: result.Failures, filter: f => f.Contains(value: "checksum"));
    }

    [Fact]
    public void Verify_NoExpectedChecksum_VerifiedButNotTrusted()
    {
        string dll = WriteTempDll(bytes: [5, 5]);
        PluginVerifier verifier = new();
        PluginVerificationResult result = verifier.Verify(manifest: Manifest(abi: "10.0"), assemblyPath: dll, expectedChecksum: null);
        Assert.True(condition: result.Verified);
        Assert.False(condition: result.Trusted);
    }

    [Fact]
    public void Verify_NonEnforcedStageFails_DoesNotFailOverallVerification()
    {
        // A stage that is NOT enforced (Enforced == false) must never contribute
        // its failure to the verdict — the `stage.Enforced` half of the
        // `outcome == Fail && stage.Enforced` guard exists specifically to keep
        // an advisory-only stage from ever blocking a plugin load.
        string dll = WriteTempDll(bytes: [1, 2, 3]);
        PluginVerifier verifier = new(stages: [new AdvisoryFailingStage()]);

        PluginVerificationResult result = verifier.Verify(manifest: Manifest(abi: "10.0"), assemblyPath: dll, expectedChecksum: null);

        Assert.True(condition: result.Verified);
        Assert.Empty(collection: result.Failures);
    }

    [Fact]
    public void Verify_EnforcedStageFailsWithNullMessage_FallsBackToStageNameMessage()
    {
        // Every other failing-stage test (Abi, Checksum) supplies a real
        // message — this is the only path that reaches the
        // `message ?? $"{stage.Name} stage failed."` fallback on the right of
        // the `??`.
        string dll = WriteTempDll(bytes: [9, 9, 9]);
        PluginVerifier verifier = new(stages: [new EnforcedFailingStageWithNoMessage()]);

        PluginVerificationResult result = verifier.Verify(manifest: Manifest(abi: "10.0"), assemblyPath: dll, expectedChecksum: null);

        Assert.False(condition: result.Verified);
        Assert.Equal(expected: "NoMessage stage failed.", actual: Assert.Single(collection: result.Failures));
    }

    [Fact]
    public void AbiVerificationStage_ExposesNameAndEnforced()
    {
        AbiVerificationStage stage = new();

        Assert.Equal(expected: "ABI", actual: stage.Name);
        Assert.True(condition: stage.Enforced);
    }

    [Fact]
    public void ChecksumVerificationStage_ExposesNameAndEnforced()
    {
        ChecksumVerificationStage stage = new();

        Assert.Equal(expected: "Checksum", actual: stage.Name);
        Assert.True(condition: stage.Enforced);
    }

    [Fact]
    public void SignatureVerificationStage_ExposesNameAndIsNotEnforced()
    {
        SignatureVerificationStage stage = new();

        Assert.Equal(expected: "Signature", actual: stage.Name);
        Assert.False(condition: stage.Enforced);
    }

    [Fact]
    public void SignatureVerificationStage_Evaluate_AlwaysPasses()
    {
        SignatureVerificationStage stage = new();
        PluginVerificationContext context = new()
        {
            Manifest = Manifest(abi: "10.0"),
            AssemblyPath = "irrelevant",
            ExpectedChecksum = null,
        };

        (PluginStageOutcome outcome, string? message) = stage.Evaluate(context: context);

        Assert.Equal(expected: PluginStageOutcome.Pass, actual: outcome);
        Assert.Null(@object: message);
    }

    private sealed class AdvisoryFailingStage : IPluginVerificationStage
    {
        public string Name => "Advisory";
        public bool Enforced => false;

        public (PluginStageOutcome Outcome, string? Message) Evaluate(
            PluginVerificationContext context
        ) => (PluginStageOutcome.Fail, "advisory failure, never enforced");
    }

    private sealed class EnforcedFailingStageWithNoMessage : IPluginVerificationStage
    {
        public string Name => "NoMessage";
        public bool Enforced => true;

        public (PluginStageOutcome Outcome, string? Message) Evaluate(
            PluginVerificationContext context
        ) => (PluginStageOutcome.Fail, null);
    }
}
