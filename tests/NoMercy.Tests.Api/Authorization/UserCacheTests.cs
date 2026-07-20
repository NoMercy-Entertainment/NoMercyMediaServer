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
            new()
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
            new()
            {
                Id = idA,
                Name = "A",
                Email = "a@nm.tv",
            }
        );

        User? found = cache.GetUser(idB);

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
        cache.AddUser(user);

        IReadOnlyList<User> firstRead = cache.Users;
        IReadOnlyList<User> secondRead = cache.Users;

        firstRead.Should().NotBeSameAs(secondRead);
        firstRead.Should().HaveCount(1);
        firstRead[0].Id.Should().Be(userId);
    }

    [Fact]
    public void GetUser_ReturnsNullForUnknownId()
    {
        UserCache cache = new();
        Guid unknownId = Guid.NewGuid();

        User? found = cache.GetUser(unknownId);

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

        cache.AddUser(userA);
        cache.AddUser(userB);
        cache.RemoveUser(userA);

        cache.Users.Should().HaveCount(1);
        cache.Users[0].Id.Should().Be(idB);
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

        cache.AddUser(userA);
        cache.AddUser(userB);

        User updatedA = new()
        {
            Id = idA,
            Name = "A Updated",
            Email = "a-updated@nm.tv",
            Owner = true,
        };
        cache.UpdateUser(updatedA);

        User? foundA = cache.GetUser(idA);
        User? foundB = cache.GetUser(idB);

        foundA.Should().NotBeNull();
        foundA!.Owner.Should().BeTrue();
        foundA.Name.Should().Be("A Updated");

        foundB.Should().NotBeNull();
        foundB!.Owner.Should().BeFalse();
        foundB.Name.Should().Be("B");
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

        cache.RemoveUser(user);

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

        cache.UpdateUser(user);

        cache.Users.Should().BeEmpty();
    }

    [Fact]
    public void Reset_ClearsUsers()
    {
        UserCache cache = new();
        cache.AddUser(
            new()
            {
                Id = Guid.NewGuid(),
                Name = "User1",
                Email = "u1@nm.tv",
            }
        );
        cache.AddUser(
            new()
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
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = $"User{i}",
                    Email = $"user{i}@nm.tv",
                }
            );
        }

        cache.AddUser(targetUser);

        for (int i = 10; i < 20; i++)
        {
            cache.AddUser(
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = $"User{i}",
                    Email = $"user{i}@nm.tv",
                }
            );
        }

        User? found = cache.GetUser(targetId);

        found.Should().NotBeNull();
        found!.Name.Should().Be("Target");
    }
}
