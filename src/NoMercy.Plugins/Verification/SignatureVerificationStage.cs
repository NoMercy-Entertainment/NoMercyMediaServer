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

namespace NoMercy.Plugins.Verification;

public class SignatureVerificationStage : IPluginVerificationStage
{
    public string Name => "Signature";
    public bool Enforced => false;

    public (PluginStageOutcome Outcome, string? Message) Evaluate(PluginVerificationContext context)
    {
        return (PluginStageOutcome.Pass, null);
    }
}
