using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
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
using NoMercy.MediaSources.OpticalMedia;
using NoMercy.MediaSources.OpticalMedia.Dto;
using NoMercy.Networking;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.Controllers.Socket;

public class RipperHub : ConnectionHub
{
    private static readonly ConcurrentDictionary<string, Guid> CurrentDevices = new();

    public RipperHub(IHttpContextAccessor httpContextAccessor, IDbContextFactory<MediaContext> contextFactory)
        : base(httpContextAccessor, contextFactory)
    {
    }

    public override async Task OnConnectedAsync()
    {
        User user = Context.User.User()!;

        CurrentDevices.TryAdd(Context.ConnectionId, user.Id);

        await base.OnConnectedAsync();
        Logger.Socket("Ripper client connected");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        Logger.Socket("Ripper client disconnected");
    }

    public async Task<DriveState?> GetDriveState(string drivePath)
    {
        if (!Context.User.IsModerator())
            return null;

        MetaData? metadata = await DriveMonitor.GetDriveMetadata(drivePath);

        return new()
        {
            Open = false,
            Path = drivePath.TrimEnd(Path.DirectorySeparatorChar),
            Label = metadata?.Title ?? "",
            MetaData = metadata
        };
    }
}