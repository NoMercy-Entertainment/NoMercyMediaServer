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

using System.Security.Claims;
using NoMercy.Database;
using NoMercy.Database.Models.Users;

namespace NoMercy.Authorization;

public static class ClaimsPrincipalExtensions
{
    // ---- Stateless claim readers (genuine extension methods) ----

    public static Guid UserId(this ClaimsPrincipal? principal)
    {
        string? userId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userId, out Guid parsedUserId) ? parsedUserId : Guid.Empty;
    }

    public static string Role(this ClaimsPrincipal? principal) =>
        principal?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

    public static string UserName(this ClaimsPrincipal? principal)
    {
        string? nameValue = principal?.FindFirst("name")?.Value;
        if (nameValue is not null)
            return nameValue;

        string given = principal?.FindFirst(ClaimTypes.GivenName)?.Value ?? string.Empty;
        string surname = principal?.FindFirst(ClaimTypes.Surname)?.Value ?? string.Empty;
        return $"{given} {surname}".Trim();
    }

    public static string Email(this ClaimsPrincipal? principal) =>
        principal?.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

    public static bool IsSelf(this ClaimsPrincipal? principal, Guid userId) =>
        principal.UserId().Equals(userId);

    // ---- TRANSITIONAL delegators ----
    // Preserve the original static API while consumers migrate to the injected
    // IUserCache / IMediaAuthorizationPolicy. They route through UserCache.Current
    // (the same instance registered in DI) so there is one source of truth.
    // Removed once all call sites use the injected services.

    private static readonly MediaAuthorizationPolicy Policy = new(UserCache.Current);

    public static bool IsOwner(this ClaimsPrincipal? principal) => Policy.IsOwner(principal);

    public static bool IsModerator(this ClaimsPrincipal? principal) => Policy.IsModerator(principal);

    public static bool IsAllowed(this ClaimsPrincipal? principal) => Policy.IsAllowed(principal);

    public static User? User(this ClaimsPrincipal? principal) =>
        UserCache.Current.GetUser(principal.UserId());

    public static List<User> Users => [.. UserCache.Current.Users];

    public static List<Ulid> FolderIds => [.. UserCache.Current.FolderIds];

    public static Task InitializeAsync(MediaContext context) =>
        UserCache.Current.InitializeAsync(context);

    public static Task RefreshUsersAsync(MediaContext context) =>
        UserCache.Current.RefreshUsersAsync(context);

    public static Task RefreshFolderIdsAsync(MediaContext context) =>
        UserCache.Current.RefreshFolderIdsAsync(context);

    public static void AddUser(User user) => UserCache.Current.AddUser(user);

    public static void RemoveUser(User user) => UserCache.Current.RemoveUser(user);

    public static void UpdateUser(User user) => UserCache.Current.UpdateUser(user);

    public static void Reset() => UserCache.Current.Reset();
}
