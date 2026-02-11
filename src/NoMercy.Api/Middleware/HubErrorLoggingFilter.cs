using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
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
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.Api.Middleware;

/// <summary>
/// SignalR hub filter that logs errors for invalid method calls, wrong arguments, and exceptions.
/// This helps debug client-side calls to hub methods that don't exist or have incorrect parameters.
/// </summary>
public class HubErrorLoggingFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        string hubName = invocationContext.Hub.GetType().Name;
        string methodName = invocationContext.HubMethodName;
        string connectionId = invocationContext.Context.ConnectionId;
        
        string? guid = invocationContext.Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (guid == null)
        {
            Logger.Socket($"[Unknown User]: [{hubName}] No user identifier found in claims.");
            return await next(invocationContext);
        }
        
        Guid userId = Guid.Parse(guid);
        User? user = ClaimsPrincipleExtensions.Users.FirstOrDefault(x => x.Id.Equals(userId));

        if (user == null)
        {
            Logger.Socket($"[Unknown User]: [{hubName}] User with ID {userId} not found.");
            return await next(invocationContext);
        }
            
        try
        {
            // Log the method invocation for debugging
            // Logger.Socket($"{user.Name}: [{hubName}] Invoking method '{methodName}' from connection {connectionId}");

            if (invocationContext.HubMethodArguments.Count > 0)
            {
                string args = string.Join(", ", invocationContext.HubMethodArguments.Select((arg, index) =>
                {
                    if (arg == null) return $"arg{index}: null";

                    string argType = arg.GetType().Name;
                    string? argValue = arg.ToString();

                    // Truncate long string values for cleaner logs
                    if (argValue != null && argValue.Length > 100)
                        argValue = argValue.Substring(0, 100) + "...";

                    return $"arg{index} ({argType}): {argValue}";
                }));

                Logger.Socket($"{user.Name}: [{hubName}.{methodName}] Arguments: {args}");
            }

            // Execute the hub method
            object? result = await next(invocationContext);

            // Logger.Socket($"{user.Name}: [{hubName}.{methodName}] Successfully executed");

            return result;
        }
        catch (HubException hubEx)
        {
            // HubException is thrown intentionally to send error messages to clients
            Logger.Socket($"{user.Name}: [{hubName}.{methodName}] Hub exception: {hubEx.Message}");
            throw; // Re-throw to send to client
        }
        catch (InvalidOperationException invalidOpEx) when (invalidOpEx.Message.Contains("does not exist"))
        {
            // This catches when a client calls a method that doesn't exist
            Logger.Socket($"{user.Name}: [{hubName}] ERROR: Method '{methodName}' does not exist!");
            Logger.Socket($"{user.Name}: [{hubName}] Connection: {connectionId}");
            Logger.Socket(
                $"{user.Name}: [{hubName}] Available methods should match public Task methods in the hub class");

            throw new HubException($"Method '{methodName}' does not exist on hub '{hubName}'");
        }
        catch (ArgumentException argEx)
        {
            // This catches parameter binding errors (wrong types, missing required params, etc.)
            Logger.Socket($"{user.Name}: [{hubName}.{methodName}] ERROR: Invalid arguments");
            Logger.Socket($"{user.Name}: [{hubName}.{methodName}] Details: {argEx.Message}");

            if (invocationContext.HubMethodArguments.Count > 0)
            {
                string argsInfo = string.Join(", ", invocationContext.HubMethodArguments.Select((arg, index) =>
                    $"arg{index}: {arg?.GetType().Name ?? "null"}"
                ));
                Logger.Socket($"{user.Name}: [{hubName}.{methodName}] Provided arguments: {argsInfo}");
            }
            else
            {
                Logger.Socket($"{user.Name}: [{hubName}.{methodName}] No arguments provided");
            }

            throw new HubException($"Invalid arguments for method '{methodName}': {argEx.Message}");
        }
        catch (Exception ex)
        {
            // Catch all other exceptions during method execution
            Logger.Socket($"{user.Name}: [{hubName}.{methodName}] ERROR: Unhandled exception");
            Logger.Socket($"{user.Name}: [{hubName}.{methodName}] Exception type: {ex.GetType().Name}");
            Logger.Socket($"{user.Name}: [{hubName}.{methodName}] Message: {ex.Message}");
            Logger.Socket($"{user.Name}: [{hubName}.{methodName}] Stack trace: {ex.StackTrace}");

            if (invocationContext.HubMethodArguments.Count > 0)
            {
                string argsInfo = string.Join(", ", invocationContext.HubMethodArguments.Select((arg, index) =>
                    $"arg{index}: {arg?.GetType().Name ?? "null"}"
                ));
                Logger.Socket($"{user.Name}: [{hubName}.{methodName}] Arguments: {argsInfo}");
            }

            throw new HubException($"An error occurred calling '{methodName}': {ex.Message}");
        }
        return await next(invocationContext);
    }
}

