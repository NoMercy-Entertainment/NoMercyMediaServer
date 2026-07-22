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

[Trait(name: "Category", value: "Authorization")]
public sealed class MediaAuthorizationPolicyTests
{
    private static readonly Guid OwnerId = Guid.Parse(input: "10000000-0000-0000-0000-000000000001");
    private static readonly Guid ModeratorId = Guid.Parse(input: "20000000-0000-0000-0000-000000000002");
    private static readonly Guid AllowedUserId = Guid.Parse(input: "30000000-0000-0000-0000-000000000003");
    private static readonly Guid RandomUserId = Guid.Parse(input: "40000000-0000-0000-0000-000000000004");

    private static IMediaAuthorizationPolicy BuildPolicy(IEnumerable<User> users)
    {
        UserCache cache = new();
        foreach (User user in users)
            cache.AddUser(user: user);
        return new MediaAuthorizationPolicy(userCache: cache);
    }

    private static ClaimsPrincipal PrincipalFor(Guid userId)
    {
        List<Claim> claims = [new(type: ClaimTypes.NameIdentifier, value: userId.ToString())];
        return new(identity: new ClaimsIdentity(claims: claims, authenticationType: "TestScheme"));
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
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [owner]);

        bool result = policy.IsOwner(principal: PrincipalFor(userId: OwnerId));

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
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [owner]);

        bool result = policy.IsOwner(principal: PrincipalFor(userId: RandomUserId));

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
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [nonOwner]);

        bool result = policy.IsOwner(principal: PrincipalFor(userId: OwnerId));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsOwner_Denies_WhenCacheIsEmpty()
    {
        IMediaAuthorizationPolicy policy = BuildPolicy(users: []);

        bool result = policy.IsOwner(principal: PrincipalFor(userId: OwnerId));

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
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [moderator]);

        bool result = policy.IsModerator(principal: PrincipalFor(userId: ModeratorId));

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
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [owner]);

        bool result = policy.IsModerator(principal: PrincipalFor(userId: OwnerId));

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
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [plain]);

        bool result = policy.IsModerator(principal: PrincipalFor(userId: AllowedUserId));

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
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [allowed]);

        bool result = policy.IsAllowed(principal: PrincipalFor(userId: AllowedUserId));

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
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [owner]);

        bool result = policy.IsAllowed(principal: PrincipalFor(userId: OwnerId));

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
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [blocked]);

        bool result = policy.IsAllowed(principal: PrincipalFor(userId: RandomUserId));

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
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [owner]);

        bool result = policy.IsAllowed(principal: PrincipalFor(userId: RandomUserId));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsOwner_Denies_WhenPrincipalIsNull()
    {
        User owner = new()
        {
            Id = OwnerId,
            Owner = true,
            Name = "Owner",
            Email = "o@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [owner]);

        bool result = policy.IsOwner(principal: null);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsModerator_Denies_WhenPrincipalIsNull()
    {
        User moderator = new()
        {
            Id = ModeratorId,
            Manage = true,
            Owner = false,
            Name = "Mod",
            Email = "m@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [moderator]);

        bool result = policy.IsModerator(principal: null);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_Denies_WhenPrincipalIsNull()
    {
        User allowed = new()
        {
            Id = AllowedUserId,
            Allowed = true,
            Owner = false,
            Name = "Allowed",
            Email = "a@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [allowed]);

        bool result = policy.IsAllowed(principal: null);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsModerator_Denies_WhenCacheIsEmpty()
    {
        IMediaAuthorizationPolicy policy = BuildPolicy(users: []);

        bool result = policy.IsModerator(principal: PrincipalFor(userId: ModeratorId));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_Denies_WhenCacheIsEmpty()
    {
        IMediaAuthorizationPolicy policy = BuildPolicy(users: []);

        bool result = policy.IsAllowed(principal: PrincipalFor(userId: AllowedUserId));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsOwner_WithMultipleUsers_IdentifiesOwner()
    {
        User owner = new()
        {
            Id = OwnerId,
            Owner = true,
            Name = "Owner",
            Email = "o@nm.tv",
        };
        User moderator = new()
        {
            Id = ModeratorId,
            Owner = false,
            Manage = true,
            Name = "Mod",
            Email = "m@nm.tv",
        };
        User allowed = new()
        {
            Id = AllowedUserId,
            Owner = false,
            Allowed = true,
            Name = "Allowed",
            Email = "a@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [owner, moderator, allowed]);

        bool result = policy.IsOwner(principal: PrincipalFor(userId: OwnerId));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsModerator_WithMultipleUsers_IdentifiesModerator()
    {
        User owner = new()
        {
            Id = OwnerId,
            Owner = true,
            Name = "Owner",
            Email = "o@nm.tv",
        };
        User moderator = new()
        {
            Id = ModeratorId,
            Owner = false,
            Manage = true,
            Name = "Mod",
            Email = "m@nm.tv",
        };
        User allowed = new()
        {
            Id = AllowedUserId,
            Owner = false,
            Allowed = true,
            Name = "Allowed",
            Email = "a@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [owner, moderator, allowed]);

        bool result = policy.IsModerator(principal: PrincipalFor(userId: ModeratorId));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_WithMultipleUsers_IdentifiesAllowed()
    {
        User owner = new()
        {
            Id = OwnerId,
            Owner = true,
            Name = "Owner",
            Email = "o@nm.tv",
        };
        User moderator = new()
        {
            Id = ModeratorId,
            Owner = false,
            Manage = true,
            Name = "Mod",
            Email = "m@nm.tv",
        };
        User allowed = new()
        {
            Id = AllowedUserId,
            Owner = false,
            Allowed = true,
            Name = "Allowed",
            Email = "a@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [owner, moderator, allowed]);

        bool result = policy.IsAllowed(principal: PrincipalFor(userId: AllowedUserId));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_ImpliesModerator()
    {
        User moderator = new()
        {
            Id = ModeratorId,
            Owner = false,
            Manage = true,
            Allowed = true,
            Name = "Mod",
            Email = "m@nm.tv",
        };
        IMediaAuthorizationPolicy policy = BuildPolicy(users: [moderator]);

        bool isAllowed = policy.IsAllowed(principal: PrincipalFor(userId: ModeratorId));
        bool isModerator = policy.IsModerator(principal: PrincipalFor(userId: ModeratorId));

        isAllowed.Should().BeTrue();
        isModerator.Should().BeTrue();
    }
}
