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

using System.Security.Claims;
using Microsoft.Extensions.Logging;
using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Capabilities;

namespace NoMercy.Plugins.Hooks;

/// <summary>
/// Collects ONLY additional claims from every active <see cref="IAuthPlugin"/> that
/// declares the <c>auth</c> capability. Never denies and never replaces identity —
/// Keycloak's own signature/issuer/audience validation is the sole authority on
/// whether a token is authenticated; a plugin only gets to enrich an already-valid
/// principal. A plugin that throws, hangs past <see cref="PerPluginTimeout"/>, omits
/// the capability, or tries to hand out a reserved (identity/role/scope/standard-JWT)
/// claim type is ignored/stripped so a broken or malicious plugin can never weaken
/// auth. This runs on every authenticated request/SignalR frame, so a hang must never
/// propagate past the per-plugin timeout.
/// </summary>
public class PluginClaimsAugmentor(IPluginManager pluginManager, ILogger<PluginClaimsAugmentor> logger)
    : IPluginClaimsAugmentor
{
    // Claim types a plugin can never forge, regardless of what it declares — identity,
    // role/scope, and the standard registered JWT claims. A plugin may only contribute
    // its OWN custom claim types (e.g. "plan"); enforced here so it holds even if a
    // future caller other than OnTokenValidated ever consumes this augmentor's output.
    private static readonly HashSet<string> ReservedClaimTypes = new(comparer: StringComparer.OrdinalIgnoreCase)
    {
        ClaimTypes.NameIdentifier,
        ClaimTypes.Role,
        "role",
        "scope",
        "sub",
        "aud",
        "iss",
        "exp",
        "iat",
        "nbf",
    };

    public TimeSpan PerPluginTimeout { get; init; } = TimeSpan.FromSeconds(seconds: 2);

    public async Task<IReadOnlyList<Claim>> CollectAdditionalClaimsAsync(string token, CancellationToken ct)
    {
        IReadOnlyList<PluginInfo> installed = pluginManager.GetInstalledPlugins();
        List<Claim> claims = [];

        foreach (IAuthPlugin plugin in pluginManager.GetPluginsOfType<IAuthPlugin>())
        {
            PluginCapabilities? capabilities = installed
                .FirstOrDefault(predicate: info => info.Id == plugin.Id)
                ?.Capabilities;

            if (!PluginCapabilityGuard.DeclaresHook(capabilities: capabilities, hook: PluginHookCapability.Auth))
                continue;

            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token: ct);
            timeoutCts.CancelAfter(delay: PerPluginTimeout);

            try
            {
                AuthResult result = await plugin.AuthenticateAsync(token: token, ct: timeoutCts.Token);
                if (!result.IsAuthenticated)
                    continue;

                foreach (KeyValuePair<string, string> claim in result.Claims)
                {
                    if (ReservedClaimTypes.Contains(item: claim.Key))
                        continue;

                    claims.Add(item: new Claim(type: claim.Key, value: claim.Value));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    exception: ex,
                    message: "Auth plugin {Plugin} failed or timed out; ignoring its claims (auth is never weakened).",
                    args: plugin.Id
                );
            }
        }

        return claims;
    }
}
