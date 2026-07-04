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

using NoMercy.Authorization;
using NoMercy.Database.Models.Users;
using Xunit;

namespace NoMercy.Tests.Api.Authorization;

[Trait("Category", "Authorization")]
public sealed class UserCacheTests
{
    [Fact]
    public void GetUser_ReturnsUser_AfterAddUser()
    {
        UserCache cache = new();
        Guid id = Guid.NewGuid();
        User user = new()
        {
            Id = id,
            Name = "Alice",
            Email = "alice@nm.tv",
        };

        cache.AddUser(user);
        User? found = cache.GetUser(id);

        found.Should().NotBeNull();
        found!.Id.Should().Be(id);
    }

    [Fact]
    public void GetUser_ReturnsNull_AfterRemoveUser()
    {
        UserCache cache = new();
        Guid id = Guid.NewGuid();
        User user = new()
        {
            Id = id,
            Name = "Bob",
            Email = "bob@nm.tv",
        };

        cache.AddUser(user);
        cache.RemoveUser(user);
        User? found = cache.GetUser(id);

        found.Should().BeNull();
    }

    [Fact]
    public void UpdateUser_ReflectsChangedProperties()
    {
        UserCache cache = new();
        Guid id = Guid.NewGuid();
        User original = new()
        {
            Id = id,
            Name = "Carol",
            Allowed = false,
            Email = "c@nm.tv",
        };
        cache.AddUser(original);

        User updated = new()
        {
            Id = id,
            Name = "Carol",
            Allowed = true,
            Email = "c@nm.tv",
        };
        cache.UpdateUser(updated);

        User? found = cache.GetUser(id);
        found.Should().NotBeNull();
        found!.Allowed.Should().BeTrue();
    }

    [Fact]
    public void Reset_ClearsAllUsersAndFolderIds()
    {
        UserCache cache = new();
        cache.AddUser(
            new User
            {
                Id = Guid.NewGuid(),
                Name = "Dave",
                Email = "d@nm.tv",
            }
        );

        cache.Reset();

        cache.Users.Should().BeEmpty();
        cache.FolderIds.Should().BeEmpty();
    }

    [Fact]
    public void Users_ReflectsMultipleAddedUsers()
    {
        UserCache cache = new();
        User userA = new()
        {
            Id = Guid.NewGuid(),
            Name = "A",
            Email = "a@nm.tv",
        };
        User userB = new()
        {
            Id = Guid.NewGuid(),
            Name = "B",
            Email = "b@nm.tv",
        };

        cache.AddUser(userA);
        cache.AddUser(userB);

        cache.Users.Should().HaveCount(2);
        cache.Users.Select(u => u.Id).Should().Contain([userA.Id, userB.Id]);
    }

    [Fact]
    public void GetUser_ReturnsNull_WhenCacheIsEmpty()
    {
        UserCache cache = new();

        User? found = cache.GetUser(Guid.NewGuid());

        found.Should().BeNull();
    }

    [Fact]
    public void AddUser_DoesNotAffectOtherUser()
    {
        UserCache cache = new();
        Guid idA = Guid.NewGuid();
        Guid idB = Guid.NewGuid();
        cache.AddUser(
            new User
            {
                Id = idA,
                Name = "A",
                Email = "a@nm.tv",
            }
        );

        User? found = cache.GetUser(idB);

        found.Should().BeNull();
    }
}
