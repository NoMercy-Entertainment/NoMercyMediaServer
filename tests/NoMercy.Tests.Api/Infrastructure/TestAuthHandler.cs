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
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace NoMercy.Tests.Api.Infrastructure;

public static class TestAuthDefaults
{
    public const string AuthenticationScheme = "TestScheme";
    public const string TestAuthHeader = "X-Test-Auth";
    public const string Deny = "deny";

    // Lets a request impersonate a second, distinct identity (see
    // TestAuthHandler.SecondaryUserId) for ownership-isolation tests, without
    // giving every other test — which never sends this header — any different
    // behavior than before.
    public const string TestUserIdHeader = "X-Test-User-Id";
}

public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options: options, logger: logger, encoder: encoder)
{
    public static Guid DefaultUserId { get; } = Guid.Parse(input: "37d03e60-7b0a-4246-a85b-a5618966a383");
    public static string DefaultUserName { get; } = "Test User";
    public static string DefaultUserEmail { get; } = "test@nomercy.tv";

    // A second, distinct seeded user (see NoMercyApiFactory.SeedMediaData) so
    // ownership-isolation tests (e.g. "user B gets 404 on user A's playlist")
    // can impersonate a real, allowed-but-unrelated identity via
    // TestAuthDefaults.TestUserIdHeader instead of the single fixed identity
    // every other controller test in this fixture relies on.
    public static Guid SecondaryUserId { get; } =
        Guid.Parse(input: "8f2c1a90-4b3d-4e7a-9c1f-6d2e8a5b3c71");
    public static string SecondaryUserName { get; } = "Secondary Test User";
    public static string SecondaryUserEmail { get; } = "test-secondary@nomercy.tv";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (
            Request.Headers.TryGetValue(key: TestAuthDefaults.TestAuthHeader, value: out StringValues value)
            && value.ToString() == TestAuthDefaults.Deny
        )
        {
            return Task.FromResult(result: AuthenticateResult.Fail(failureMessage: "Authentication denied by test"));
        }

        Guid userId = DefaultUserId;
        string userName = DefaultUserName;
        string userEmail = DefaultUserEmail;

        if (
            Request.Headers.TryGetValue(
                key: TestAuthDefaults.TestUserIdHeader,
                value: out StringValues userIdHeader
            )
            && userIdHeader.ToString() == SecondaryUserId.ToString()
        )
        {
            userId = SecondaryUserId;
            userName = SecondaryUserName;
            userEmail = SecondaryUserEmail;
        }

        Claim[] claims =
        [
            new(type: ClaimTypes.NameIdentifier, value: userId.ToString()),
            new(type: ClaimTypes.Name, value: userName),
            new(type: ClaimTypes.Email, value: userEmail),
            new(type: ClaimTypes.Role, value: "user"),
            // Single space-delimited scope claim, mirroring the real Keycloak token
            // shape so tests exercise the same split-aware ApiScopePolicy path as
            // production rather than a pre-split multi-claim shape that masks it.
            new(type: "scope", value: "openid profile email"),
        ];

        ClaimsIdentity identity = new(claims: claims, authenticationType: TestAuthDefaults.AuthenticationScheme);
        ClaimsPrincipal principal = new(identity: identity);
        AuthenticationTicket ticket = new(principal: principal, authenticationScheme: TestAuthDefaults.AuthenticationScheme);

        return Task.FromResult(result: AuthenticateResult.Success(ticket: ticket));
    }
}
