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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using NoMercy.Database;
using NoMercy.Database.Activity;
using NoMercy.Database.Models.Users;
using NoMercy.Authorization;
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Networking;

[Authorize]
public class ConnectionHub : Hub
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDbContextFactory<MediaContext> _contextFactory;
    protected IDbContextFactory<MediaContext> ContextFactory => _contextFactory;
    protected readonly ConnectedClients ConnectedClients;
    protected readonly IActivityLogger ActivityLogger;
    private string Endpoint { get; set; }

    protected IUserCache UserCacheService =>
        _httpContextAccessor.HttpContext?.RequestServices?.GetService<IUserCache>()
        ?? UserCache.Current;

    protected IMediaAuthorizationPolicy AuthPolicy =>
        _httpContextAccessor.HttpContext?.RequestServices?.GetService<IMediaAuthorizationPolicy>()
        ?? new MediaAuthorizationPolicy(UserCache.Current);

    protected ConnectionHub(
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<MediaContext> contextFactory,
        ConnectedClients connectedClients,
        IActivityLogger activityLogger
    )
    {
        _httpContextAccessor = httpContextAccessor;
        _contextFactory = contextFactory;
        ConnectedClients = connectedClients;
        ActivityLogger = activityLogger;
        Endpoint = _httpContextAccessor.HttpContext?.Request.Path.Value ?? "Unknown";
        // Logger.Socket($"Connected to {Endpoint}");
    }

    public string GetCountryFromContext()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers["country"].FirstOrDefault()
            ?? "US";
    }

    public string GetLanguageFromContext()
    {
        return _httpContextAccessor
                .HttpContext?.Request.Headers.AcceptLanguage.FirstOrDefault()
                ?.Split("_")
                .FirstOrDefault()
            ?? LocalizationHelper.GlobalLocalizer.TargetLanguage;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        User? user = UserCacheService.GetUser(Context.User.UserId());
        if (user is null)
            return;

        Client client = new()
        {
            Sub = user.Id,
            Ip =
                _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString()
                ?? "Unknown",
            Socket = Clients.Caller,
            Endpoint = Endpoint,
            IsActive = true,
        };

        IQueryCollection? query = _httpContextAccessor.HttpContext?.Request.Query;
        if (query is not null && query.Count > 1)
        {
            if (query.TryGetValue("client_id", out StringValues value))
                client.DeviceId = value.ToString();

            if (query.TryGetValue("custom_name", out StringValues customName))
                client.CustomName = customName.ToString();

            if (
                query.TryGetValue("client_volume", out StringValues volumePercent)
                && int.TryParse(volumePercent.ToString(), out int parsedVolume)
            )
            {
                client.VolumePercent = Math.Clamp(parsedVolume, 0, 100);
            }

            if (query.TryGetValue("client_name", out StringValues name))
                client.Name = name.ToString();

            if (query.TryGetValue("client_type", out StringValues type))
                client.Type = type.ToString();

            if (query.TryGetValue("client_version", out StringValues version))
                client.Version = version.ToString();

            if (query.TryGetValue("client_os", out StringValues os))
                client.Os = os.ToString();

            if (query.TryGetValue("client_browser", out StringValues browser))
                client.Browser = browser.ToString();

            if (query.TryGetValue("client_device", out StringValues model))
                client.Model = model.ToString();
        }

        await using MediaContext mediaContext = await _contextFactory.CreateDbContextAsync();
        await mediaContext
            .Devices.Upsert(client)
            .On(x => x.DeviceId)
            .WhenMatched(
                (ds, di) =>
                    new()
                    {
                        Browser = di.Browser,
                        DeviceId = di.DeviceId,
                        Ip = di.Ip,
                        Model = di.Model,
                        Name = di.Name,
                        Os = di.Os,
                        Type = di.Type,
                        Version = di.Version,
                        VolumePercent = di.VolumePercent,
                    }
            )
            .RunAsync();

        // Update CustomName separately — FlexLabs upsert doesn't support conditional expressions.
        // Only overwrite when the client sends a non-empty custom_name, preserving existing names otherwise.
        if (!string.IsNullOrEmpty(client.CustomName))
        {
            await mediaContext
                .Devices.Where(x => x.DeviceId == client.DeviceId)
                .ExecuteUpdateAsync(x => x.SetProperty(d => d.CustomName, client.CustomName));
        }

        Device? device = await mediaContext.Devices.FirstOrDefaultAsync(x =>
            x.DeviceId == client.DeviceId
        );

        // Align the in-memory client's PK with the persisted Devices.Id. The
        // Client base class assigns a fresh Ulid at construction; the upsert
        // matches on the DeviceId fingerprint and preserves the existing PK,
        // so client.Id and device.Id diverge. Subsequent ActivityLog writes
        // use device.Id from the in-memory map and FK-fail because the random
        // Ulid never made it into the Devices table.
        if (device is not null)
            client.Id = device.Id;

        client.CustomName = device?.CustomName;
        client.VolumePercent = device?.VolumePercent;
        client.IsActive = true;

        if (device is not null)
        {
            await mediaContext
                .Devices.Where(x => x.DeviceId == device.DeviceId)
                .ExecuteUpdateAsync(x => x.SetProperty(d => d.IsActive, true));
            await mediaContext.SaveChangesAsync();

            await ActivityLogger.LogConnectionAsync("connection.connected", user.Id, device.Id);
        }

        ConnectedClients.Clients.TryAdd(Context.ConnectionId, client);

        await Clients.All.SendAsync("ConnectedDevicesState", Devices());
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);

        if (ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? client))
        {
            await using MediaContext mediaContext = await _contextFactory.CreateDbContextAsync();
            Device? device = await mediaContext.Devices.FirstOrDefaultAsync(x =>
                x.DeviceId == client.DeviceId
            );
            if (device is not null)
            {
                await mediaContext
                    .Devices.Where(x => x.DeviceId == device.DeviceId)
                    .ExecuteUpdateAsync(x => x.SetProperty(d => d.IsActive, false));
                await mediaContext.SaveChangesAsync();

                await ActivityLogger.LogConnectionAsync(
                    "connection.disconnected",
                    client.Sub,
                    device.Id
                );
            }

            ConnectedClients.Clients.Remove(Context.ConnectionId, out _);

            await Clients.All.SendAsync("ConnectedDevicesState", Devices());
        }
    }

    public List<Device> Devices()
    {
        User? user = UserCacheService.GetUser(Context.User.UserId());
        if (user is null)
            return [];

        return ConnectedClients
            .Clients.Values.Where(x => x.Sub.Equals(user.Id))
            .Where(x => x.Endpoint == Endpoint)
            .Select(c => new Device
            {
                Name = c.Name,
                Ip = c.Ip,
                DeviceId = c.DeviceId,
                Browser = c.Browser,
                Os = c.Os,
                Model = c.Model,
                Type = c.Type,
                Version = c.Version,
                Id = c.Id,
                CustomName = c.CustomName,
                VolumePercent = c.VolumePercent,
            })
            .ToList();
    }
}
