using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.Networking;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.Controllers.Socket;
public class VideoHub(IHttpContextAccessor httpContextAccessor) : ConnectionHub(httpContextAccessor)
{
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

}
