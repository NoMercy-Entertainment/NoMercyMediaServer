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

[Trait(name: "Category", value: "Authorization")]
public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void UserId_ReturnsGuid_WhenSubClaimIsValidGuid()
    {
        Guid expected = Guid.Parse(input: "11111111-1111-1111-1111-111111111111");
        ClaimsPrincipal principal = PrincipalWithSub(sub: expected.ToString());

        Guid result = principal.UserId();

        result.Should().Be(expected: expected);
    }

    [Fact]
    public void UserId_ReturnsEmpty_WhenSubClaimIsMalformed()
    {
        ClaimsPrincipal principal = PrincipalWithSub(sub: "definitely-not-a-guid");

        Guid result = principal.UserId();

        result.Should().Be(expected: Guid.Empty);
    }

    [Fact]
    public void UserId_ReturnsEmpty_WhenSubClaimMissing()
    {
        ClaimsIdentity identity = new(claims: [], authenticationType: "TestScheme");
        ClaimsPrincipal principal = new(identity: identity);

        Guid result = principal.UserId();

        result.Should().Be(expected: Guid.Empty);
    }

    [Fact]
    public void UserId_ReturnsEmpty_WhenPrincipalIsNull()
    {
        ClaimsPrincipal? principal = null;

        Guid result = principal.UserId();

        result.Should().Be(expected: Guid.Empty);
    }

    [Fact]
    public void IsSelf_ReturnsTrue_WhenPrincipalSubMatchesUserId()
    {
        Guid userId = Guid.Parse(input: "22222222-2222-2222-2222-222222222222");
        ClaimsPrincipal principal = PrincipalWithSub(sub: userId.ToString());

        bool result = principal.IsSelf(userId: userId);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsSelf_ReturnsFalse_WhenPrincipalSubDoesNotMatchUserId()
    {
        Guid principalId = Guid.Parse(input: "22222222-2222-2222-2222-222222222222");
        Guid otherId = Guid.Parse(input: "33333333-3333-3333-3333-333333333333");
        ClaimsPrincipal principal = PrincipalWithSub(sub: principalId.ToString());

        bool result = principal.IsSelf(userId: otherId);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsSelf_ReturnsFalse_WhenPrincipalIsNull()
    {
        Guid userId = Guid.Parse(input: "22222222-2222-2222-2222-222222222222");
        ClaimsPrincipal? principal = null;

        bool result = principal.IsSelf(userId: userId);

        result.Should().BeFalse();
    }

    [Fact]
    public void Role_ReturnsRoleClaim_WhenPresent()
    {
        List<Claim> claims = [new(type: ClaimTypes.Role, value: "Administrator")];
        ClaimsPrincipal principal = new(identity: new ClaimsIdentity(claims: claims, authenticationType: "TestScheme"));

        string result = principal.Role();

        result.Should().Be(expected: "Administrator");
    }

    [Fact]
    public void Role_ReturnsEmpty_WhenRoleClaimAbsent()
    {
        ClaimsPrincipal principal = new(identity: new ClaimsIdentity(claims: [], authenticationType: "TestScheme"));

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
            new(type: "name", value: "Full Name"),
            new(type: ClaimTypes.GivenName, value: "Given"),
            new(type: ClaimTypes.Surname, value: "Family"),
        ];
        ClaimsPrincipal principal = new(identity: new ClaimsIdentity(claims: claims, authenticationType: "TestScheme"));

        string result = principal.UserName();

        result.Should().Be(expected: "Full Name");
    }

    [Fact]
    public void UserName_FallsBackToGivenAndSurname_WhenNameClaimAbsent()
    {
        List<Claim> claims =
        [
            new(type: ClaimTypes.GivenName, value: "Jane"),
            new(type: ClaimTypes.Surname, value: "Doe"),
        ];
        ClaimsPrincipal principal = new(identity: new ClaimsIdentity(claims: claims, authenticationType: "TestScheme"));

        string result = principal.UserName();

        result.Should().Be(expected: "Jane Doe");
    }

    [Fact]
    public void UserName_ReturnsTrimmedGivenName_WhenSurnameAbsent()
    {
        List<Claim> claims = [new(type: ClaimTypes.GivenName, value: "John")];
        ClaimsPrincipal principal = new(identity: new ClaimsIdentity(claims: claims, authenticationType: "TestScheme"));

        string result = principal.UserName();

        result.Should().Be(expected: "John");
    }

    [Fact]
    public void UserName_ReturnsTrimmedSurname_WhenGivenNameAbsent()
    {
        List<Claim> claims = [new(type: ClaimTypes.Surname, value: "Smith")];
        ClaimsPrincipal principal = new(identity: new ClaimsIdentity(claims: claims, authenticationType: "TestScheme"));

        string result = principal.UserName();

        result.Should().Be(expected: "Smith");
    }

    [Fact]
    public void UserName_ReturnsEmpty_WhenAllNameClaimsAbsent()
    {
        ClaimsPrincipal principal = new(identity: new ClaimsIdentity(claims: [], authenticationType: "TestScheme"));

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
        List<Claim> claims = [new(type: ClaimTypes.Email, value: "user@nomercy.tv")];
        ClaimsPrincipal principal = new(identity: new ClaimsIdentity(claims: claims, authenticationType: "TestScheme"));

        string result = principal.Email();

        result.Should().Be(expected: "user@nomercy.tv");
    }

    [Fact]
    public void Email_ReturnsEmpty_WhenEmailClaimAbsent()
    {
        ClaimsPrincipal principal = new(identity: new ClaimsIdentity(claims: [], authenticationType: "TestScheme"));

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
        List<Claim> claims = [new(type: ClaimTypes.NameIdentifier, value: sub)];
        return new(identity: new ClaimsIdentity(claims: claims, authenticationType: "TestScheme"));
    }
}
