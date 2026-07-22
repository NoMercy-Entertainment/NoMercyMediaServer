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

        string? guid = invocationContext.Context.User?.FindFirstValue(claimType: ClaimTypes.NameIdentifier);
        if (guid == null)
        {
            logger.LogInformation(
                message: "[Unknown User]: [{HubName}] No user identifier found in claims.",
                args: hubName
            );
            return await next(arg: invocationContext);
        }

        if (!Guid.TryParse(input: guid, result: out Guid userId))
        {
            logger.LogInformation(
                message: "[{HubName}] Malformed user GUID claim '{Guid}' on connection {ConnectionId}", args: [hubName, guid, connectionId]
            );
            return await next(arg: invocationContext);
        }
        User? user = UserCache.Current.Users.FirstOrDefault(predicate: x => x.Id.Equals(g: userId));

        if (user == null)
        {
            logger.LogInformation(
                message: "[Unknown User]: [{HubName}] User with ID {UserId} not found.", args: [hubName, userId]
            );
            return await next(arg: invocationContext);
        }

        try
        {
            // Execute the hub method with SQLite retry protection.
            // FlexLabs.Upsert (used in VideoHub.SetTime etc.) calls ExecuteSqlRawAsync
            // which bypasses the EF Core execution strategy's retry pipeline.
            return await SqliteRetryingExecutionStrategy.ExecuteWithRetryAsync(operation: async () =>
                await next(arg: invocationContext)
            );
        }
        catch (HubException hubEx)
        {
            // HubException is thrown intentionally to send error messages to clients
            logger.LogInformation(
                message: "{Name}: [{HubName}.{MethodName}] Hub exception: {Message}", args: [user.Name, hubName, methodName, hubEx.Message]
            );
            throw; // Re-throw to send to client
        }
        catch (InvalidOperationException invalidOpEx)
            when (invalidOpEx.Message.Contains(value: "does not exist"))
        {
            // This catches when a client calls a method that doesn't exist
            logger.LogInformation(
                message: "{Name}: [{HubName}] ERROR: Method '{MethodName}' does not exist!", args: [user.Name, hubName, methodName]
            );
            logger.LogInformation(
                message: "{Name}: [{HubName}] Connection: {ConnectionId}", args: [user.Name, hubName, connectionId]
            );
            logger.LogInformation(
                message: "{Name}: [{HubName}] Available methods should match public Task methods in the hub class", args: [user.Name, hubName]
            );

            throw new HubException(
                message: Config.IsDev
                    ? $"Method '{methodName}' does not exist on hub '{hubName}'"
                    : "An internal error occurred"
            );
        }
        catch (ArgumentException argEx)
        {
            // This catches parameter binding errors (wrong types, missing required params, etc.)
            logger.LogInformation(
                message: "{Name}: [{HubName}.{MethodName}] ERROR: Invalid arguments", args: [user.Name, hubName, methodName]
            );
            logger.LogInformation(
                message: "{Name}: [{HubName}.{MethodName}] Details: {Message}", args: [user.Name, hubName, methodName, argEx.Message]
            );

            if (invocationContext.HubMethodArguments.Count > 0)
            {
                string argsInfo = string.Join(
                    separator: ", ",
                    values: invocationContext.HubMethodArguments.Select(
                        selector: (arg, index) => $"arg{index}: {arg?.GetType().Name ?? "null"}"
                    )
                );
                logger.LogInformation(
                    message: "{Name}: [{HubName}.{MethodName}] Provided arguments: {ArgsInfo}", args: [user.Name, hubName, methodName, argsInfo]
                );
            }
            else
            {
                logger.LogInformation(
                    message: "{Name}: [{HubName}.{MethodName}] No arguments provided", args: [user.Name, hubName, methodName]
                );
            }

            throw new HubException(
                message: Config.IsDev
                    ? $"Invalid arguments for method '{methodName}': {argEx.Message}"
                    : "An internal error occurred"
            );
        }
        catch (Exception ex)
        {
            // Catch all other exceptions during method execution
            logger.LogInformation(
                message: "{Name}: [{HubName}.{MethodName}] ERROR: Unhandled exception", args: [user.Name, hubName, methodName]
            );
            logger.LogInformation(
                message: "{Name}: [{HubName}.{MethodName}] Exception type: {Name2}", args: [user.Name, hubName, methodName, ex.GetType().Name]
            );
            logger.LogInformation(
                message: "{Name}: [{HubName}.{MethodName}] Message: {Message}", args: [user.Name, hubName, methodName, ex.Message]
            );
            logger.LogInformation(
                message: "{Name}: [{HubName}.{MethodName}] Stack trace: {StackTrace}", args: [user.Name, hubName, methodName, ex.StackTrace]
            );

            if (invocationContext.HubMethodArguments.Count > 0)
            {
                string argsInfo = string.Join(
                    separator: ", ",
                    values: invocationContext.HubMethodArguments.Select(
                        selector: (arg, index) => $"arg{index}: {arg?.GetType().Name ?? "null"}"
                    )
                );
                logger.LogInformation(
                    message: "{Name}: [{HubName}.{MethodName}] Arguments: {ArgsInfo}", args: [user.Name, hubName, methodName, argsInfo]
                );
            }

            throw new HubException(
                message: Config.IsDev
                    ? $"An error occurred calling '{methodName}': {ex.Message}"
                    : "An internal error occurred"
            );
        }
    }
}
