using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Users;

namespace NoMercy.Helpers.Extensions;

public static class ClaimsPrincipleExtensions
{
    private static readonly Lock UsersLock = new();
    private static readonly Lock FolderIdsLock = new();

    private static List<User> _users = [];
    private static List<Ulid> _folderIds = [];

    public static List<User> Users
    {
        get
        {
            lock (UsersLock)
                return [.. _users];
        }
    }

    public static List<Ulid> FolderIds
    {
        get
        {
            lock (FolderIdsLock)
                return [.. _folderIds];
        }
    }

    private static User? Owner
    {
        get
        {
            lock (UsersLock)
                return _users.FirstOrDefault(u => u.Owner);
        }
    }

    private static List<User> ManagerUsers
    {
        get
        {
            lock (UsersLock)
                return _users.Where(u => u.Manage).ToList();
        }
    }

    private static List<User> AllowedUsers
    {
        get
        {
            lock (UsersLock)
                return _users.Where(u => u.Allowed).ToList();
        }
    }

    public static async Task InitializeAsync(MediaContext context)
    {
        List<User> users = await context.Users.AsNoTracking().ToListAsync();
        List<Ulid> folderIds = await context.Folders.AsNoTracking().Select(x => x.Id).ToListAsync();

        lock (UsersLock)
            _users = users;
        lock (FolderIdsLock)
            _folderIds = folderIds;
    }

    public static async Task RefreshUsersAsync(MediaContext context)
    {
        List<User> users = await context.Users.AsNoTracking().ToListAsync();
        lock (UsersLock)
            _users = users;
    }

    public static async Task RefreshFolderIdsAsync(MediaContext context)
    {
        List<Ulid> folderIds = await context.Folders.AsNoTracking().Select(x => x.Id).ToListAsync();
        lock (FolderIdsLock)
            _folderIds = folderIds;
    }

    public static Guid UserId(this ClaimsPrincipal? principal)
    {
        string? userId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(userId, out Guid parsedUserId) ? parsedUserId : Guid.Empty;
    }

    public static string Role(this ClaimsPrincipal? principal)
    {
        return principal?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    }

    public static string UserName(this ClaimsPrincipal? principal)
    {
        string? nameValue = principal?.FindFirst("name")?.Value;
        if (nameValue is not null)
            return nameValue;

        string given = principal?.FindFirst(ClaimTypes.GivenName)?.Value ?? string.Empty;
        string surname = principal?.FindFirst(ClaimTypes.Surname)?.Value ?? string.Empty;
        return $"{given} {surname}".Trim();
    }

    public static string Email(this ClaimsPrincipal? principal)
    {
        return principal?.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
    }

    public static bool IsOwner(this ClaimsPrincipal? principal)
    {
        return Owner is not null && principal?.UserId() == Owner.Id;
    }

    public static bool IsModerator(this ClaimsPrincipal? principal)
    {
        return ManagerUsers.Any(u => u.Id == principal.UserId()) || principal.IsOwner();
    }

    public static bool IsAllowed(this ClaimsPrincipal? principal)
    {
        return AllowedUsers.Any(u => u.Id == principal.UserId()) || principal.IsOwner();
    }

    public static bool IsSelf(this ClaimsPrincipal? principal, Guid userId)
    {
        return principal.UserId().Equals(userId);
    }

    public static User? User(this ClaimsPrincipal? principal)
    {
        lock (UsersLock)
            return _users.FirstOrDefault(u => u.Id == principal.UserId());
    }

    public static void AddUser(User user)
    {
        lock (UsersLock)
            _users = [.. _users, user];
    }

    public static void RemoveUser(User user)
    {
        lock (UsersLock)
            _users = _users.Where(u => u.Id != user.Id).ToList();
    }

    public static void UpdateUser(User user)
    {
        lock (UsersLock)
        {
            _users = _users.Select(u => u.Id == user.Id ? user : u).ToList();
        }
    }

    public static void Reset()
    {
        lock (UsersLock)
            _users = [];
        lock (FolderIdsLock)
            _folderIds = [];
    }
}
