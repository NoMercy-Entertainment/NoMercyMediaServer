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

using System.Reflection;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Collection("ClaimsPrincipleExtensions")]
public class ClaimsPrincipleExtensionsTests : IDisposable
{
    private readonly MediaContext _context;

    public ClaimsPrincipleExtensionsTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
    }

    public void Dispose()
    {
        ClaimsPrincipleExtensions.Reset();
        _context.Dispose();
    }

    [Fact]
    public async Task Initialize_LoadsUsersFromContext()
    {
        await ClaimsPrincipleExtensions.InitializeAsync(_context);

        Assert.Single(ClaimsPrincipleExtensions.Users);
        Assert.Equal(SeedConstants.UserId, ClaimsPrincipleExtensions.Users[0].Id);
    }

    [Fact]
    public async Task Initialize_LoadsFolderIdsFromContext()
    {
        await ClaimsPrincipleExtensions.InitializeAsync(_context);

        Assert.Single(ClaimsPrincipleExtensions.FolderIds);
        Assert.Equal(SeedConstants.MovieFolderId, ClaimsPrincipleExtensions.FolderIds[0]);
    }

    [Fact]
    public async Task NewUserCreatedAfterStartup_IsAccessibleViaAddUser()
    {
        await ClaimsPrincipleExtensions.InitializeAsync(_context);

        Guid newUserId = Guid.NewGuid();
        User newUser = new()
        {
            Id = newUserId,
            Email = "new@nomercy.tv",
            Name = "New User",
            Owner = false,
            Allowed = true,
            Manage = false,
        };

        ClaimsPrincipleExtensions.AddUser(newUser);

        Assert.Equal(2, ClaimsPrincipleExtensions.Users.Count);
        Assert.Contains(ClaimsPrincipleExtensions.Users, u => u.Id == newUserId);
    }

    [Fact]
    public async Task DeletedUser_IsRemovedFromList()
    {
        await ClaimsPrincipleExtensions.InitializeAsync(_context);

        User existingUser = ClaimsPrincipleExtensions.Users.First();
        ClaimsPrincipleExtensions.RemoveUser(existingUser);

        Assert.Empty(ClaimsPrincipleExtensions.Users);
    }

    [Fact]
    public async Task RefreshUsers_ReloadsFromDatabase()
    {
        await ClaimsPrincipleExtensions.InitializeAsync(_context);

        Guid newUserId = Guid.NewGuid();
        _context.Users.Add(
            new()
            {
                Id = newUserId,
                Email = "added@nomercy.tv",
                Name = "Added User",
                Owner = false,
                Allowed = true,
                Manage = false,
            }
        );
        await _context.SaveChangesAsync();

        await ClaimsPrincipleExtensions.RefreshUsersAsync(_context);

        Assert.Equal(2, ClaimsPrincipleExtensions.Users.Count);
        Assert.Contains(ClaimsPrincipleExtensions.Users, u => u.Id == newUserId);
    }

    [Fact]
    public async Task UpdateUser_ReplacesExistingUserInList()
    {
        await ClaimsPrincipleExtensions.InitializeAsync(_context);

        User updatedUser = new()
        {
            Id = SeedConstants.UserId,
            Email = "updated@nomercy.tv",
            Name = "Updated User",
            Owner = true,
            Allowed = true,
            Manage = true,
        };

        ClaimsPrincipleExtensions.UpdateUser(updatedUser);

        Assert.Single(ClaimsPrincipleExtensions.Users);
        Assert.Equal("Updated User", ClaimsPrincipleExtensions.Users[0].Name);
        Assert.Equal("updated@nomercy.tv", ClaimsPrincipleExtensions.Users[0].Email);
    }

    [Fact]
    public async Task Initialize_ClearsPreviousData()
    {
        ClaimsPrincipleExtensions.AddUser(
            new()
            {
                Id = Guid.NewGuid(),
                Email = "stale@nomercy.tv",
                Name = "Stale User",
                Owner = false,
                Allowed = false,
                Manage = false,
            }
        );

        await ClaimsPrincipleExtensions.InitializeAsync(_context);

        Assert.Single(ClaimsPrincipleExtensions.Users);
        Assert.Equal(SeedConstants.UserId, ClaimsPrincipleExtensions.Users[0].Id);
    }

    [Fact]
    public void NoStaticMediaContext_FieldDoesNotExist()
    {
        FieldInfo? field = typeof(ClaimsPrincipleExtensions).GetField(
            "MediaContext",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        Assert.Null(field);
    }
}
