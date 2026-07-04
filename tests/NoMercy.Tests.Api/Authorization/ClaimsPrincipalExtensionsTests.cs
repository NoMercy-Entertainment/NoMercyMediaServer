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
using NoMercy.Authorization;
using Xunit;

namespace NoMercy.Tests.Api.Authorization;

[Trait("Category", "Authorization")]
public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void UserId_ReturnsGuid_WhenSubClaimIsValidGuid()
    {
        Guid expected = Guid.Parse("11111111-1111-1111-1111-111111111111");
        ClaimsPrincipal principal = PrincipalWithSub(expected.ToString());

        Guid result = principal.UserId();

        result.Should().Be(expected);
    }

    [Fact]
    public void UserId_ReturnsEmpty_WhenSubClaimIsMalformed()
    {
        ClaimsPrincipal principal = PrincipalWithSub("definitely-not-a-guid");

        Guid result = principal.UserId();

        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void UserId_ReturnsEmpty_WhenSubClaimMissing()
    {
        ClaimsIdentity identity = new([], "TestScheme");
        ClaimsPrincipal principal = new(identity);

        Guid result = principal.UserId();

        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void UserId_ReturnsEmpty_WhenPrincipalIsNull()
    {
        ClaimsPrincipal? principal = null;

        Guid result = principal.UserId();

        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void IsSelf_ReturnsTrue_WhenPrincipalSubMatchesUserId()
    {
        Guid userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        ClaimsPrincipal principal = PrincipalWithSub(userId.ToString());

        bool result = principal.IsSelf(userId);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsSelf_ReturnsFalse_WhenPrincipalSubDoesNotMatchUserId()
    {
        Guid principalId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Guid otherId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        ClaimsPrincipal principal = PrincipalWithSub(principalId.ToString());

        bool result = principal.IsSelf(otherId);

        result.Should().BeFalse();
    }

    [Fact]
    public void UserName_ReturnsNameClaim_WhenPresent()
    {
        List<Claim> claims =
        [
            new Claim("name", "Full Name"),
            new Claim(ClaimTypes.GivenName, "Given"),
            new Claim(ClaimTypes.Surname, "Family"),
        ];
        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, "TestScheme"));

        string result = principal.UserName();

        result.Should().Be("Full Name");
    }

    [Fact]
    public void UserName_FallsBackToGivenAndSurname_WhenNameClaimAbsent()
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.GivenName, "Jane"),
            new Claim(ClaimTypes.Surname, "Doe"),
        ];
        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, "TestScheme"));

        string result = principal.UserName();

        result.Should().Be("Jane Doe");
    }

    [Fact]
    public void Email_ReturnsEmailClaim()
    {
        List<Claim> claims = [new Claim(ClaimTypes.Email, "user@nomercy.tv")];
        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, "TestScheme"));

        string result = principal.Email();

        result.Should().Be("user@nomercy.tv");
    }

    [Fact]
    public void Email_ReturnsEmpty_WhenEmailClaimAbsent()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([], "TestScheme"));

        string result = principal.Email();

        result.Should().BeEmpty();
    }

    private static ClaimsPrincipal PrincipalWithSub(string sub)
    {
        List<Claim> claims = [new Claim(ClaimTypes.NameIdentifier, sub)];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestScheme"));
    }
}
