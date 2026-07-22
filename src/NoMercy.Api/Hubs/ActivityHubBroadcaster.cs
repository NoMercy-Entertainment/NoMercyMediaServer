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

using Microsoft.AspNetCore.SignalR;
using NoMercy.Data.Activity;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.Hubs;

public class ActivityHubBroadcaster : IActivityHubBroadcaster
{
    private const string ModeratorsGroup = "moderators";
    private const string EventName = "ActivityLogged";

    private readonly IHubContext<DashboardHub> _hubContext;

    public ActivityHubBroadcaster(IHubContext<DashboardHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BroadcastAsync(ActivityLog row, CancellationToken ct = default)
    {
        return _hubContext.Clients.Group(groupName: ModeratorsGroup).SendAsync(method: EventName, arg1: row, cancellationToken: ct);
    }
}
