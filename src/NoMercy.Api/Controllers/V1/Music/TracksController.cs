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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Music;
using NoMercy.Api.Services.Music;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Music;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Music;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[Tags(tags: "Music Tracks")]
[Authorize(Policy = "MediaAccess")]
[Route(template: "api/v{version:apiVersion}/music/tracks")]
public class TracksController : BaseController
{
    private readonly IMusicRepository _musicRepository;
    private readonly IEventBus _eventBus;
    private readonly LyricsResolver _lyricsResolver;

    public TracksController(
        IMusicRepository musicService,
        IEventBus eventBus,
        LyricsResolver lyricsResolver
    )
    {
        _musicRepository = musicService;
        _eventBus = eventBus;
        _lyricsResolver = lyricsResolver;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        Guid userId = User.UserId();

        string language = Language();

        List<TrackUser> rawTracks = await _musicRepository.GetTracks(userId: userId);
        List<ArtistTrackDto> tracks = rawTracks
            .Select(selector: track => new ArtistTrackDto(track: track.Track, country: language))
            .ToList();

        if (tracks.Count == 0)
            return NotFoundResponse(detail: "Tracks not found");

        return Ok(
            value: new TracksResponseDto
            {
                Data = new()
                {
                    Name = "Favorite Tracks".Localize(),
                    Link = new(uriString: "music/tracks", uriKind: UriKind.Relative),
                    Type = "track",
                    ColorPalette = new(),
                    Tracks = tracks,
                },
            }
        );
    }

    [HttpPost]
    [Route(template: "{id:guid}/like")]
    public async Task<IActionResult> Value(Guid id, [FromBody] LikeRequestDto request)
    {
        Guid userId = User.UserId();

        Track? track = await _musicRepository.GetTrackWithIncludesAsync(id: id);

        if (track is null)
            return NotFoundResponse(detail: "Track not found");

        await _musicRepository.LikeTrackAsync(userId: userId, track: track, liked: request.Value);

        await _eventBus.PublishAsync(
            @event: new LibraryRefreshedEvent
            {
                QueryKey = ["music", "album", track.AlbumTrack.FirstOrDefault()?.Album.Id],
            }
        );
        await _eventBus.PublishAsync(
            @event: new LibraryRefreshedEvent
            {
                QueryKey = ["music", "artist", track.ArtistTrack.FirstOrDefault()?.Artist.Id],
            }
        );
        await _eventBus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["music", "tracks"] });

        await _eventBus.PublishAsync(
            @event: new MusicItemLikedEvent
            {
                UserId = User.UserId(),
                ItemId = track.Id,
                ItemType = "track",
                Liked = request.Value,
            }
        );

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "{0} {1}",
                Args = new object[] { track.Name, request.Value ? "liked" : "unliked" },
            }
        );
    }

    [HttpGet]
    [Route(template: "{id:guid}/lyrics")]
    public async Task<IActionResult> Lyrics(Guid id)
    {

        Track? track = await _musicRepository.GetTrackWithIncludesAsync(id: id);

        if (track is null)
            return NotFoundResponse(detail: "Track not found");

        // Non-empty => real cached lyrics. Empty array => negative marker
        // (checked, none found) persisted by the resolver; treat as not found
        // rather than re-querying the rate-limited providers.
        if (track.Lyrics is { Length: > 0 })
            return Ok(
                value: new LyricsResponseDto
                {
                    Data = ApplyLyricsOffset(lyrics: track.Lyrics, offsetMs: track.LyricsOffset),
                    Offset = track.LyricsOffset,
                }
            );
        if (track.Lyrics is not null)
            return NotFoundResponse(detail: "Subtitle not found");

        try
        {
            // Coalesced: concurrent requests from multiple devices for this
            // track share a single provider fetch instead of each hitting the
            // rate-limited Lrclib/Musixmatch queues.
            Lyric[]? lyrics = await _lyricsResolver.ResolveAsync(trackId: id);
            if (lyrics is null)
                return NotFoundResponse(detail: "Subtitle not found");
            return Ok(
                value: new LyricsResponseDto
                {
                    Data = ApplyLyricsOffset(lyrics: lyrics, offsetMs: track.LyricsOffset),
                    Offset = track.LyricsOffset,
                }
            );
        }
        catch (Exception e)
        {
            return NotFoundResponse(detail: e.Message);
        }
    }

    [HttpPatch]
    [Route(template: "{id:guid}/lyrics-offset")]
    public async Task<IActionResult> LyricsOffset(Guid id, [FromBody] PatchLyricsOffsetDto request)
    {

        if (request.Offset is not null && (request.Offset < -30000 || request.Offset > 30000))
            return ValidationProblem(detail: "Offset must be between -30000 and 30000 ms");

        Track? track = await _musicRepository.GetTrackWithIncludesAsync(id: id);

        if (track is null)
            return NotFoundResponse(detail: "Track not found");

        await _musicRepository.UpdateTrackLyricsOffsetAsync(track: track, offsetMs: request.Offset);

        return Ok(
            value: new LyricsOffsetResponseDto
            {
                Status = "ok",
                Message = request.Offset is null
                    ? "Lyrics offset cleared"
                    : $"Lyrics offset set to {request.Offset} ms",
                Offset = request.Offset,
            }
        );
    }

    // Pure: returns a new array and never mutates the input. The coalescing
    // LyricsResolver hands the same Lyric[] instance to every concurrent caller,
    // so in-place mutation here would double-apply the offset per extra device.
    private static Lyric[] ApplyLyricsOffset(Lyric[] lyrics, int? offsetMs)
    {
        if (offsetMs is null or 0)
            return lyrics;
        double offsetSec = offsetMs.Value / 1000.0;
        return lyrics
            .Select(selector: line =>
            {
                double newTotal = Math.Max(val1: 0, val2: line.Time.Total + offsetSec);
                int totalHundredths = (int)Math.Round(a: newTotal * 100);
                return new Lyric
                {
                    Text = line.Text,
                    Time = new()
                    {
                        Total = newTotal,
                        Minutes = totalHundredths / 6000,
                        Seconds = totalHundredths / 100 % 60,
                        Hundredths = totalHundredths % 100,
                    },
                };
            })
            .ToArray();
    }

    [HttpPost]
    [Route(template: "{id:guid}/playback")]
    public async Task<IActionResult> Playback(Guid id)
    {
        Guid userId = User.UserId();

        Track? track = await _musicRepository.GetTrackAsync(id: id);

        if (track is null)
            return NotFoundResponse(detail: "Track not found");

        await _musicRepository.RecordPlaybackAsync(trackId: id, userId: userId);

        return Ok(value: new StatusResponseDto<string> { Status = "ok", Message = "Playback recorded" });
    }
}

public record LyricsResponseDto
{
    [JsonProperty(propertyName: "data")]
    public Lyric[] Data { get; set; } = [];

    [JsonProperty(propertyName: "offset")]
    public int? Offset { get; set; }
}

public record PatchLyricsOffsetDto
{
    [JsonProperty(propertyName: "offset")]
    public int? Offset { get; set; }
}

public record LyricsOffsetResponseDto
{
    [JsonProperty(propertyName: "status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty(propertyName: "message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty(propertyName: "offset")]
    public int? Offset { get; set; }
}
