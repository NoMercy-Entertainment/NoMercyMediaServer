using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Networking;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.Hubs;

/// <summary>
/// SignalR hub for real-time content-analysis progress events.
/// Currently surfaces:
/// <list type="bullet">
///   <item>
///     <description>
///       <c>WhisperProgress</c> — emitted by the Whisper transcription
///       endpoint as it processes each audio chunk. Payload:
///       <c>{ video_file_id: string, percent_done: double }</c>.
///     </description>
///   </item>
/// </list>
/// Clients subscribe on <c>/contentAnalysisHub</c>. Authentication via the
/// standard Bearer / <c>?access_token=</c> flow (handled by
/// <c>TokenParamAuthMiddleware</c>).
/// </summary>
[Authorize]
public class ContentAnalysisHub(
    IHttpContextAccessor httpContextAccessor,
    IDbContextFactory<MediaContext> contextFactory,
    ConnectedClients connectedClients
) : ConnectionHub(httpContextAccessor, contextFactory, connectedClients) { }
