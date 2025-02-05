using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Networking;
using NoMercy.NmSystem;

namespace NoMercy.Api.Controllers.Socket;
public class VideoHub(IHttpContextAccessor httpContextAccessor) : ConnectionHub(httpContextAccessor)
{
    private static readonly ConcurrentDictionary<Guid, string> CurrentDevices = new();
    private static readonly ConcurrentDictionary<Guid, PlayerState> PlayerState = new();

    public async Task SetTime(VideoProgressRequest request)
    {
        Guid userId = Guid.Parse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);

        User? user = ClaimsPrincipleExtensions.Users.FirstOrDefault(x => x.Id.Equals(userId));

        if (user is null) return;

        UserData userdata = new()
        {
            UserId = user.Id,
            Type = request.VideoType,
            Time = request.Time,
            Audio = request.Audio,
            Subtitle = request.Subtitle,
            SubtitleType = request.SubtitleType,
            VideoFileId = Ulid.Parse(request.VideoId),
            MovieId = request.VideoType == "movie" ? request.TmdbId : null,
            TvId = request.VideoType == "tv" ? request.TmdbId : null,
            CollectionId = request.CollectionId,
            SpecialId = request.SpecialId
        };

        await using MediaContext mediaContext = new();
        await mediaContext.UserData.Upsert(userdata)
            .On(x => new { x.UserId, x.VideoFileId })
            .WhenMatched((uds, udi) => new()
            {
                Id = uds.Id,
                Type = udi.Type,
                MovieId = udi.MovieId,
                TvId = udi.TvId,
                CollectionId = udi.CollectionId,
                SpecialId = udi.SpecialId,
                Time = udi.Time,
                Audio = udi.Audio,
                Subtitle = udi.Subtitle,
                SubtitleType = udi.SubtitleType,
                LastPlayedDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                UpdatedAt = udi.UpdatedAt
            })
            .RunAsync();
    }

    public async Task RemoveWatched(VideoProgressRequest request)
    {
        Guid userId = Guid.Parse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);

        User? user = ClaimsPrincipleExtensions.Users.FirstOrDefault(x => x.Id.Equals(userId));

        if (user is null) return;

        await using MediaContext mediaContext = new();
        UserData[] userdata = await mediaContext.UserData
            .Where(x => x.UserId == user.Id)
            .Where(x => x.Type == request.PlaylistType)
            .Where(x => x.MovieId == request.TmdbId
                        || x.TvId == request.TmdbId
                        || x.SpecialId == request.SpecialId
                        || x.CollectionId == request.TmdbId)
            .ToArrayAsync();
        
        Logger.Socket(request);
        Logger.Socket(userdata);

        mediaContext.UserData.RemoveRange(userdata);

        await mediaContext.SaveChangesAsync();
    }

    public Task CreateSession(VideoProgressRequest request)
    {
        return Task.CompletedTask;
    }

    public Task Disconnect(VideoProgressRequest request)
    {
        return Task.CompletedTask;
    }

    public Task Connect(VideoProgressRequest request)
    {
        return Task.CompletedTask;
    }

    public Task Party(VideoProgressRequest request)
    {
        return Task.CompletedTask;
    }

    public Task JoinSession(VideoProgressRequest request)
    {
        return Task.CompletedTask;
    }

    public Task LeaveSession(VideoProgressRequest request)
    {
        return Task.CompletedTask;
    }

    public Task PartyTime(VideoProgressRequest request)
    {
        return Task.CompletedTask;
    }

    public Task PartyPlay(VideoProgressRequest request)
    {
        return Task.CompletedTask;
    }

    public Task PartyPause(VideoProgressRequest request)
    {
        return Task.CompletedTask;
    }

    public Task PartyItem(VideoProgressRequest request)
    {
        return Task.CompletedTask;
    }

    public void Log(dynamic? request)
    {
        Logger.Socket(request);
    }

    public class VideoProgressRequest
    {
        [JsonProperty("app")] public int AppId { get; set; }
        [JsonProperty("video_id")] public string VideoId { get; set; } = string.Empty;
        [JsonProperty("tmdb_id")] public int TmdbId { get; set; }
        [JsonProperty("playlist_type")] public string PlaylistType { get; set; } = string.Empty;
        [JsonProperty("video_type")] public string VideoType { get; set; } = string.Empty;
        [JsonProperty("time")] public int Time { get; set; }
        [JsonProperty("audio")] public string Audio { get; set; } = string.Empty;
        [JsonProperty("subtitle")] public string Subtitle { get; set; } = string.Empty;
        [JsonProperty("subtitle_type")] public string SubtitleType { get; set; } = string.Empty;
        [JsonProperty("special_id")] public Ulid? SpecialId { get; set; }
        [JsonProperty("collection_id")] public int? CollectionId { get; set; }
    }

    private PlayerState MusicPlayerState()
    {
        User? user = Context.User.User();

        if (user is null)
        {
            Logger.Socket("Creating new player state");
            return new();
        }

        PlayerState? playerState = PlayerState.FirstOrDefault(p => p.Key == user.Id).Value;

        if (playerState != null)
        {
            if (playerState.CurrentItem is not null) playerState.CurrentItem.Lyrics = null;
            return playerState;
        }

        Logger.Socket("Creating new player state");
        playerState = new();

        PlayerState.TryAdd(user.Id, playerState);

        return playerState;
    }

    public List<Device> ConnectedDevices()
    {
        User? user = Context.User.User();

        return Networking.Networking.SocketClients.Values
            .Where(x => x.Sub.Equals(user?.Id))
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
                CustomName = c.CustomName
            })
            .ToList();
    }

    public class CurrentDeviceRequest
    {
        [JsonProperty("deviceId")] public string DeviceId { get; set; } = string.Empty;
    }

    public void SetCurrentDevice(CurrentDeviceRequest request)
    {
        User? user = Context.User.User();

        PlayerState state = MusicPlayerState();
        state.CurrentDevice = request.DeviceId;

        CurrentDevices.TryAdd(user!.Id, request.DeviceId);

        Clients.All.SendAsync("CurrentDevice", request.DeviceId);

        if (state.Muted) Clients.All.SendAsync("Mute", state.Muted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);

        List<Device> connectedDevices = ConnectedDevices();
        PlayerState state = MusicPlayerState();

        // if (state.CurrentItem is null)
        // {
        //     state.Time = 0;
        //     state.Volume = 20;
        // }

        if (connectedDevices.Any(c => c.DeviceId == state.CurrentDevice) == false) await Clients.All.SendAsync("Pause");
    }

    public void CurrentDevice()
    {
        PlayerState state = MusicPlayerState();

        Clients.Caller.SendAsync("CurrentDevice", state.CurrentDevice);

        if (state.Muted) Clients.All.SendAsync("Mute", state.Muted);

        switch (state.PlayState)
        {
            case "play":
                Clients.Others.SendAsync("Play");
                break;
            case "pause":
                Clients.Others.SendAsync("Pause");
                break;
            case "stop":
                Clients.Others.SendAsync("Stop");
                break;
        }

        Clients.All.SendAsync("Volume", state.Volume);
    }

    public async Task Play()
    {
        PlayerState state = MusicPlayerState();
        state.PlayState = "play";

        await Clients.Others.SendAsync("Play");
    }

    public async Task Stop()
    {
        PlayerState state = MusicPlayerState();
        state.PlayState = "stop";
        state.CurrentItem = null;
        state.Queue = [];
        state.Backlog = [];

        Logger.Socket(state);

        await Clients.All.SendAsync("Stop");
        await Clients.All.SendAsync("State", state);
        await Clients.All.SendAsync("Queue", state.Queue);
        await Clients.All.SendAsync("Backlog", state.Backlog);
    }

    public async Task Pause()
    {
        PlayerState state = MusicPlayerState();
        state.PlayState = "pause";

        // Logger.Socket(state.State);

        await Clients.Others.SendAsync("Pause");
    }

    public async Task Next()
    {
        await Clients.Others.SendAsync("Next");
    }

    public async Task Previous()
    {
        await Clients.Others.SendAsync("Previous");
    }

    public async Task Shuffle(bool shuffle)
    {
        PlayerState state = MusicPlayerState();
        state.Shuffle = shuffle;

        await Clients.Others.SendAsync("Shuffle");
    }

    public async Task Repeat(string repeat)
    {
        PlayerState state = MusicPlayerState();
        state.Repeat = repeat;

        // Logger.Socket(state.Repeat.ToString());

        await Clients.Others.SendAsync("Repeat");
    }

    public async Task Mute(bool mute)
    {
        PlayerState state = MusicPlayerState();
        state.Muted = mute;

        // Logger.Socket(state.Muted.ToString());

        await Clients.Others.SendAsync("Mute", mute);
    }

    public async Task Volume(int volume)
    {
        PlayerState state = MusicPlayerState();
        state.Volume = volume;

        await Clients.Others.SendAsync("Volume", volume);
    }

    public async Task SeekTo(int value)
    {
        PlayerState state = MusicPlayerState();
        state.Time = value;

        // Logger.Socket(state.Percentage.ToString());

        await Clients.Others.SendAsync("SeekTo", value);
    }

    public async Task CurrentTime(int time)
    {
        PlayerState state = MusicPlayerState();
        state.Time = time;

        // Logger.Socket(state.Time.ToString(CultureInfo.InvariantCulture));

        await Clients.Others.SendAsync("CurrentTime", time);
    }

    public async Task CurrentItem(Song? currentItem)
    {
        PlayerState state = MusicPlayerState();
        state.CurrentItem = currentItem;

        await Clients.Others.SendAsync("CurrentItem", currentItem);
        await Clients.All.SendAsync("CurrentDevice", state.CurrentDevice);
    }

    public async Task Queue(Dictionary<string, Song> queue)
    {
        PlayerState state = MusicPlayerState();
        state.Queue = queue.Values;

        await Clients.Others.SendAsync("Queue", queue.Values);
    }

    public async Task Backlog(Dictionary<string, Song> backlog)
    {
        PlayerState state = MusicPlayerState();
        state.Backlog = backlog.Values;

        await Clients.Others.SendAsync("Backlog", backlog.Values);
    }

    public PlayerState State()
    {
        PlayerState state = MusicPlayerState();
        return state;
    }
}
