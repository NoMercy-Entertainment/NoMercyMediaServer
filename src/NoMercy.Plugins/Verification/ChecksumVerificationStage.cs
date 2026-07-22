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

namespace NoMercy.Plugins.Verification;

public class ChecksumVerificationStage : IPluginVerificationStage
{
    public string Name => "Checksum";
    public bool Enforced => true;

    public (PluginStageOutcome Outcome, string? Message) Evaluate(PluginVerificationContext context)
    {
        if (string.IsNullOrWhiteSpace(value: context.ExpectedChecksum))
            return (PluginStageOutcome.Pass, null);

        byte[] bytes = File.ReadAllBytes(path: context.AssemblyPath);
        string actual = Convert.ToHexString(inArray: SHA256.HashData(source: bytes)).ToLowerInvariant();

        if (string.Equals(a: actual, b: context.ExpectedChecksum, comparisonType: StringComparison.OrdinalIgnoreCase))
            return (PluginStageOutcome.Trust, null);

        return (
            PluginStageOutcome.Fail,
            $"Assembly checksum mismatch: expected {context.ExpectedChecksum}, got {actual}."
        );
    }
}
