using System.Security.Authentication;
using System.Security.Claims;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Networking;

public static class ClaimsPrincipleExtensions
{
    private static readonly MediaContext MediaContext = new();

    public static readonly List<User> Users = MediaContext.Users.ToList();
    public static readonly List<Ulid> FolderIds = MediaContext.Folders.Select(x => x.Id).ToList();

    private static readonly User Owner = Users.FirstOrDefault(u => u.Owner) ?? throw new InvalidOperationException();
    private static List<User> ManagerUsers => Users.Where(u => u.Manage).ToList();
    private static List<User> AllowedUsers => Users.Where(u => u.Allowed).ToList();

    public static Guid UserId(this ClaimsPrincipal? principal)
    {
        string? userId = principal?
            .FindFirst(ClaimTypes.NameIdentifier)?
            .Value;

        return Guid.TryParse(userId, out Guid parsedUserId)
            ? parsedUserId
            : throw new AuthenticationException("User ID not found");
    }

    public static string Role(this ClaimsPrincipal? principal)
    {
        return principal?
                   .FindFirst(ClaimTypes.Role)?
                   .Value
               ?? throw new AuthenticationException("Role not found");
    }

    public static string UserName(this ClaimsPrincipal? principal)
    {
        try
        {
            return principal?.FindFirst("name")?.Value
                   ?? principal?.FindFirst(ClaimTypes.GivenName)?.Value + " " +
                   principal?.FindFirst(ClaimTypes.Surname)?.Value;
        }
        catch (Exception e)
        {
            throw new AuthenticationException("User name not found");
        }
    }

    public static string Email(this ClaimsPrincipal? principal)
    {
        try
        {
            return principal?.FindFirst(ClaimTypes.Email)?.Value
                   ?? throw new AuthenticationException("Email not found");
        }
        catch (Exception e)
        {
            throw new AuthenticationException("User name not found");
        }
    }

    public static bool IsOwner(this ClaimsPrincipal? principal)
    {
        return principal?.UserId() == Owner.Id;
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
        return principal.UserId() == userId;
    }

    public static User? User(this ClaimsPrincipal? principal)
    {
        return Users.FirstOrDefault(u => u.Id == principal.UserId());
    }

    public static void AddUser(User user)
    {
        Users.Add(user);
    }

    public static void RemoveUser(User user)
    {
        Users.Remove(user);
    }

    public static void UpdateUser(User user)
    {
        User? existingUser = Users.FirstOrDefault(u => u.Id == user.Id);

        if (existingUser != null)
        {
            Users.Remove(existingUser);
            Users.Add(user);
        }
    }
}