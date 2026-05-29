using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NoMercy.Api.Hubs;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Storage;

namespace NoMercy.Api.Services.LiveTranscode;

public sealed class SignalRLiveSessionTransport(
    IHubContext<LiveTranscodeHub> hubContext,
    ILiveStreamingService streamingService,
    IStorage storage,
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

    public Task<Stream> ServeSegmentAsync(string sessionId, int segmentIndex, CancellationToken ct)
    {
        if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return Task.FromResult(Stream.Null);

        if (!runtime.TryGetSegment(segmentIndex, out Segment segment))
            return Task.FromResult(Stream.Null);

        if (!storage.Exists(segment.FilePath))
            return Task.FromResult(Stream.Null);

        Stream stream = storage.OpenRead(segment.FilePath);
        return Task.FromResult(stream);
    }
}
