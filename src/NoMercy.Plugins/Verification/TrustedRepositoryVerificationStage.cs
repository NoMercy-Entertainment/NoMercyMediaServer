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

using NoMercy.Plugins.Abstractions;

namespace NoMercy.Plugins.Verification;

/// <summary>
/// A plugin listed by a repository the owner trusts does not need approving one
/// at a time.
/// <para>
/// Consent exists so a stranger's plugin cannot quietly start reaching the
/// network on first install. A plugin from an index the owner already added and
/// marked trusted is not a stranger, and asking about each one teaches them to
/// click through the prompt that is supposed to mean something.
/// </para>
/// <para>
/// Provenance, never self-description: the manifest's author line is free text
/// any file can copy, so it decides nothing here. Where the plugin came from is
/// the owner's own configuration and it is the only input this reads.
/// </para>
/// </summary>
/// <remarks>
/// Takes a resolver rather than the repository itself, and asks for it only when
/// a plugin is being verified. The catalogue needs an HTTP stack; the verifier
/// runs in hosts that have none — a test, an embedded use — and building it
/// eagerly turned "this host cannot fetch an index" into "this host cannot load
/// plugins at all".
/// </remarks>
public class TrustedRepositoryVerificationStage(Func<IPluginRepository?> repository)
    : IPluginVerificationStage
{
    public string Name => "trusted-repository";

    /// <summary>
    /// Never enforced. This stage grants trust and never withholds it, so a
    /// plugin no trusted index lists simply goes through the ordinary consent.
    /// </summary>
    public bool Enforced => false;

    public (PluginStageOutcome Outcome, string? Message) Evaluate(PluginVerificationContext context)
    {
        // No catalogue is not a reason to trust. It is a reason to ask.
        if (repository() is not { } catalogue)
            return (PluginStageOutcome.Pass, null);

        if (!catalogue.IsFromTrustedRepository(context.Manifest.Id))
            return (PluginStageOutcome.Pass, null);

        return (
            PluginStageOutcome.Trust,
            $"{context.Manifest.Name} is listed by a repository this server trusts."
        );
    }
}
