using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers;
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
        ClaimsPrincipleExtensions.Users.Clear();
        ClaimsPrincipleExtensions.FolderIds.Clear();
        _context.Dispose();
    }

    [Fact]
    public void Initialize_LoadsUsersFromContext()
    {
        ClaimsPrincipleExtensions.Initialize(_context);

        Assert.Single(ClaimsPrincipleExtensions.Users);
        Assert.Equal(SeedConstants.UserId, ClaimsPrincipleExtensions.Users[0].Id);
    }

    [Fact]
    public void Initialize_LoadsFolderIdsFromContext()
    {
        ClaimsPrincipleExtensions.Initialize(_context);

        Assert.Single(ClaimsPrincipleExtensions.FolderIds);
        Assert.Equal(SeedConstants.MovieFolderId, ClaimsPrincipleExtensions.FolderIds[0]);
    }

    [Fact]
    public void NewUserCreatedAfterStartup_IsAccessibleViaAddUser()
    {
        ClaimsPrincipleExtensions.Initialize(_context);

        Guid newUserId = Guid.NewGuid();
        User newUser = new()
        {
            Id = newUserId,
            Email = "new@nomercy.tv",
            Name = "New User",
            Owner = false,
            Allowed = true,
            Manage = false
        };

        ClaimsPrincipleExtensions.AddUser(newUser);

        Assert.Equal(2, ClaimsPrincipleExtensions.Users.Count);
        Assert.Contains(ClaimsPrincipleExtensions.Users, u => u.Id == newUserId);
    }

    [Fact]
    public void DeletedUser_IsRemovedFromList()
    {
        ClaimsPrincipleExtensions.Initialize(_context);

        User existingUser = ClaimsPrincipleExtensions.Users.First();
        ClaimsPrincipleExtensions.RemoveUser(existingUser);

        Assert.Empty(ClaimsPrincipleExtensions.Users);
    }

    [Fact]
    public void RefreshUsers_ReloadsFromDatabase()
    {
        ClaimsPrincipleExtensions.Initialize(_context);

        Guid newUserId = Guid.NewGuid();
        _context.Users.Add(new User
        {
            Id = newUserId,
            Email = "added@nomercy.tv",
            Name = "Added User",
            Owner = false,
            Allowed = true,
            Manage = false
        });
        _context.SaveChanges();

        ClaimsPrincipleExtensions.RefreshUsers(_context);

        Assert.Equal(2, ClaimsPrincipleExtensions.Users.Count);
        Assert.Contains(ClaimsPrincipleExtensions.Users, u => u.Id == newUserId);
    }

    [Fact]
    public void UpdateUser_ReplacesExistingUserInList()
    {
        ClaimsPrincipleExtensions.Initialize(_context);

        User updatedUser = new()
        {
            Id = SeedConstants.UserId,
            Email = "updated@nomercy.tv",
            Name = "Updated User",
            Owner = true,
            Allowed = true,
            Manage = true
        };

        ClaimsPrincipleExtensions.UpdateUser(updatedUser);

        Assert.Single(ClaimsPrincipleExtensions.Users);
        Assert.Equal("Updated User", ClaimsPrincipleExtensions.Users[0].Name);
        Assert.Equal("updated@nomercy.tv", ClaimsPrincipleExtensions.Users[0].Email);
    }

    [Fact]
    public void Initialize_ClearsPreviousData()
    {
        ClaimsPrincipleExtensions.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "stale@nomercy.tv",
            Name = "Stale User",
            Owner = false,
            Allowed = false,
            Manage = false
        });

        ClaimsPrincipleExtensions.Initialize(_context);

        Assert.Single(ClaimsPrincipleExtensions.Users);
        Assert.Equal(SeedConstants.UserId, ClaimsPrincipleExtensions.Users[0].Id);
    }

    [Fact]
    public void NoStaticMediaContext_FieldDoesNotExist()
    {
        System.Reflection.FieldInfo? field = typeof(ClaimsPrincipleExtensions)
            .GetField("MediaContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.Null(field);
    }
}
