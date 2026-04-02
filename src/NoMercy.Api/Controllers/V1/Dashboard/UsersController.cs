using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Users;
using NoMercy.Events;
using NoMercy.Events.Users;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Users")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/users", Order = 10)]
public class UsersController(
    MediaContext mediaContext,
    IDbContextFactory<MediaContext> contextFactory
) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to view users");

        List<User> users = await mediaContext
            .Users.Include(user => user.LibraryUser)
                .ThenInclude(libraryUser => libraryUser.Library)
            .ToListAsync();

        return Ok(
            new DataResponseDto<IEnumerable<PermissionsResponseItemDto>>
            {
                Data = users.Select(user => new PermissionsResponseItemDto(user)),
            }
        );
    }

    [HttpPost]
    public async Task<IActionResult> Store([FromBody] UserRequest request)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to create a user");

        Guid userId = User.UserId();
        User? hasPermission = mediaContext.Users.FirstOrDefault(user => user.Id.Equals(userId));

        if (hasPermission is null || hasPermission.Owner is false)
            return NotFoundResponse("You do not have permission to create a user");

        User? user = await mediaContext
            .Users.Include(user => user.LibraryUser)
            .FirstOrDefaultAsync(user => user.Id == request.Id);

        if (user != null)
            return UnprocessableEntityResponse("User already exists");

        User newUser = new()
        {
            Id = request.Id,
            Email = request.Email,
            Name = request.Name,
            Allowed = true,
            AudioTranscoding = request.AudioTranscoding,
            VideoTranscoding = request.VideoTranscoding,
            NoTranscoding = true,
            Manage = request.Manage,
            Owner = request.Owner,
            LibraryUser =
                request
                    .Libraries?.Select(libraryId => new LibraryUser
                    {
                        LibraryId = libraryId,
                        UserId = userId,
                    })
                    .ToList()
                ?? [],
        };

        mediaContext.Users.Add(newUser);

        await mediaContext.SaveChangesAsync();

        User? createdUser = await mediaContext
            .Users.Include(u => u.LibraryUser)
            .FirstOrDefaultAsync(u => u.Id == newUser.Id);

        if (createdUser is null)
            return UnprocessableEntityResponse("User was created but could not be retrieved");

        ClaimsPrincipleExtensions.AddUser(createdUser);

        return Ok(
            new StatusResponseDto<string>
            {
                Status = "success",
                Message = "User {0} created successfully",
                Data = createdUser.Name,
            }
        );
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Destroy(Guid id)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to delete a user");

        await using MediaContext deleteContext = await contextFactory.CreateDbContextAsync();
        User? user = await deleteContext
            .Users.Include(user => user.LibraryUser)
            .FirstOrDefaultAsync(user => user.Id == id);

        if (user == null)
            return NotFoundResponse("User not found");

        if (user.Owner)
            return UnauthorizedResponse("The owner cannot be deleted");

        deleteContext.Users.Remove(user);
        await deleteContext.SaveChangesAsync();

        ClaimsPrincipleExtensions.RemoveUser(user);

        return Ok(new StatusResponseDto<string> { Status = "success", Message = "User deleted" });
    }

    [HttpGet]
    [Route("permissions")]
    public async Task<IActionResult> PermissionS()
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to view user permissions");

        List<User> users = await mediaContext
            .Users.Include(user => user.LibraryUser)
                .ThenInclude(libraryUser => libraryUser.Library)
            .ToListAsync();

        return Ok(
            new DataResponseDto<IEnumerable<PermissionsResponseItemDto>>
            {
                Data = users.Select(user => new PermissionsResponseItemDto(user)),
            }
        );
    }

    [HttpPatch("notifications")]
    public async Task<IActionResult> NotificationS([FromBody] object request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse(
                "You do not have permission to update notification settings"
            );

        await using MediaContext notifContext = await contextFactory.CreateDbContextAsync();
        User? user = await notifContext
            .Users.Where(user => user.Id.Equals(userId))
            .Include(user => user.LibraryUser)
            .Include(user => user.NotificationUser)
                .ThenInclude(notificationUser => notificationUser.Notification)
            .FirstOrDefaultAsync(user => user.Id.Equals(userId));

        if (user == null)
            return NotFoundResponse("User not found");

        // TODO Implement notification settings.

        return Ok(
            new StatusResponseDto<string>
            {
                Status = "success",
                Message = "Notification settings updated",
            }
        );
    }

    [HttpGet]
    [Route("{id:guid}/permissions")]
    public async Task<IActionResult> UserPermissions(Guid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view user permissions");

        if (User.IsSelf(id))
            return UnauthorizedResponse("You do not have permission to edit your own permissions");

        User? user = await mediaContext
            .Users.Where(user => user.Id == id)
            .Include(user => user.LibraryUser)
                .ThenInclude(libraryUser => libraryUser.Library)
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFoundResponse("User not found");

        return Ok(new DataResponseDto<UserPermissionRequest> { Data = new(user) });
    }

    [HttpPatch("{id:guid}/permissions")]
    public async Task<IActionResult> UserPermissionUpdate(
        Guid id,
        [FromBody] UserPermissionRequest request
    )
    {
        Guid userId = User.UserId();
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update a user");

        if (User.IsSelf(id))
            return UnauthorizedResponse(
                "You do not have permission to update your own permissions"
            );

        await using MediaContext permContext = await contextFactory.CreateDbContextAsync();
        User? user = await permContext
            .Users.Include(user => user.LibraryUser)
            .FirstOrDefaultAsync(user => user.Id == id);

        if (user == null)
            return NotFoundResponse("User not found");

        if (User.IsOwner())
            user.Manage = request.Manage;

        user.Allowed = request.Allowed;
        user.AudioTranscoding = request.AudioTranscoding;
        user.VideoTranscoding = request.VideoTranscoding;
        user.NoTranscoding = request.NoTranscoding;

        user.LibraryUser.Clear();

        foreach (Ulid libraryId in request.Libraries)
            user.LibraryUser.Add(new() { LibraryId = libraryId, UserId = userId });

        await permContext.SaveChangesAsync();

        ClaimsPrincipleExtensions.UpdateUser(user);

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                new UserPermissionsChangedEvent { UserId = id, ChangedBy = userId }
            );

        return Ok(new StatusResponseDto<string> { Status = "success", Message = "User updated" });
    }

    [HttpPatch("{id:guid}/notifications")]
    public async Task<IActionResult> UserNotification(Guid id, [FromBody] object request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse(
                "You do not have permission to update notification settings"
            );

        await using MediaContext userNotifContext = await contextFactory.CreateDbContextAsync();
        User? user = await userNotifContext
            .Users.Where(user => user.Id.Equals(userId))
            .Include(user => user.LibraryUser)
            .Include(user => user.NotificationUser)
                .ThenInclude(notificationUser => notificationUser.Notification)
            .FirstOrDefaultAsync(user => user.Id.Equals(userId));

        if (user == null)
            return NotFoundResponse("User not found");

        // TODO Implement notification settings.

        return Ok(
            new StatusResponseDto<string>
            {
                Status = "success",
                Message = "Notification settings updated",
            }
        );
    }
}
