using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Networking;
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
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view tracks");

        List<ArtistTrackDto> tracks = [];

        string language = Language();

        await using MediaContext mediaContext = new();
        await foreach (TrackUser track in TracksResponseDto.GetTracks(mediaContext, userId))
            tracks.Add(new(track.Track, language));

        if (tracks.Count == 0)
            return NotFoundResponse("Tracks not found");

        return Ok(new TracksResponseDto
        {
            Data = new()
            {
                ColorPalette = new(),
                Tracks = tracks
            }
        });
    }

    [HttpPost]
    [Route("{id:guid}/like")]
    public async Task<IActionResult> Value(Guid id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to like tracks");

        await using MediaContext mediaContext = new();
        Track? track = await mediaContext.Tracks
            .AsNoTracking()
            .Where(track => track.Id == id)
            .Include(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .Include(track => track.TrackUser
                .Where(trackUser => trackUser.UserId.Equals(userId)))
            .FirstOrDefaultAsync();

        if (track is null)
            return NotFoundResponse("Track not found");

        bool liked = false;

        if (track.TrackUser.Count == 0)
        {
            await mediaContext.TrackUser
                .Upsert(new(track.Id, userId))
                .On(m => new { m.TrackId, m.UserId })
                .WhenMatched(m => new()
                {
                    TrackId = m.TrackId,
                    UserId = m.UserId
                })
                .RunAsync();
            liked = true;
        }
        else
        {
            TrackUser? tvUser = await mediaContext.TrackUser
                .Where(tvUser => tvUser.TrackId == track.Id && tvUser.UserId.Equals(userId))
                .FirstOrDefaultAsync();

            if (tvUser is not null) mediaContext.TrackUser.Remove(tvUser);

            await mediaContext.SaveChangesAsync();
        }

        Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto()
        {
            QueryKey = ["music", "albums", track.AlbumTrack.FirstOrDefault()?.Album.Id]
        });
        Networking.Networking.SendToAll("RefreshLibrary", "socket", new RefreshLibraryDto()
        {
            QueryKey = ["music", "artists", track.ArtistTrack.FirstOrDefault()?.Artist.Id]
        });

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "{0} {1}",
            Args = new object[]
            {
                track.Name,
                liked ? "liked" : "unliked"
            }
        });
    }

    [HttpGet]
    [Route("{id:guid}/lyrics")]
    [Obsolete("Obsolete")]
    public async Task<IActionResult> Lyrics(Guid id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view lyrics");

        MediaContext mediaContext = new();
        Track? track = await mediaContext.Tracks
            .Where(track => track.Id == id)
            .Include(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .FirstOrDefaultAsync();

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
                Artist = track.ArtistTrack.FirstOrDefault()?.Artist.Name ?? "",
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
                    // Album = track.AlbumTrack.FirstOrDefault()?.Album.Name ?? "",
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

            track._lyrics = JsonConvert.SerializeObject(subtitles);
            track.UpdatedAt = DateTime.UtcNow;
            await mediaContext.SaveChangesAsync();

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

        await using MediaContext mediaContext = new();
        Track? track = await mediaContext.Tracks
            .AsNoTracking()
            .Where(track => track.Id == id)
            .FirstOrDefaultAsync();

        if (track is null)
            return NotFoundResponse("Track not found");

        await mediaContext.MusicPlays
            .AddAsync(new(userId, track.Id));

        await mediaContext.SaveChangesAsync();

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Playback recorded"
        });
    }
}
