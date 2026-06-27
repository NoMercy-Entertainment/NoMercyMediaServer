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

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.WebSockets;
using NoMercy.Authorization;
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Networking;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Api.Hubs;

public class DashboardHub : ConnectionHub
{
    private readonly IClientMessenger _clientMessenger;

    public DashboardHub(
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<MediaContext> contextFactory,
        ConnectedClients connectedClients,
        IClientMessenger clientMessenger,
        IActivityLogger activityLogger
    )
        : base(httpContextAccessor, contextFactory, connectedClients, activityLogger)
    {
        _clientMessenger = clientMessenger;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        if (AuthPolicy.IsModerator(Context.User))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "moderators");
        }

        Logger.Socket("Dashboard client connected", LogEventLevel.Debug);
        LogBroadcaster.StartBroadcasting(_clientMessenger);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "moderators");
        Logger.Socket("Dashboard client disconnected", LogEventLevel.Debug);

        StopResources();
    }

    public void StartResources()
    {
        ResourceMonitor.StartBroadcasting(_clientMessenger);
    }

    public void StopResources()
    {
        if (ConnectedClients.Clients.Values.All(x => x.Endpoint != "/dashboardHub"))
        {
            ResourceMonitor.StopBroadcasting();
            LogBroadcaster.StopBroadcasting();
        }
    }
}
