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

[Trait(name: "Category", value: "Authorization")]
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

        cache.AddUser(user: user);
        User? found = cache.GetUser(userId: id);

        found.Should().NotBeNull();
        found!.Id.Should().Be(expected: id);
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

        cache.AddUser(user: user);
        cache.RemoveUser(user: user);
        User? found = cache.GetUser(userId: id);

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
        cache.AddUser(user: original);

        User updated = new()
        {
            Id = id,
            Name = "Carol",
            Allowed = true,
            Email = "c@nm.tv",
        };
        cache.UpdateUser(user: updated);

        User? found = cache.GetUser(userId: id);
        found.Should().NotBeNull();
        found!.Allowed.Should().BeTrue();
    }

    [Fact]
    public void Reset_ClearsAllUsersAndFolderIds()
    {
        UserCache cache = new();
        cache.AddUser(
            user: new()
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

        cache.AddUser(user: userA);
        cache.AddUser(user: userB);

        cache.Users.Should().HaveCount(expected: 2);
        cache.Users.Select(selector: u => u.Id).Should().Contain(expected: [userA.Id, userB.Id]);
    }

    [Fact]
    public void GetUser_ReturnsNull_WhenCacheIsEmpty()
    {
        UserCache cache = new();

        User? found = cache.GetUser(userId: Guid.NewGuid());

        found.Should().BeNull();
    }

    [Fact]
    public void AddUser_DoesNotAffectOtherUser()
    {
        UserCache cache = new();
        Guid idA = Guid.NewGuid();
        Guid idB = Guid.NewGuid();
        cache.AddUser(
            user: new()
            {
                Id = idA,
                Name = "A",
                Email = "a@nm.tv",
            }
        );

        User? found = cache.GetUser(userId: idB);

        found.Should().BeNull();
    }

    [Fact]
    public void FolderIds_ReturnsEmptyInitially()
    {
        UserCache cache = new();

        IReadOnlyList<Ulid> folderIds = cache.FolderIds;

        folderIds.Should().BeEmpty();
    }

    [Fact]
    public void Users_ReturnsImmutableCopy()
    {
        UserCache cache = new();
        Guid userId = Guid.NewGuid();
        User user = new()
        {
            Id = userId,
            Name = "Test",
            Email = "test@nm.tv",
        };
        cache.AddUser(user: user);

        IReadOnlyList<User> firstRead = cache.Users;
        IReadOnlyList<User> secondRead = cache.Users;

        firstRead.Should().NotBeSameAs(unexpected: secondRead);
        firstRead.Should().HaveCount(expected: 1);
        firstRead[index: 0].Id.Should().Be(expected: userId);
    }

    [Fact]
    public void GetUser_ReturnsNullForUnknownId()
    {
        UserCache cache = new();
        Guid unknownId = Guid.NewGuid();

        User? found = cache.GetUser(userId: unknownId);

        found.Should().BeNull();
    }

    [Fact]
    public void RemoveUser_PreservesOtherUsers()
    {
        UserCache cache = new();
        Guid idA = Guid.NewGuid();
        Guid idB = Guid.NewGuid();
        User userA = new()
        {
            Id = idA,
            Name = "A",
            Email = "a@nm.tv",
        };
        User userB = new()
        {
            Id = idB,
            Name = "B",
            Email = "b@nm.tv",
        };

        cache.AddUser(user: userA);
        cache.AddUser(user: userB);
        cache.RemoveUser(user: userA);

        cache.Users.Should().HaveCount(expected: 1);
        cache.Users[index: 0].Id.Should().Be(expected: idB);
    }

    [Fact]
    public void UpdateUser_OnlyUpdatesTargetUser()
    {
        UserCache cache = new();
        Guid idA = Guid.NewGuid();
        Guid idB = Guid.NewGuid();
        User userA = new()
        {
            Id = idA,
            Name = "A",
            Email = "a@nm.tv",
            Owner = false,
        };
        User userB = new()
        {
            Id = idB,
            Name = "B",
            Email = "b@nm.tv",
            Owner = false,
        };

        cache.AddUser(user: userA);
        cache.AddUser(user: userB);

        User updatedA = new()
        {
            Id = idA,
            Name = "A Updated",
            Email = "a-updated@nm.tv",
            Owner = true,
        };
        cache.UpdateUser(user: updatedA);

        User? foundA = cache.GetUser(userId: idA);
        User? foundB = cache.GetUser(userId: idB);

        foundA.Should().NotBeNull();
        foundA!.Owner.Should().BeTrue();
        foundA.Name.Should().Be(expected: "A Updated");

        foundB.Should().NotBeNull();
        foundB!.Owner.Should().BeFalse();
        foundB.Name.Should().Be(expected: "B");
    }

    [Fact]
    public void RemoveUser_DoesNothingWhenUserNotPresent()
    {
        UserCache cache = new();
        Guid id = Guid.NewGuid();
        User user = new()
        {
            Id = id,
            Name = "Test",
            Email = "test@nm.tv",
        };

        cache.RemoveUser(user: user);

        cache.Users.Should().BeEmpty();
    }

    [Fact]
    public void UpdateUser_DoesNothingWhenUserNotPresent()
    {
        UserCache cache = new();
        Guid id = Guid.NewGuid();
        User user = new()
        {
            Id = id,
            Name = "Test",
            Email = "test@nm.tv",
        };

        cache.UpdateUser(user: user);

        cache.Users.Should().BeEmpty();
    }

    [Fact]
    public void Reset_ClearsUsers()
    {
        UserCache cache = new();
        cache.AddUser(
            user: new()
            {
                Id = Guid.NewGuid(),
                Name = "User1",
                Email = "u1@nm.tv",
            }
        );
        cache.AddUser(
            user: new()
            {
                Id = Guid.NewGuid(),
                Name = "User2",
                Email = "u2@nm.tv",
            }
        );

        cache.Reset();

        cache.Users.Should().BeEmpty();
        cache.FolderIds.Should().BeEmpty();
    }

    [Fact]
    public void GetUser_FindsUserAfterMultipleAdditions()
    {
        UserCache cache = new();
        Guid targetId = Guid.NewGuid();
        User targetUser = new()
        {
            Id = targetId,
            Name = "Target",
            Email = "target@nm.tv",
        };

        for (int i = 0; i < 10; i++)
        {
            cache.AddUser(
                user: new()
                {
                    Id = Guid.NewGuid(),
                    Name = $"User{i}",
                    Email = $"user{i}@nm.tv",
                }
            );
        }

        cache.AddUser(user: targetUser);

        for (int i = 10; i < 20; i++)
        {
            cache.AddUser(
                user: new()
                {
                    Id = Guid.NewGuid(),
                    Name = $"User{i}",
                    Email = $"user{i}@nm.tv",
                }
            );
        }

        User? found = cache.GetUser(userId: targetId);

        found.Should().NotBeNull();
        found!.Name.Should().Be(expected: "Target");
    }
}
