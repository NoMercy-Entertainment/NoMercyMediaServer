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
using NoMercy.Database.Models.Users;
using Xunit;

namespace NoMercy.Tests.Api.Authorization;

[Trait("Category", "Authorization")]
public sealed class MediaAuthorizationPolicyTests
{
    private static readonly Guid OwnerId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ModeratorId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid AllowedUserId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly Guid RandomUserId = Guid.Parse("40000000-0000-0000-0000-000000000004");

    private static IMediaAuthorizationPolicy BuildPolicy(IEnumerable<User> users)
    {
        UserCache cache = new();
        foreach (User user in users)
            cache.AddUser(user);
        return new MediaAuthorizationPolicy(cache);
    }

    private static ClaimsPrincipal PrincipalFor(Guid userId)
    {
        List<Claim> claims = [new Claim(ClaimTypes.NameIdentifier, userId.ToString())];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestScheme"));
    }

    [Fact]
    public void IsOwner_Admits_WhenPrincipalIsOwner()
    {
        User owner = new()
        {
            Id = OwnerId,
            Owner = true,
            Name = "Owner",
            Email = "o@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy([owner]);

        bool result = policy.IsOwner(PrincipalFor(OwnerId));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsOwner_Denies_WhenPrincipalIsNotOwner()
    {
        User owner = new()
        {
            Id = OwnerId,
            Owner = true,
            Name = "Owner",
            Email = "o@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy([owner]);

        bool result = policy.IsOwner(PrincipalFor(RandomUserId));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsOwner_Denies_WhenCacheHasNoOwnerFlagged()
    {
        User nonOwner = new()
        {
            Id = OwnerId,
            Owner = false,
            Manage = true,
            Name = "Mod",
            Email = "m@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy([nonOwner]);

        bool result = policy.IsOwner(PrincipalFor(OwnerId));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsOwner_Denies_WhenCacheIsEmpty()
    {
        IMediaAuthorizationPolicy policy = BuildPolicy([]);

        bool result = policy.IsOwner(PrincipalFor(OwnerId));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsModerator_Admits_WhenPrincipalHasManageFlag()
    {
        User moderator = new()
        {
            Id = ModeratorId,
            Manage = true,
            Owner = false,
            Name = "Mod",
            Email = "m@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy([moderator]);

        bool result = policy.IsModerator(PrincipalFor(ModeratorId));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsModerator_Admits_WhenPrincipalIsOwner()
    {
        User owner = new()
        {
            Id = OwnerId,
            Owner = true,
            Manage = false,
            Name = "Owner",
            Email = "o@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy([owner]);

        bool result = policy.IsModerator(PrincipalFor(OwnerId));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsModerator_Denies_WhenPrincipalLacksManageAndIsNotOwner()
    {
        User plain = new()
        {
            Id = AllowedUserId,
            Owner = false,
            Manage = false,
            Allowed = true,
            Name = "Plain",
            Email = "p@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy([plain]);

        bool result = policy.IsModerator(PrincipalFor(AllowedUserId));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_Admits_WhenPrincipalHasAllowedFlag()
    {
        User allowed = new()
        {
            Id = AllowedUserId,
            Allowed = true,
            Owner = false,
            Name = "Allowed",
            Email = "a@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy([allowed]);

        bool result = policy.IsAllowed(PrincipalFor(AllowedUserId));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_Admits_WhenPrincipalIsOwner()
    {
        User owner = new()
        {
            Id = OwnerId,
            Owner = true,
            Allowed = false,
            Name = "Owner",
            Email = "o@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy([owner]);

        bool result = policy.IsAllowed(PrincipalFor(OwnerId));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_Denies_WhenPrincipalLacksAllowedAndIsNotOwner()
    {
        User blocked = new()
        {
            Id = RandomUserId,
            Allowed = false,
            Owner = false,
            Name = "Blocked",
            Email = "b@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy([blocked]);

        bool result = policy.IsAllowed(PrincipalFor(RandomUserId));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_Denies_WhenUserNotInCache()
    {
        User owner = new()
        {
            Id = OwnerId,
            Owner = true,
            Name = "Owner",
            Email = "o@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy([owner]);

        bool result = policy.IsAllowed(PrincipalFor(RandomUserId));

        result.Should().BeFalse();
    }
}
