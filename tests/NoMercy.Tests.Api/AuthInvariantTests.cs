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

using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using NoMercy.NmSystem.Configuration;
using NoMercy.Plugins.Abstractions;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Proves the "auth is never weakened" invariant for the plugin claims-augmentation
/// wired into OnTokenValidated (ServiceConfiguration.Auth.cs): a plugin can only
/// enrich an ALREADY-accepted principal, never decide acceptance. Unlike every other
/// fixture in this project (which authenticates via TestAuthHandler, a header-driven
/// fake scheme), this exercises the REAL Keycloak JwtBearer pipeline — the exact
/// JwtBearerOptions/Events instance ConfigureAuth registers in production.
/// </summary>
[Trait(name: "Category", value: "Authorization")]
public sealed class AuthInvariantTests
{
    [Fact]
    public async Task AnonymousEndpoint_StillReturnsOk_UnderTheRealAuthPipeline()
    {
        // Sanity check for the two 401 tests below: confirms 401 there comes from
        // real auth rejection, not from some unrelated middleware blanket-denying
        // every request once TestAuthHandler is out of the picture.
        await using RealAuthApiFactory factory = new();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(requestUri: "/status");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    [Fact]
    public async Task InvalidToken_StillReturns401_EvenWithAMaliciousAuthPluginInstalled()
    {
        await using RealAuthApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            scheme: "Bearer",
            parameter: "this.is-not.a-valid-jwt"
        );

        HttpResponseMessage response = await client.GetAsync(requestUri: "/api/v1/setup/permissions");

        Assert.Equal(expected: HttpStatusCode.Unauthorized, actual: response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_StillReturns401_EvenWithAMaliciousAuthPluginInstalled()
    {
        await using RealAuthApiFactory factory = new();
        HttpClient client = factory.CreateClient();

        // Structurally well-formed (three base64url segments, real claim shape) but
        // unsigned and long expired — SecurityTokenValidator must reject it on
        // signature/expiry before OnTokenValidated ever runs.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            scheme: "Bearer",
            parameter: ExpiredUnsignedJwt()
        );

        HttpResponseMessage response = await client.GetAsync(requestUri: "/api/v1/setup/permissions");

        Assert.Equal(expected: HttpStatusCode.Unauthorized, actual: response.StatusCode);
    }

    [Fact]
    public async Task OnTokenValidated_OnlyAddsClaims_NeverFailsOrReplacesThePrincipal()
    {
        // This is the real OnTokenValidated delegate ConfigureAuth wires in
        // production, resolved from the real DI container — not a re-implementation.
        // JwtBearerHandler invokes it ONLY after signature/issuer/audience validation
        // already succeeded (framework contract, proven separately by the two 401
        // tests above). This test proves that once invoked, it stays additive-only,
        // and that the reserved-claim deny-list holds through the REAL pipeline, not
        // just the isolated PluginClaimsAugmentor unit tests.
        await using RealAuthApiFactory factory = new();
        IServiceProvider rootServices = factory.Services;

        JwtBearerOptions options = rootServices
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(name: JwtBearerDefaults.AuthenticationScheme);

        Claim originalClaim = new(type: ClaimTypes.NameIdentifier, value: Guid.NewGuid().ToString());
        ClaimsIdentity identity = new(claims: [originalClaim], authenticationType: JwtBearerDefaults.AuthenticationScheme);
        ClaimsPrincipal principal = new(identity: identity);

        using IServiceScope scope = rootServices.CreateScope();
        DefaultHttpContext httpContext = new() { RequestServices = scope.ServiceProvider };
        AuthenticationScheme scheme = new(
            name: JwtBearerDefaults.AuthenticationScheme,
            displayName: null,
            handlerType: typeof(JwtBearerHandler)
        );

        TokenValidatedContext context = new(context: httpContext, scheme: scheme, options: options)
        {
            Principal = principal,
            SecurityToken = new JsonWebToken(jwtEncodedString: UnsignedJwt()),
        };

        await options.Events!.OnTokenValidated!(arg: context);

        Assert.Null(@object: context.Result);
        Assert.Same(expected: principal, actual: context.Principal);
        Assert.Contains(
            collection: identity.Claims,
            filter: c => c.Type == ClaimTypes.NameIdentifier && c.Value == originalClaim.Value
        );
        Assert.Contains(collection: identity.Claims, filter: c => c is { Type: "plan", Value: "pro" });
        Assert.DoesNotContain(collection: identity.Claims, filter: c => c.Type == ClaimTypes.Role);
        Assert.DoesNotContain(collection: identity.Claims, filter: c => c is { Type: "sub", Value: "attacker" });
    }

    private static string UnsignedJwt() =>
        BuildJwt(header: """{"alg":"none","typ":"JWT"}""", payload: """{"sub":"11111111-1111-1111-1111-111111111111"}""");

    private static string ExpiredUnsignedJwt() =>
        BuildJwt(
            header: """{"alg":"none","typ":"JWT"}""",
            payload: """{"sub":"11111111-1111-1111-1111-111111111111","exp":1}"""
        );

    private static string BuildJwt(string header, string payload) =>
        $"{Base64Url(json: header)}.{Base64Url(json: payload)}.";

    private static string Base64Url(string json) =>
        Convert.ToBase64String(inArray: Encoding.UTF8.GetBytes(s: json))
            .TrimEnd(trimChar: '=')
            .Replace(oldChar: '+', newChar: '-')
            .Replace(oldChar: '/', newChar: '_');

    /// <summary>
    /// A plugin that always claims success and tries to hand out an elevated "role"
    /// claim plus a forged "sub", alongside a legitimate custom claim — the adversarial
    /// case the invariant must survive: the reserved claims must never reach the
    /// identity, while the plugin's own custom claim type still gets through.
    /// </summary>
    private sealed class MaliciousAuthPlugin : IAuthPlugin
    {
        public string Name => "malicious-auth";
        public string Description => "always claims success and grants an elevated role";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(major: 1, minor: 0);

        public void Initialize(IPluginContext context) { }

        public void Dispose() { }

        public Task<AuthResult> AuthenticateAsync(string token, CancellationToken ct = default) =>
            Task.FromResult(
                result: new AuthResult
                {
                    IsAuthenticated = true,
                    Claims = new()
                    {
                        [key: ClaimTypes.Role] = "super-admin",
                        [key: "sub"] = "attacker",
                        [key: "plan"] = "pro",
                    },
                }
            );
    }

    private sealed class MaliciousPluginManager : IPluginManager
    {
        private readonly MaliciousAuthPlugin _plugin = new();

        public IReadOnlyList<PluginInfo> GetInstalledPlugins() =>
            [
                new()
                {
                    Id = _plugin.Id,
                    Name = _plugin.Name,
                    Description = _plugin.Description,
                    Version = _plugin.Version,
                    Status = PluginStatus.Active,
                    Capabilities = new() { Hooks = [PluginHookCapability.Auth] },
                },
            ];

        public IEnumerable<T> GetPluginsOfType<T>()
            where T : IPlugin => new IAuthPlugin[] { _plugin }.OfType<T>();

        public Task InstallPluginAsync(string packageUrl, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task EnablePluginAsync(Guid pluginId, CancellationToken ct = default) => Task.CompletedTask;

        public Task DisablePluginAsync(Guid pluginId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task UninstallPluginAsync(Guid pluginId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<PluginLoadResult>> LoadAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PluginLoadResult>>(result: []);
    }

    /// <summary>
    /// Keeps the REAL Keycloak JwtBearer scheme as the ambient default (unlike every
    /// other fixture, which swaps in TestAuthHandler) and installs a plugin manager
    /// that always tries to grant elevated access, so a failure of the "never weaken
    /// auth" invariant would show up as a 200/elevated-claims leak instead of a 401.
    /// </summary>
    private sealed class RealAuthApiFactory : NoMercyApiFactory
    {
        protected override bool UseRealAuthentication => true;

        protected override IPluginManager CreateTestPluginManager() => new MaliciousPluginManager();

        protected override void ConfigureRealAuthentication(IServiceCollection services)
        {
            // Bypass the live OIDC discovery network round-trip to auth.nomercy.tv so
            // this test is deterministic and offline. Every real validation rule
            // ConfigureAuth configures (issuer, audience, signing-key resolver,
            // OnTokenValidated) is untouched — only the discovery document's source
            // is swapped from "live HTTP fetch" to "supplied directly".
            services.PostConfigure<JwtBearerOptions>(
                name: JwtBearerDefaults.AuthenticationScheme,
                configureOptions: options =>
                {
                    options.Configuration = new() { Issuer = ExternalServicesConfig.Current.AuthBaseUrl };
                }
            );
        }
    }
}
