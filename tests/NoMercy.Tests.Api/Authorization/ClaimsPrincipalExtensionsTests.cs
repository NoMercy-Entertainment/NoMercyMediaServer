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
    public void IsSelf_ReturnsFalse_WhenPrincipalIsNull()
    {
        Guid userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        ClaimsPrincipal? principal = null;

        bool result = principal.IsSelf(userId);

        result.Should().BeFalse();
    }

    [Fact]
    public void Role_ReturnsRoleClaim_WhenPresent()
    {
        List<Claim> claims = [new(ClaimTypes.Role, "Administrator")];
        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, "TestScheme"));

        string result = principal.Role();

        result.Should().Be("Administrator");
    }

    [Fact]
    public void Role_ReturnsEmpty_WhenRoleClaimAbsent()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([], "TestScheme"));

        string result = principal.Role();

        result.Should().BeEmpty();
    }

    [Fact]
    public void Role_ReturnsEmpty_WhenPrincipalIsNull()
    {
        ClaimsPrincipal? principal = null;

        string result = principal.Role();

        result.Should().BeEmpty();
    }

    [Fact]
    public void UserName_ReturnsNameClaim_WhenPresent()
    {
        List<Claim> claims =
        [
            new("name", "Full Name"),
            new(ClaimTypes.GivenName, "Given"),
            new(ClaimTypes.Surname, "Family"),
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
            new(ClaimTypes.GivenName, "Jane"),
            new(ClaimTypes.Surname, "Doe"),
        ];
        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, "TestScheme"));

        string result = principal.UserName();

        result.Should().Be("Jane Doe");
    }

    [Fact]
    public void UserName_ReturnsTrimmedGivenName_WhenSurnameAbsent()
    {
        List<Claim> claims = [new(ClaimTypes.GivenName, "John")];
        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, "TestScheme"));

        string result = principal.UserName();

        result.Should().Be("John");
    }

    [Fact]
    public void UserName_ReturnsTrimmedSurname_WhenGivenNameAbsent()
    {
        List<Claim> claims = [new(ClaimTypes.Surname, "Smith")];
        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, "TestScheme"));

        string result = principal.UserName();

        result.Should().Be("Smith");
    }

    [Fact]
    public void UserName_ReturnsEmpty_WhenAllNameClaimsAbsent()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([], "TestScheme"));

        string result = principal.UserName();

        result.Should().BeEmpty();
    }

    [Fact]
    public void UserName_ReturnsEmpty_WhenPrincipalIsNull()
    {
        ClaimsPrincipal? principal = null;

        string result = principal.UserName();

        result.Should().BeEmpty();
    }

    [Fact]
    public void Email_ReturnsEmailClaim()
    {
        List<Claim> claims = [new(ClaimTypes.Email, "user@nomercy.tv")];
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

    [Fact]
    public void Email_ReturnsEmpty_WhenPrincipalIsNull()
    {
        ClaimsPrincipal? principal = null;

        string result = principal.Email();

        result.Should().BeEmpty();
    }

    private static ClaimsPrincipal PrincipalWithSub(string sub)
    {
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, sub)];
        return new(new ClaimsIdentity(claims, "TestScheme"));
    }
}
