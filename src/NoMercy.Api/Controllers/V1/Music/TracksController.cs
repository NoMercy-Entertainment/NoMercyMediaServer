using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.Socket.music;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.Networking.Dto;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.Lrclib.Client;
using NoMercy.Providers.MusixMatch.Client;
using NoMercy.Providers.MusixMatch.Models;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[Tags(tags: "Music Tracks")]
[Authorize]
[Route("api/v{version:apiVersion}/music/tracks")]
public partial class TracksController : BaseController
{
    public static event EventHandler<MusicLikeEventDto>? OnLikeEvent;
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

        foreach (TrackUser track in _musicRepository.GetTracks(_mediaContext, userId))
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

        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["music", "album", track.AlbumTrack.FirstOrDefault()?.Album.Id]
        });
        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["music", "artist", track.ArtistTrack.FirstOrDefault()?.Artist.Id]
        });
        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["music","tracks"]

        });
        
        MusicLikeEventDto musicLikeEventDto = new()
        {
            Id = track.Id,
            Type = "track",
            Liked = request.Value,
            User = User.User()
        };

        OnLikeEvent?.Invoke(this, musicLikeEventDto);

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
            dynamic? subtitles = await SearchLyrics(track);
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

    private static async Task<dynamic?> SearchLyrics(Track track)
    {
        MusixmatchClient musixmatchClient = new();
        LrclibClient lrclibClient = new();
        dynamic? lyric = null;
        int recursiveCount = 0;
        string artistNames = string.Join(",", track.ArtistTrack.Select(artistTrack => artistTrack.Artist.Name));
        string duration = track.Duration.ToSeconds().ToString(CultureInfo.InvariantCulture);
        string albumName = track.AlbumTrack.FirstOrDefault()?.Album.Name ?? string.Empty;
        while (true)
        {
            MusixMatchSubtitleGet? lyrics = null;
            switch (recursiveCount)
            {
                case 0:
                case 4:
                    lyrics = await musixmatchClient.SongSearch(new() { Album = albumName, Artist = artistNames, Title = track.Name, Duration = duration, Sort = MusixMatchTrackSearchParameters.MusixMatchSortStrategy.TrackRatingDesc });
                    if (recursiveCount == 4)
                    {
                        lyric = await lrclibClient.SongSearch(
                            artists: track.ArtistTrack.Select(artistTrack => artistTrack.Artist.Name).ToArray(),
                            trackName: track.Name,
                            albumName: track.AlbumTrack.FirstOrDefault()?.Album.Name,
                            duration: track.Duration?.ToSeconds()
                        );
                        lyric ??= ToFormatLyrics(lyrics);
                        break;
                    }
                    lyric = lyrics?.Message?.Body?.MacroCalls?.TrackSubtitlesGet?.Message?.Body?.SubtitleList
                        .FirstOrDefault()
                        ?.Subtitle?.SubtitleBody;
                    break;
                case 1:
                case 5:
                    lyrics = await musixmatchClient.SongSearch(new() { Artist = artistNames, Title = track.Name, Duration = duration, Sort = MusixMatchTrackSearchParameters.MusixMatchSortStrategy.TrackRatingDesc });
                    if (recursiveCount == 5)
                    {
                        lyric = await lrclibClient.SongSearch(
                            artists: track.ArtistTrack.Select(artistTrack => artistTrack.Artist.Name).ToArray(),
                            trackName: track.Name,
                            duration: track.Duration?.ToSeconds()
                        );
                        lyric ??= ToFormatLyrics(lyrics);
                        break;
                    }
                    lyric = lyrics?.Message?.Body?.MacroCalls?.TrackSubtitlesGet?.Message?.Body?.SubtitleList
                        .FirstOrDefault()
                        ?.Subtitle?.SubtitleBody;
                    break;
                case 2:
                case 6:
                    lyrics = await musixmatchClient.SongSearch(new() { Artist = artistNames, Title = track.Name, Sort = MusixMatchTrackSearchParameters.MusixMatchSortStrategy.TrackRatingDesc });
                    if (recursiveCount == 6)
                    {
                        lyric = await lrclibClient.SongSearch(
                            artists: track.ArtistTrack.Select(artistTrack => artistTrack.Artist.Name).ToArray(),
                            trackName: track.Name
                        );
                        lyric ??= ToFormatLyrics(lyrics);
                        break;
                    }
                    lyric = lyrics?.Message?.Body?.MacroCalls?.TrackSubtitlesGet?.Message?.Body?.SubtitleList
                        .FirstOrDefault()
                        ?.Subtitle?.SubtitleBody;
                    break;
                case 3:
                case 7:
                    lyrics = await musixmatchClient.SongSearch(new() { Title = track.Name, Sort = MusixMatchTrackSearchParameters.MusixMatchSortStrategy.TrackRatingDesc });
                    if (recursiveCount == 7)
                    {
                        lyric = ToFormatLyrics(lyrics);
                        break;
                    }
                    lyric = lyrics?.Message?.Body?.MacroCalls?.TrackSubtitlesGet?.Message?.Body?.SubtitleList
                        .FirstOrDefault()
                        ?.Subtitle?.SubtitleBody;
                    break;
            }
            if (lyric is not null || recursiveCount >= 7) break;
            recursiveCount += 1;
        }
        musixmatchClient.Dispose();
        lrclibClient.Dispose();
        return lyric;
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

    private static dynamic? ToFormatLyrics(MusixMatchSubtitleGet? lyrics)
    {
        string text = FormatLyricsRegex().Replace(input: lyrics?.Message?.Body?.MacroCalls?.TrackLyricsGet?.Message?.Body?.Lyrics?.LyricsBody ?? string.Empty, replacement: "");
        if (string.IsNullOrEmpty(text))
            return null;
        
        return new[]{
            new MusixMatchFormattedLyric
            {
                Text = text,
                Time = new()
                {
                    Total = 0.0,
                    Minutes = 0,
                    Seconds = 0,
                    Hundredths = 0
                }
            }
        };
    }

    [GeneratedRegex("^\"|\"$")]
    private static partial Regex FormatLyricsRegex();
}