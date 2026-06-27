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
using NoMercy.Authorization;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Collection("ClaimsPrincipalExtensions")]
public class ClaimsPrincipalExtensionsTests : IDisposable
{
    private readonly MediaContext _context;

    public ClaimsPrincipalExtensionsTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
    }

    public void Dispose()
    {
        ClaimsPrincipalExtensions.Reset();
        _context.Dispose();
    }

    [Fact]
    public async Task Initialize_LoadsUsersFromContext()
    {
        await ClaimsPrincipalExtensions.InitializeAsync(_context);

        Assert.Single(ClaimsPrincipalExtensions.Users);
        Assert.Equal(SeedConstants.UserId, ClaimsPrincipalExtensions.Users[0].Id);
    }

    [Fact]
    public async Task Initialize_LoadsFolderIdsFromContext()
    {
        await ClaimsPrincipalExtensions.InitializeAsync(_context);

        Assert.Single(ClaimsPrincipalExtensions.FolderIds);
        Assert.Equal(SeedConstants.MovieFolderId, ClaimsPrincipalExtensions.FolderIds[0]);
    }

    [Fact]
    public async Task NewUserCreatedAfterStartup_IsAccessibleViaAddUser()
    {
        await ClaimsPrincipalExtensions.InitializeAsync(_context);

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

        ClaimsPrincipalExtensions.AddUser(newUser);

        Assert.Equal(2, ClaimsPrincipalExtensions.Users.Count);
        Assert.Contains(ClaimsPrincipalExtensions.Users, u => u.Id == newUserId);
    }

    [Fact]
    public async Task DeletedUser_IsRemovedFromList()
    {
        await ClaimsPrincipalExtensions.InitializeAsync(_context);

        User existingUser = ClaimsPrincipalExtensions.Users.First();
        ClaimsPrincipalExtensions.RemoveUser(existingUser);

        Assert.Empty(ClaimsPrincipalExtensions.Users);
    }

    [Fact]
    public async Task RefreshUsers_ReloadsFromDatabase()
    {
        await ClaimsPrincipalExtensions.InitializeAsync(_context);

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

        await ClaimsPrincipalExtensions.RefreshUsersAsync(_context);

        Assert.Equal(2, ClaimsPrincipalExtensions.Users.Count);
        Assert.Contains(ClaimsPrincipalExtensions.Users, u => u.Id == newUserId);
    }

    [Fact]
    public async Task UpdateUser_ReplacesExistingUserInList()
    {
        await ClaimsPrincipalExtensions.InitializeAsync(_context);

        User updatedUser = new()
        {
            Id = SeedConstants.UserId,
            Email = "updated@nomercy.tv",
            Name = "Updated User",
            Owner = true,
            Allowed = true,
            Manage = true,
        };

        ClaimsPrincipalExtensions.UpdateUser(updatedUser);

        Assert.Single(ClaimsPrincipalExtensions.Users);
        Assert.Equal("Updated User", ClaimsPrincipalExtensions.Users[0].Name);
        Assert.Equal("updated@nomercy.tv", ClaimsPrincipalExtensions.Users[0].Email);
    }

    [Fact]
    public async Task Initialize_ClearsPreviousData()
    {
        ClaimsPrincipalExtensions.AddUser(
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

        await ClaimsPrincipalExtensions.InitializeAsync(_context);

        Assert.Single(ClaimsPrincipalExtensions.Users);
        Assert.Equal(SeedConstants.UserId, ClaimsPrincipalExtensions.Users[0].Id);
    }

    [Fact]
    public void NoStaticMediaContext_FieldDoesNotExist()
    {
        FieldInfo? field = typeof(ClaimsPrincipalExtensions).GetField(
            "MediaContext",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        Assert.Null(field);
    }
}
