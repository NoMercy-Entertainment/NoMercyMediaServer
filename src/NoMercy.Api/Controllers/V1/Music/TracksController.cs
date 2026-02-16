using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Music;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.Helpers.Extensions;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Music;
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

    public TracksController(MusicRepository musicService, MediaContext mediaContext, IEventBus eventBus)
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

        List<ArtistTrackDto> tracks = [];

        string language = Language();

        foreach (TrackUser track in _musicRepository.GetTracks(userId))
            tracks.Add(new(track.Track, language));

        if (tracks.Count == 0)
            return NotFoundResponse("Tracks not found");

        return Ok(new TracksResponseDto
        {
            Data = new()
            {
                Name = "Favorite Tracks".Localize(),
                Link = new("music/tracks", UriKind.Relative),
                Type = "track",
                ColorPalette = new(),
                Tracks = tracks
            }
        });
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

        await _eventBus.PublishAsync(new LibraryRefreshEvent
        {
            QueryKey = ["music", "album", track.AlbumTrack.FirstOrDefault()?.Album.Id]
        });
        await _eventBus.PublishAsync(new LibraryRefreshEvent
        {
            QueryKey = ["music", "artist", track.ArtistTrack.FirstOrDefault()?.Artist.Id]
        });
        await _eventBus.PublishAsync(new LibraryRefreshEvent
        {
            QueryKey = ["music", "tracks"]
        });
        
        await _eventBus.PublishAsync(new MusicItemLikedEvent
        {
            UserId = User.UserId(),
            ItemId = track.Id,
            ItemType = "track",
            Liked = request.Value
        });

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "{0} {1}",
            Args = new object[]
            {
                track.Name,
                request.Value ? "liked" : "unliked"
            }
        });
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
            return Ok(new DataResponseDto<Lyric[]>
            {
                Data = track.Lyrics
            });

        try
        {
            dynamic? subtitles = await NoMercyLyricsClient.SearchLyrics(track);
            if (subtitles is null) return NotFoundResponse("Subtitle not found");
            subtitles = await _musicRepository.UpdateTrackLyricsAsync(track, JsonConvert.SerializeObject(subtitles));
            return Ok(new DataResponseDto<dynamic>
            {
                Data = subtitles
            });
        }
        catch (Exception e)
        {
            return NotFoundResponse(e.Message);
        }
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

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Playback recorded"
        });
    }
}