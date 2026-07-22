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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Authorization;
using NoMercy.Database.Models.Users;
using NoMercy.Service.Authorization;
using Xunit;

namespace NoMercy.Tests.Api.Authorization;

[Trait(name: "Category", value: "Authorization")]
public sealed class AuthorizationPolicyTests : IDisposable
{
    private static readonly Guid KnownUserId = Guid.Parse(input: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UnknownUserId = Guid.Parse(input: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public AuthorizationPolicyTests()
    {
        UserCache.Current.Reset();
        UserCache.Current.AddUser(
            user: new()
            {
                Id = KnownUserId,
                Email = "known@nomercy.tv",
                Name = "Known User",
                Owner = false,
                Manage = false,
                Allowed = true,
            }
        );
    }

    public void Dispose()
    {
        UserCache.Current.Reset();
    }

    private static IAuthorizationService BuildAuthorizationService()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                name: "api",
                configurePolicy: policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(claimType: "scope", allowedValues: ["openid", "profile"]);
                    policy.AddRequirements(
                        requirements: new AssertionRequirement(handler: ctx =>
                        {
                            string? sub = ctx.User.FindFirstValue(claimType: ClaimTypes.NameIdentifier);
                            if (!Guid.TryParse(input: sub, result: out Guid userId))
                                return false;
                            User? user = UserCache.Current.Users.FirstOrDefault(predicate: u =>
                                u.Id == userId
                            );
                            return user is not null;
                        })
                    );
                }
            );
        ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal BuildPrincipal(
        Guid userId,
        bool includeOpenid,
        bool includeProfile
    )
    {
        List<Claim> claims = [new(type: ClaimTypes.NameIdentifier, value: userId.ToString())];
        if (includeOpenid)
            claims.Add(item: new(type: "scope", value: "openid"));
        if (includeProfile)
            claims.Add(item: new(type: "scope", value: "profile"));

        ClaimsIdentity identity = new(claims: claims, authenticationType: "TestScheme");
        return new(identity: identity);
    }

    [Fact]
    public async Task ApiPolicy_Admits_WhenAuthenticatedWithScopesAndUserInCache()
    {
        IAuthorizationService authService = BuildAuthorizationService();
        ClaimsPrincipal principal = BuildPrincipal(
            userId: KnownUserId,
            includeOpenid: true,
            includeProfile: true
        );

        AuthorizationResult result = await authService.AuthorizeAsync(user: principal, policyName: "api");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ApiPolicy_Admits_WhenOnlyOpenidScopePresent()
    {
        IAuthorizationService authService = BuildAuthorizationService();
        ClaimsPrincipal principal = BuildPrincipal(
            userId: KnownUserId,
            includeOpenid: true,
            includeProfile: false
        );

        AuthorizationResult result = await authService.AuthorizeAsync(user: principal, policyName: "api");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ApiPolicy_Admits_WhenOnlyProfileScopePresent()
    {
        IAuthorizationService authService = BuildAuthorizationService();
        ClaimsPrincipal principal = BuildPrincipal(
            userId: KnownUserId,
            includeOpenid: false,
            includeProfile: true
        );

        AuthorizationResult result = await authService.AuthorizeAsync(user: principal, policyName: "api");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ApiPolicy_Denies_WhenNoScopeClaimPresent()
    {
        IAuthorizationService authService = BuildAuthorizationService();
        ClaimsPrincipal principal = BuildPrincipal(
            userId: KnownUserId,
            includeOpenid: false,
            includeProfile: false
        );

        AuthorizationResult result = await authService.AuthorizeAsync(user: principal, policyName: "api");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task ApiPolicy_Denies_WhenUserNotInCache()
    {
        IAuthorizationService authService = BuildAuthorizationService();
        ClaimsPrincipal principal = BuildPrincipal(
            userId: UnknownUserId,
            includeOpenid: true,
            includeProfile: true
        );

        AuthorizationResult result = await authService.AuthorizeAsync(user: principal, policyName: "api");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task ApiPolicy_Denies_WhenSubClaimMalformed()
    {
        IAuthorizationService authService = BuildAuthorizationService();

        List<Claim> claims =
        [
            new(type: ClaimTypes.NameIdentifier, value: "not-a-guid"),
            new(type: "scope", value: "openid"),
            new(type: "scope", value: "profile"),
        ];
        ClaimsIdentity identity = new(claims: claims, authenticationType: "TestScheme");
        ClaimsPrincipal principal = new(identity: identity);

        AuthorizationResult result = await authService.AuthorizeAsync(user: principal, policyName: "api");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task ApiPolicy_Denies_WhenUnauthenticated()
    {
        IAuthorizationService authService = BuildAuthorizationService();
        ClaimsPrincipal principal = new(identity: new ClaimsIdentity());

        AuthorizationResult result = await authService.AuthorizeAsync(user: principal, policyName: "api");

        result.Succeeded.Should().BeFalse();
    }
}
