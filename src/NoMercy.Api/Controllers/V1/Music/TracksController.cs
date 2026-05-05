using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Music;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Music;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.NoMercy.Client;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[Tags(tags: "Music Tracks")]
[Authorize]
[Route("api/v{version:apiVersion}/music/tracks")]
public class TracksController : BaseController
{
    private readonly MusicRepository _musicRepository;
    private readonly MediaContext _mediaContext;
    private readonly IEventBus _eventBus;

    public TracksController(
        MusicRepository musicService,
        MediaContext mediaContext,
        IEventBus eventBus
    )
    {
        _musicRepository = musicService;
        _mediaContext = mediaContext;
        _eventBus = eventBus;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view tracks");

        string language = Language();

        List<TrackUser> rawTracks = await _musicRepository.GetTracks(userId);
        List<ArtistTrackDto> tracks = rawTracks
            .Select(track => new ArtistTrackDto(track.Track, language))
            .ToList();

        if (tracks.Count == 0)
            return NotFoundResponse("Tracks not found");

        return Ok(
            new TracksResponseDto
            {
                Data = new()
                {
                    Name = "Favorite Tracks".Localize(),
                    Link = new("music/tracks", UriKind.Relative),
                    Type = "track",
                    ColorPalette = new(),
                    Tracks = tracks,
                },
            }
        );
    }

    [HttpPost]
    [Route("{id:guid}/like")]
    public async Task<IActionResult> Value(Guid id, [FromBody] LikeRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to like tracks");

        Track? track = await _musicRepository.GetTrackWithIncludesAsync(id);

        if (track is null)
            return NotFoundResponse("Track not found");

        await _musicRepository.LikeTrackAsync(userId, track, request.Value);

        await _eventBus.PublishAsync(
            new LibraryRefreshEvent
            {
                QueryKey = ["music", "album", track.AlbumTrack.FirstOrDefault()?.Album.Id],
            }
        );
        await _eventBus.PublishAsync(
            new LibraryRefreshEvent
            {
                QueryKey = ["music", "artist", track.ArtistTrack.FirstOrDefault()?.Artist.Id],
            }
        );
        await _eventBus.PublishAsync(new LibraryRefreshEvent { QueryKey = ["music", "tracks"] });

        await _eventBus.PublishAsync(
            new MusicItemLikedEvent
            {
                UserId = User.UserId(),
                ItemId = track.Id,
                ItemType = "track",
                Liked = request.Value,
            }
        );

        return Ok(
            new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "{0} {1}",
                Args = new object[] { track.Name, request.Value ? "liked" : "unliked" },
            }
        );
    }

    [HttpGet]
    [Route("{id:guid}/lyrics")]
    public async Task<IActionResult> Lyrics(Guid id)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view lyrics");

        Track? track = await _musicRepository.GetTrackWithIncludesAsync(id);

        if (track is null)
            return NotFoundResponse("Track not found");

        if (track.Lyrics is not null)
            return Ok(
                new LyricsResponseDto
                {
                    Data = ApplyLyricsOffset(track.Lyrics, track.LyricsOffset),
                    Offset = track.LyricsOffset,
                }
            );

        try
        {
            dynamic? subtitles = await NoMercyLyricsClient.SearchLyrics(track);
            if (subtitles is null)
                return NotFoundResponse("Subtitle not found");
            Lyric[]? saved = await _musicRepository.UpdateTrackLyricsAsync(
                track,
                JsonConvert.SerializeObject(subtitles)
            );
            return Ok(
                new LyricsResponseDto
                {
                    Data = ApplyLyricsOffset(saved ?? [], track.LyricsOffset),
                    Offset = track.LyricsOffset,
                }
            );
        }
        catch (Exception e)
        {
            return NotFoundResponse(e.Message);
        }
    }

    [HttpPatch]
    [Route("{id:guid}/lyrics-offset")]
    public async Task<IActionResult> LyricsOffset(Guid id, [FromBody] PatchLyricsOffsetDto request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to update lyrics offset");

        if (request.Offset is not null && (request.Offset < -30000 || request.Offset > 30000))
            return ValidationProblem("Offset must be between -30000 and 30000 ms");

        Track? track = await _musicRepository.GetTrackWithIncludesAsync(id);

        if (track is null)
            return NotFoundResponse("Track not found");

        await _musicRepository.UpdateTrackLyricsOffsetAsync(track, request.Offset);

        return Ok(
            new LyricsOffsetResponseDto
            {
                Status = "ok",
                Message = request.Offset is null
                    ? "Lyrics offset cleared"
                    : $"Lyrics offset set to {request.Offset} ms",
                Offset = request.Offset,
            }
        );
    }

    private static Lyric[] ApplyLyricsOffset(Lyric[] lyrics, int? offsetMs)
    {
        if (offsetMs is null or 0)
            return lyrics;
        double offsetSec = offsetMs.Value / 1000.0;
        foreach (Lyric line in lyrics)
        {
            double newTotal = Math.Max(0, line.Time.Total + offsetSec);
            int totalHundredths = (int)Math.Round(newTotal * 100);
            line.Time.Total = newTotal;
            line.Time.Minutes = totalHundredths / 6000;
            line.Time.Seconds = totalHundredths / 100 % 60;
            line.Time.Hundredths = totalHundredths % 100;
        }
        return lyrics;
    }

    [HttpPost]
    [Route("{id:guid}/playback")]
    public async Task<IActionResult> Playback(Guid id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to record playback");

        Track? track = await _musicRepository.GetTrackAsync(id);

        if (track is null)
            return NotFoundResponse("Track not found");

        await _musicRepository.RecordPlaybackAsync(id, userId);

        return Ok(new StatusResponseDto<string> { Status = "ok", Message = "Playback recorded" });
    }
}

public record LyricsResponseDto
{
    [JsonProperty("data")]
    public Lyric[] Data { get; set; } = [];

    [JsonProperty("offset")]
    public int? Offset { get; set; }
}

public record PatchLyricsOffsetDto
{
    [JsonProperty("offset")]
    public int? Offset { get; set; }
}

public record LyricsOffsetResponseDto
{
    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("offset")]
    public int? Offset { get; set; }
}
