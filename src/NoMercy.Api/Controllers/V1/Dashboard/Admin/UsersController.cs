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
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Users;
using NoMercy.Events;
using NoMercy.Events.Users;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

[ApiController]
[Tags(tags: "Dashboard Users")]
[ApiVersion(version: 1.0)]
[Authorize]
[Route(template: "api/v{version:apiVersion}/dashboard/users", Order = 10)]
public class UsersController(IUserRepository userRepository) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "Owner")]
    public async Task<IActionResult> Index()
    {
        List<User> users = await userRepository.GetAllWithLibrariesAsync();

        return Ok(
            value: new DataResponseDto<IEnumerable<PermissionsResponseItemDto>>
            {
                Data = users.Select(selector: user => new PermissionsResponseItemDto(user: user)),
            }
        );
    }

    [HttpPost]
    [Authorize(Policy = "Owner")]
    public async Task<IActionResult> Store([FromBody] UserRequest request)
    {
        Guid userId = User.UserId();
        User? hasPermission = await userRepository.GetByIdAsync(userId: userId);

        if (hasPermission is null || hasPermission.Owner is false)
            return NotFoundResponse(detail: "You do not have permission to create a user");

        bool alreadyExists = await userRepository.ExistsAsync(userId: request.Id);

        if (alreadyExists)
            return UnprocessableEntityResponse(detail: "User already exists");

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
                    .Libraries?.Select(selector: libraryId => new LibraryUser
                    {
                        LibraryId = libraryId,
                        UserId = userId,
                    })
                    .ToList()
                ?? [],
        };

        await userRepository.AddAsync(user: newUser);

        User? createdUser = await userRepository.GetByIdWithLibrariesAfterAddAsync(userId: newUser.Id);

        if (createdUser is null)
            return UnprocessableEntityResponse(detail: "User was created but could not be retrieved");

        UserCacheService.AddUser(user: createdUser);

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "success",
                Message = "User {0} created successfully",
                Data = createdUser.Name,
            }
        );
    }

    [HttpDelete(template: "{id:guid}")]
    [Authorize(Policy = "Owner")]
    public async Task<IActionResult> Destroy(Guid id)
    {
        User? user = await userRepository.GetByIdWithLibrariesAsync(userId: id);

        if (user is null)
            return NotFoundResponse(detail: "User not found");

        if (user.Owner)
            return UnauthorizedResponse(detail: "The owner cannot be deleted");

        await userRepository.DeleteAsync(userId: id);

        UserCacheService.RemoveUser(user: user);

        return Ok(value: new StatusResponseDto<string> { Status = "success", Message = "User deleted" });
    }

    [HttpGet(template: "{id:guid}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Show(Guid id)
    {
        User? user = await userRepository.GetByIdWithLibrariesAsync(userId: id);

        if (user is null)
            return NotFoundResponse(detail: "User not found");

        return Ok(value: new DataResponseDto<PermissionsResponseItemDto> { Data = new(user: user) });
    }

    [HttpGet]
    [Route(template: "permissions")]
    [Authorize(Policy = "Owner")]
    public async Task<IActionResult> PermissionS()
    {
        List<User> users = await userRepository.GetAllWithLibrariesAsync();

        return Ok(
            value: new DataResponseDto<IEnumerable<PermissionsResponseItemDto>>
            {
                Data = users.Select(selector: user => new PermissionsResponseItemDto(user: user)),
            }
        );
    }

    [HttpPatch(template: "notifications")]
    public async Task<IActionResult> NotificationS([FromBody] object request)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(
                detail: "You do not have permission to update notification settings"
            );

        User? user = await userRepository.GetByIdWithNotificationsAsync(userId: userId);

        if (user is null)
            return NotFoundResponse(detail: "User not found");

        // TODO Implement notification settings.

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "success",
                Message = "Notification settings updated",
            }
        );
    }

    [HttpGet]
    [Route(template: "{id:guid}/permissions")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> UserPermissions(Guid id)
    {
        if (User.IsSelf(userId: id))
            return UnauthorizedResponse(detail: "You do not have permission to edit your own permissions");

        User? user = await userRepository.GetByIdWithLibrariesAsync(userId: id);

        if (user is null)
            return NotFoundResponse(detail: "User not found");

        return Ok(value: new DataResponseDto<UserPermissionRequest> { Data = new(user: user) });
    }

    [HttpPatch(template: "{id:guid}/permissions")]
    public async Task<IActionResult> UserPermissionUpdate(
        Guid id,
        [FromBody] UserPermissionRequest request
    )
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsModerator(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to update a user");

        if (User.IsSelf(userId: id))
            return UnauthorizedResponse(
                detail: "You do not have permission to update your own permissions"
            );

        User? existing = await userRepository.GetByIdWithLibrariesAsync(userId: id);

        if (existing is null)
            return NotFoundResponse(detail: "User not found");

        bool? manage = AuthPolicy.IsOwner(principal: User) ? request.Manage : null;

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

        User? updatedUser = await userRepository.GetByIdWithLibrariesAsync(userId: id);

        if (updatedUser is not null)
            UserCacheService.UpdateUser(user: updatedUser);

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                @event: new UserPermissionsChangedEvent { UserId = id, ChangedBy = userId }
            );

        return Ok(value: new StatusResponseDto<string> { Status = "success", Message = "User updated" });
    }

    [HttpPatch(template: "{id:guid}/notifications")]
    public async Task<IActionResult> UserNotification(Guid id, [FromBody] object request)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(
                detail: "You do not have permission to update notification settings"
            );

        User? user = await userRepository.GetByIdWithNotificationsAsync(userId: userId);

        if (user is null)
            return NotFoundResponse(detail: "User not found");

        // TODO Implement notification settings.

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "success",
                Message = "Notification settings updated",
            }
        );
    }
}
