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
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Information;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.Api.Middleware;

/// <summary>
/// SignalR hub filter that logs errors for invalid method calls, wrong arguments, and exceptions.
/// This helps debug client-side calls to hub methods that don't exist or have incorrect parameters.
/// </summary>
public class HubErrorLoggingFilter(ILogger<HubErrorLoggingFilter> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next
    )
    {
        string hubName = invocationContext.Hub.GetType().Name;
        string methodName = invocationContext.HubMethodName;
        string connectionId = invocationContext.Context.ConnectionId;

        string? guid = invocationContext.Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (guid == null)
        {
            logger.LogInformation(
                $"[Unknown User]: [{hubName}] No user identifier found in claims."
            );
            return await next(invocationContext);
        }

        if (!Guid.TryParse(guid, out Guid userId))
        {
            logger.LogInformation(
                $"[{hubName}] Malformed user GUID claim '{guid}' on connection {connectionId}"
            );
            return await next(invocationContext);
        }
        User? user = UserCache.Current.Users.FirstOrDefault(x => x.Id.Equals(userId));

        if (user == null)
        {
            logger.LogInformation($"[Unknown User]: [{hubName}] User with ID {userId} not found.");
            return await next(invocationContext);
        }

        try
        {
            // Execute the hub method with SQLite retry protection.
            // FlexLabs.Upsert (used in VideoHub.SetTime etc.) calls ExecuteSqlRawAsync
            // which bypasses the EF Core execution strategy's retry pipeline.
            return await SqliteRetryingExecutionStrategy.ExecuteWithRetryAsync(async () =>
                await next(invocationContext)
            );
        }
        catch (HubException hubEx)
        {
            // HubException is thrown intentionally to send error messages to clients
            logger.LogInformation(
                $"{user.Name}: [{hubName}.{methodName}] Hub exception: {hubEx.Message}"
            );
            throw; // Re-throw to send to client
        }
        catch (InvalidOperationException invalidOpEx)
            when (invalidOpEx.Message.Contains("does not exist"))
        {
            // This catches when a client calls a method that doesn't exist
            logger.LogInformation(
                $"{user.Name}: [{hubName}] ERROR: Method '{methodName}' does not exist!"
            );
            logger.LogInformation($"{user.Name}: [{hubName}] Connection: {connectionId}");
            logger.LogInformation(
                $"{user.Name}: [{hubName}] Available methods should match public Task methods in the hub class"
            );

            throw new HubException(
                Config.IsDev
                    ? $"Method '{methodName}' does not exist on hub '{hubName}'"
                    : "An internal error occurred"
            );
        }
        catch (ArgumentException argEx)
        {
            // This catches parameter binding errors (wrong types, missing required params, etc.)
            logger.LogInformation(
                $"{user.Name}: [{hubName}.{methodName}] ERROR: Invalid arguments"
            );
            logger.LogInformation(
                $"{user.Name}: [{hubName}.{methodName}] Details: {argEx.Message}"
            );

            if (invocationContext.HubMethodArguments.Count > 0)
            {
                string argsInfo = string.Join(
                    ", ",
                    invocationContext.HubMethodArguments.Select(
                        (arg, index) => $"arg{index}: {arg?.GetType().Name ?? "null"}"
                    )
                );
                logger.LogInformation(
                    $"{user.Name}: [{hubName}.{methodName}] Provided arguments: {argsInfo}"
                );
            }
            else
            {
                logger.LogInformation(
                    $"{user.Name}: [{hubName}.{methodName}] No arguments provided"
                );
            }

            throw new HubException(
                Config.IsDev
                    ? $"Invalid arguments for method '{methodName}': {argEx.Message}"
                    : "An internal error occurred"
            );
        }
        catch (Exception ex)
        {
            // Catch all other exceptions during method execution
            logger.LogInformation(
                $"{user.Name}: [{hubName}.{methodName}] ERROR: Unhandled exception"
            );
            logger.LogInformation(
                $"{user.Name}: [{hubName}.{methodName}] Exception type: {ex.GetType().Name}"
            );
            logger.LogInformation($"{user.Name}: [{hubName}.{methodName}] Message: {ex.Message}");
            logger.LogInformation(
                $"{user.Name}: [{hubName}.{methodName}] Stack trace: {ex.StackTrace}"
            );

            if (invocationContext.HubMethodArguments.Count > 0)
            {
                string argsInfo = string.Join(
                    ", ",
                    invocationContext.HubMethodArguments.Select(
                        (arg, index) => $"arg{index}: {arg?.GetType().Name ?? "null"}"
                    )
                );
                logger.LogInformation(
                    $"{user.Name}: [{hubName}.{methodName}] Arguments: {argsInfo}"
                );
            }

            throw new HubException(
                Config.IsDev
                    ? $"An error occurred calling '{methodName}': {ex.Message}"
                    : "An internal error occurred"
            );
        }
    }
}
