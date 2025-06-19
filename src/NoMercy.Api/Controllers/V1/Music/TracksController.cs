using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.Socket.music;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Api.Services;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.Networking.Dto;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.MusixMatch.Client;
using NoMercy.Providers.MusixMatch.Models;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[Tags(tags: "Music Tracks")]
[Authorize]
[Route("api/v{version:apiVersion}/music/tracks")]
public class TracksController : BaseController
{
    public static event EventHandler<LikeEventDto>? OnLikeEvent;
    private readonly MusicRepository _musicRepository;
    private readonly MediaContext _mediaContext;

    public TracksController(MusicRepository musicService, MediaContext mediaContext)
    {
        _musicRepository = musicService;
        _mediaContext = mediaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view tracks");

        List<ArtistTrackDto> tracks = [];

        string language = Language();

        foreach (TrackUser track in await _musicRepository.GetTracks(_mediaContext, userId))
            tracks.Add(new(track.Track, language));

        if (tracks.Count == 0)
            return NotFoundResponse("Tracks not found");

        return Ok(new TracksResponseDto
        {
            Data = new()
            {
                Name = "Favorite Tracks".Localize(),
                Link = new("music/tracks", UriKind.Relative),
                Type = "playlist",
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

        Track? track = await _musicRepository.GetTrackWithIncludes(_mediaContext, id);

        if (track is null)
            return NotFoundResponse("Track not found");

        await _musicRepository.LikeTrackAsync(userId, track, request.Value);

        Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto
        {
            QueryKey = ["music", "album", track.AlbumTrack.FirstOrDefault()?.Album.Id]
        });
        Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto
        {
            QueryKey = ["music", "artist", track.ArtistTrack.FirstOrDefault()?.Artist.Id]
        });


        LikeEventDto likeEventDto = new()
        {
            Id = track.Id,
            Type = "track",
            Liked = request.Value,
            User = User.User()
        };

        OnLikeEvent?.Invoke(this, likeEventDto);

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
    [Obsolete("Obsolete")]
    public async Task<IActionResult> Lyrics(Guid id)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view lyrics");

        Track? track = await _musicRepository.GetTrackWithIncludes(_mediaContext, id);

        if (track is null)
            return NotFoundResponse("Track not found");

        if (track.Lyrics is not null)
            return Ok(new DataResponseDto<Lyric[]>
            {
                Data = track.Lyrics
            });

        try
        {
            MusixmatchClient client = new();
            MusixMatchTrackSearchParameters parameters = new()
            {
                Album = track.AlbumTrack.FirstOrDefault()?.Album.Name ?? "",
                // Artist = track.ArtistTrack.FirstOrDefault()?.Artist.Name ?? "",
                Artists = track.ArtistTrack.Select(artistTrack => artistTrack.Artist.Name).ToArray(),
                Title = track.Name,
                Duration = track.Duration?.ToSeconds().ToString(),
                Sort = MusixMatchTrackSearchParameters.MusixMatchSortStrategy.TrackRatingDesc
            };

            MusixMatchSubtitleGet? lyrics = await client.SongSearch(parameters);

            dynamic? subtitles = lyrics?.Message?.Body?.MacroCalls?
                .TrackSubtitlesGet?.Message?.Body?.SubtitleList.FirstOrDefault()?.Subtitle?.SubtitleBody;

            if (subtitles is null)
            {
                subtitles = lyrics?.Message?.Body?.MacroCalls?.TrackLyricsGet?.Message?.Body?.Lyrics?.LyricsBody;
                if (subtitles is not null)
                    subtitles = Regex.Replace(input: subtitles, pattern: "^\"|\"$", replacement: "");
            }

            if (subtitles is null)
            {
                parameters = new()
                {
                    // Albums = track.AlbumTrack.FirstOrDefault()?.Albums.Name ?? "",
                    Artist = track.ArtistTrack.FirstOrDefault()?.Artist.Name ?? "",
                    // Artists = track.ArtistTrack.Select(artistTrack => artistTrack.Artist.Name).ToArray(),
                    Title = track.Name,
                    Duration = track.Duration?.ToSeconds().ToString(),
                    Sort = MusixMatchTrackSearchParameters.MusixMatchSortStrategy.TrackRatingDesc
                };

                lyrics = await client.SongSearch(parameters);

                subtitles = lyrics?.Message?.Body?.MacroCalls?
                    .TrackSubtitlesGet?.Message?.Body?.SubtitleList.FirstOrDefault()?.Subtitle?.SubtitleBody;

                if (subtitles is null)
                {
                    subtitles = lyrics?.Message?.Body?.MacroCalls?.TrackLyricsGet?.Message?.Body?.Lyrics?.LyricsBody;
                    if (subtitles is not null)
                        subtitles = Regex.Replace(input: subtitles, pattern: "^\"|\"$", replacement: "");
                }
            }

            if (subtitles is null)
                return NotFoundResponse("Subtitle not found");

            await _musicRepository.UpdateTrackLyricsAsync(track, JsonConvert.SerializeObject(subtitles));

            return Ok(new DataResponseDto<Lyric[]>
            {
                Data = track.Lyrics
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

        Track? track = await _musicRepository.GetTrack(_mediaContext, id);

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