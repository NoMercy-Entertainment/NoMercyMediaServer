using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.Networking;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Sources;
using Serilog.Events;

namespace NoMercy.Api.Hubs;

public class RipperHub : ConnectionHub
{
    private static readonly ConcurrentDictionary<string, Guid> CurrentDevices = new();

    private readonly IDriveMonitor _driveMonitor;
    private readonly DiscSourceFactory _discSourceFactory;

    public RipperHub(
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<MediaContext> contextFactory,
        ConnectedClients connectedClients,
        IActivityLogger activityLogger,
        IDriveMonitor driveMonitor,
        DiscSourceFactory discSourceFactory
    )
        : base(httpContextAccessor, contextFactory, connectedClients, activityLogger)
    {
        _driveMonitor = driveMonitor;
        _discSourceFactory = discSourceFactory;
    }

    public override async Task OnConnectedAsync()
    {
        User user = Context.User.User()!;

        CurrentDevices.TryAdd(Context.ConnectionId, user.Id);

        await base.OnConnectedAsync();
        Logger.Socket("Ripper client connected", LogEventLevel.Debug);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        Logger.Socket("Ripper client disconnected", LogEventLevel.Debug);
    }

    /// <summary>
    /// Returns the current state of the drive at <paramref name="drivePath"/>.
    /// When a disc is inserted and a reader is registered for the disc type,
    /// a lightweight probe is run and the result is returned. For drives with
    /// no disc, or disc types without a registered source, only drive-level
    /// info is returned.
    /// </summary>
    public async Task<object?> GetDriveState(string drivePath)
    {
        if (!Context.User.IsModerator())
            return null;

        DiscDrive? drive = _driveMonitor
            .GetDrives()
            .FirstOrDefault(d =>
                d.Path.TrimEnd(Path.DirectorySeparatorChar)
                    .Equals(
                        drivePath.TrimEnd(Path.DirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase
                    )
            );

        if (drive is null)
            return null;

        if (!drive.HasDisc)
        {
            return new
            {
                path = drive.Path.TrimEnd(Path.DirectorySeparatorChar),
                label = drive.Label.OrEmpty(),
                open = true,
                has_disc = false,
                disc_type = drive.DiscType.ToString().ToLowerInvariant(),
            };
        }

        IDiscSource? source = _discSourceFactory.CreateFor(drive.DiscType);
        if (source is null)
        {
            return new
            {
                path = drive.Path.TrimEnd(Path.DirectorySeparatorChar),
                label = drive.Label.OrEmpty(),
                open = false,
                has_disc = true,
                disc_type = drive.DiscType.ToString().ToLowerInvariant(),
            };
        }

        try
        {
            OpticalMedia.Sources.DiscInfo info = await source.ProbeAsync(
                drive,
                CancellationToken.None
            );
            return new
            {
                path = drive.Path.TrimEnd(Path.DirectorySeparatorChar),
                label = (info.DiscTitle ?? info.DiscLabel ?? drive.Label).OrEmpty(),
                open = false,
                has_disc = true,
                disc_type = drive.DiscType.ToString().ToLowerInvariant(),
                disc = info,
            };
        }
        catch
        {
            return new
            {
                path = drive.Path.TrimEnd(Path.DirectorySeparatorChar),
                label = drive.Label.OrEmpty(),
                open = false,
                has_disc = true,
                disc_type = drive.DiscType.ToString().ToLowerInvariant(),
            };
        }
    }
}
