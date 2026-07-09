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

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using NoMercy.Api.Controllers.V1.Media.Subtitles.Dtos;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.Services.Video;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Subtitles;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.Storage;

namespace NoMercy.Api.Controllers.V1.Media.Subtitles;

/// <summary>
/// Playback-time subtitle search. Reuses the encoder's OpenSubtitles integration
/// (<see cref="IOpenSubtitlesAdapter"/>) to search for subtitles matching whatever
/// the requesting user is currently watching. Search only — downloading and
/// persisting a chosen subtitle is a separate, later slice.
/// </summary>
[ApiController]
[Tags("Media Subtitles")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/subtitles")]
public class SubtitlesController(
    VideoPlaylistManager videoPlaylistManager,
    IVideoFileRepository videoFileRepository,
    IOpenSubtitlesAdapter openSubtitlesAdapter,
    IStorageDriver storageDriver
) : BaseController
{
    private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Searches OpenSubtitles for the video file identified by <paramref name="type"/> +
    /// <paramref name="id"/> (optionally narrowed to a specific <paramref name="videoFileId"/>).
    /// Tries a moviehash match first, falls back to filename, then title/season/episode —
    /// stopping at the first strategy that returns results.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string type,
        [FromQuery] int id,
        [FromQuery] string? videoFileId,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(User))
            return UnauthorizedResponse("You do not have permission to view subtitles");

        if (type != MediaTypes.MovieMediaType && type != MediaTypes.TvMediaType)
            return BadRequestResponse($"Invalid type '{type}'. Expected 'movie' or 'tv'.");

        Ulid? requestedVideoFileId = null;
        if (!string.IsNullOrWhiteSpace(videoFileId))
        {
            if (!Ulid.TryParse(videoFileId, out Ulid parsedVideoFileId))
                return BadRequestResponse("Invalid videoFileId");
            requestedVideoFileId = parsedVideoFileId;
        }

        string language = Language();
        string country = Country();

        (VideoPlaylistResponseDto? Item, List<VideoPlaylistResponseDto> Playlist) resolved;
        try
        {
            // VideoPlaylistManager resolves listId via int.Parse(dynamic) internally — it
            // must arrive as a string (the shape SignalR hands it JSON-deserialized), not
            // a raw int, or the dynamic dispatch throws RuntimeBinderException.
            resolved = await videoPlaylistManager.GetPlaylist(
                userId,
                type,
                id.ToString(),
                null,
                language,
                country
            );
        }
        catch (ArgumentException)
        {
            return BadRequestResponse($"Invalid type '{type}'. Expected 'movie' or 'tv'.");
        }

        VideoPlaylistResponseDto? target = requestedVideoFileId is not null
            ? resolved.Playlist.FirstOrDefault(p => p.VideoId == requestedVideoFileId.Value)
                ?? resolved.Item
            : resolved.Item;

        if (target is null)
            return NotFoundResponse("No video found for the given media");

        VideoFile? file = await videoFileRepository.GetByIdAsync(target.VideoId, ct);
        if (file is null)
            return NotFoundResponse("Video file not found");

        string[] languages = ResolveLanguages(Request.Query, language);

        (string? movieHash, long fileSize) = TryComputeHash(file);

        if (openSubtitlesAdapter.IsRateLimited)
            return TooManyRequestsResponse(
                "OpenSubtitles is rate-limited by the upstream provider. Try again in a few minutes."
            );

        IReadOnlyList<SubtitleCandidate> candidates = [];

        if (movieHash is not null)
            candidates = await openSubtitlesAdapter.SearchByHashAsync(
                movieHash,
                fileSize,
                languages,
                SearchTimeout,
                ct
            );

        if (candidates.Count == 0)
            candidates = await openSubtitlesAdapter.SearchByFilenameAsync(
                file.Filename,
                languages,
                SearchTimeout,
                ct
            );

        if (candidates.Count == 0)
        {
            bool isTv = type == MediaTypes.TvMediaType;
            string title = (isTv ? target.Show : target.Title) ?? file.Filename;
            int? season = isTv ? target.Season : null;
            int? episode = isTv ? target.Episode : null;
            int? year = target.Year > 0 ? (int)target.Year : null;

            candidates = await openSubtitlesAdapter.SearchByTitleAsync(
                title,
                season,
                episode,
                year,
                languages,
                SearchTimeout,
                ct
            );
        }

        // The adapter swallows OpenSubtitlesRateLimitException internally (by design, so an
        // encode never fails on a rate-limited subtitle lookup) and returns an empty list
        // instead. Re-check the flag so a rate-limited search reports 429, not an empty 200.
        if (candidates.Count == 0 && openSubtitlesAdapter.IsRateLimited)
            return TooManyRequestsResponse(
                "OpenSubtitles is rate-limited by the upstream provider. Try again in a few minutes."
            );

        List<SubtitleSearchResultDto> results = candidates.Select(MapToDto).ToList();

        return Ok(
            new StatusResponseDto<List<SubtitleSearchResultDto>>
            {
                Status = "ok",
                Data = results,
                Message = "Found {0} subtitle(s)",
                Args = [results.Count],
            }
        );
    }

    private (string? Hash, long FileSize) TryComputeHash(VideoFile file)
    {
        try
        {
            string path = storageDriver.CombinePath(file.HostFolder, file.Filename);
            if (!storageDriver.FileExists(path))
                return (null, 0);

            long fileSize = storageDriver.GetFileSize(path);
            using Stream stream = storageDriver.OpenRead(path);
            ulong hash = MovieHashHelper.ComputeMovieHash(stream, fileSize);
            return (MovieHashHelper.FormatHash(hash), fileSize);
        }
        catch (Exception)
        {
            // File may be on remote/unreachable storage — fall back to filename/title search.
            return (null, 0);
        }
    }

    private static string[] ResolveLanguages(IQueryCollection query, string fallback)
    {
        if (!query.TryGetValue("language", out StringValues values) || values.Count == 0)
            return [fallback];

        string[] languages = values
            .SelectMany(v =>
                (v ?? string.Empty).Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
            )
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return languages.Length > 0 ? languages : [fallback];
    }

    private static SubtitleSearchResultDto MapToDto(SubtitleCandidate candidate) =>
        new(
            Id: candidate.DownloadUrl,
            DownloadUrl: candidate.DownloadUrl,
            Language: candidate.Language,
            LanguageName: Culture.EnglishLanguageName(candidate.Language),
            ReleaseName: candidate.ReleaseName,
            FileName: candidate.FileName,
            Downloads: candidate.Downloads,
            Rating: candidate.Rating,
            Format: candidate.Format,
            HearingImpaired: candidate.HearingImpaired,
            Fps: candidate.Fps,
            Uploader: candidate.Uploader,
            Trusted: candidate.IsTrustedUploader
        );
}
