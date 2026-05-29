using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Helpers.Extensions;
using NoMercy.Networking;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.Hubs;

[Authorize]
public class LiveTranscodeHub(
    IHttpContextAccessor httpContextAccessor,
    IDbContextFactory<MediaContext> contextFactory,
    ConnectedClients connectedClients,
    IActivityLogger activityLogger,
    ISessionManager sessionManager,
    ILiveStreamingService streamingService,
    ILogger<LiveTranscodeHub> logger
) : ConnectionHub(httpContextAccessor, contextFactory, connectedClients, activityLogger)
{
    public static string GroupName(string sessionId) => $"live-{sessionId}";

    /// <summary>
    /// Client calls this after receiving the session id from POST /sessions to
    /// start receiving server-push events for that session. Validates that the
    /// calling user owns the session before admitting them to the group.
    /// </summary>
    public async Task SubscribeToSession(string sessionId)
    {
        string? ownerId = sessionManager.GetOwnerUserId(sessionId);

        if (ownerId is null)
        {
            logger.LogDebug("SubscribeToSession: session {SessionId} not found", sessionId);
            return;
        }

        string callerId = Context.UserIdentifier ?? string.Empty;

        if (!string.Equals(ownerId, callerId, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "SubscribeToSession: caller {CallerId} is not the owner of session {SessionId}",
                callerId,
                sessionId
            );
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));

        logger.LogDebug(
            "Client {ConnectionId} subscribed to live session {SessionId}",
            Context.ConnectionId,
            sessionId
        );
    }

    /// <summary>
    /// Client leaves the session group. Counterpart to <see cref="SubscribeToSession"/>.
    /// </summary>
    public async Task UnsubscribeFromSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(sessionId));
    }

    /// <summary>
    /// Client heartbeat — updates the session last-activity timestamp so the
    /// idle reaper does not evict an active session.
    /// </summary>
    public void Heartbeat(string sessionId)
    {
        if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return;

        string? ownerId = sessionManager.GetOwnerUserId(sessionId);
        string callerId = Context.UserIdentifier ?? string.Empty;

        if (!string.Equals(ownerId, callerId, StringComparison.OrdinalIgnoreCase))
            return;

        runtime.TouchLastAccess();
    }

    /// <summary>
    /// Client requests the encoder pause (fill buffer to max, stop producing
    /// new segments). Maps to <see cref="ILiveSession.Suspend"/>.
    /// </summary>
    public void RequestPause(string sessionId)
    {
        if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return;

        if (!CallerOwnsSession(sessionId))
            return;

        runtime.Session.Suspend();
    }

    /// <summary>
    /// Client requests the encoder resume after a pause.
    /// Maps to <see cref="ILiveSession.Resume"/>.
    /// </summary>
    public void RequestResume(string sessionId)
    {
        if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return;

        if (!CallerOwnsSession(sessionId))
            return;

        runtime.Session.Resume();
    }

    private bool CallerOwnsSession(string sessionId)
    {
        string? ownerId = sessionManager.GetOwnerUserId(sessionId);
        string callerId = Context.UserIdentifier ?? string.Empty;
        return string.Equals(ownerId, callerId, StringComparison.OrdinalIgnoreCase);
    }
}
