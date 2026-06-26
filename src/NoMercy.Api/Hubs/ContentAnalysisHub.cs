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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Activity;
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
    ConnectedClients connectedClients,
    IActivityLogger activityLogger
) : ConnectionHub(httpContextAccessor, contextFactory, connectedClients, activityLogger) { }
