using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NoMercy.Api.Hubs;
using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Api.Services.LiveTranscode;

public sealed class SignalRLiveSessionTransport(
    IHubContext<LiveTranscodeHub> hubContext,
    ILogger<SignalRLiveSessionTransport> logger
) : ILiveSessionTransport
{
    public async Task SendToClientAsync(string sessionId, object message, CancellationToken ct)
    {
        string groupName = LiveTranscodeHub.GroupName(sessionId);
        string eventName = message.GetType().Name;

        try
        {
            await hubContext
                .Clients.Group(groupName)
                .SendAsync(eventName, message, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "SignalR send failed for event {Event} on session {SessionId}",
                eventName,
                sessionId
            );
        }
    }
}
