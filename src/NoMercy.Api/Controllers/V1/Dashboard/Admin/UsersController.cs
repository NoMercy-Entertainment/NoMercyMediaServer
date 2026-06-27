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

/*
 * Copyright (c) NoMercy Entertainment. All Rights Reserved.
 */

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Users;
using NoMercy.Events;
using NoMercy.Events.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.Authorization;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

[ApiController]
[Tags("Dashboard Users")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/users", Order = 10)]
public class UsersController(IUserRepository userRepository) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!AuthPolicy.IsOwner(User))
            return UnauthorizedResponse("You do not have permission to view users");

        List<User> users = await userRepository.GetAllWithLibrariesAsync();

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
        if (!AuthPolicy.IsOwner(User))
            return UnauthorizedResponse("You do not have permission to create a user");

        Guid userId = User.UserId();
        User? hasPermission = await userRepository.GetByIdAsync(userId);

        if (hasPermission is null || hasPermission.Owner is false)
            return NotFoundResponse("You do not have permission to create a user");

        bool alreadyExists = await userRepository.ExistsAsync(request.Id);

        if (alreadyExists)
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

        await userRepository.AddAsync(newUser);

        User? createdUser = await userRepository.GetByIdWithLibrariesAfterAddAsync(newUser.Id);

        if (createdUser is null)
            return UnprocessableEntityResponse("User was created but could not be retrieved");

        UserCacheService.AddUser(createdUser);

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
        if (!AuthPolicy.IsOwner(User))
            return UnauthorizedResponse("You do not have permission to delete a user");

        User? user = await userRepository.GetByIdWithLibrariesAsync(id);

        if (user is null)
            return NotFoundResponse("User not found");

        if (user.Owner)
            return UnauthorizedResponse("The owner cannot be deleted");

        await userRepository.DeleteAsync(id);

        UserCacheService.RemoveUser(user);

        return Ok(new StatusResponseDto<string> { Status = "success", Message = "User deleted" });
    }

    [HttpGet]
    [Route("permissions")]
    public async Task<IActionResult> PermissionS()
    {
        if (!AuthPolicy.IsOwner(User))
            return UnauthorizedResponse("You do not have permission to view user permissions");

        List<User> users = await userRepository.GetAllWithLibrariesAsync();

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
        if (!AuthPolicy.IsAllowed(User))
            return UnauthorizedResponse(
                "You do not have permission to update notification settings"
            );

        User? user = await userRepository.GetByIdWithNotificationsAsync(userId);

        if (user is null)
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
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to view user permissions");

        if (User.IsSelf(id))
            return UnauthorizedResponse("You do not have permission to edit your own permissions");

        User? user = await userRepository.GetByIdWithLibrariesAsync(id);

        if (user is null)
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
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to update a user");

        if (User.IsSelf(id))
            return UnauthorizedResponse(
                "You do not have permission to update your own permissions"
            );

        User? existing = await userRepository.GetByIdWithLibrariesAsync(id);

        if (existing is null)
            return NotFoundResponse("User not found");

        bool? manage = AuthPolicy.IsOwner(User) ? request.Manage : null;

        await userRepository.UpdatePermissionsAsync(
            targetUserId: id,
            actingUserId: userId,
            allowed: request.Allowed,
            audioTranscoding: request.AudioTranscoding,
            videoTranscoding: request.VideoTranscoding,
            noTranscoding: request.NoTranscoding,
            manage: manage,
            libraryIds: request.Libraries
        );

        User? updatedUser = await userRepository.GetByIdWithLibrariesAsync(id);

        if (updatedUser is not null)
            UserCacheService.UpdateUser(updatedUser);

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
        if (!AuthPolicy.IsAllowed(User))
            return UnauthorizedResponse(
                "You do not have permission to update notification settings"
            );

        User? user = await userRepository.GetByIdWithNotificationsAsync(userId);

        if (user is null)
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
