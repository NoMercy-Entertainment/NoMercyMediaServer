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

using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.Media.Subtitles.Dtos;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.Services.Video;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Subtitles;
using NoMercy.MediaProcessing.Files;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.Storage;

namespace NoMercy.Api.Controllers.V1.Media.Subtitles;

/// <summary>
/// Playback-time subtitle search + download. Reuses the encoder's OpenSubtitles
/// integration (<see cref="IOpenSubtitlesAdapter"/>) to find and fetch subtitles
/// matching whatever the requesting user is currently watching, then persists the
/// chosen candidate as a permanent VTT sidecar next to the video file.
/// </summary>
[ApiController]
[Tags("Media Subtitles")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/subtitles")]
public class SubtitlesController(
    VideoPlaylistManager videoPlaylistManager,
    IVideoFileRepository videoFileRepository,
    IFileRepository fileRepository,
    IOpenSubtitlesAdapter openSubtitlesAdapter,
    IStorageDriver storageDriver
) : BaseController
{
    private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(5);

    // Subtitles downloaded through this endpoint are always plain (non-sign,
    // non-song) full-length tracks — the only variant the search endpoint offers.
    private const string DownloadedSubtitleType = "full";
    private const string SidecarExtension = "vtt";

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

    /// <summary>
    /// Downloads the subtitle candidate identified by <see cref="SubtitleDownloadRequestDto.DownloadUrl"/>
    /// (the opaque token returned by <c>GET subtitles/search</c>), converts it to WebVTT, writes it
    /// as a permanent sidecar next to the video file, and registers it on <see cref="VideoFile.Subtitles"/>
    /// so it appears in every future watch response. Returns the sidecar's playable URL so the caller
    /// can also add it to the CURRENT playback session immediately, without waiting on a re-fetch.
    /// </summary>
    [HttpPost("download")]
    public async Task<IActionResult> Download(
        [FromBody] SubtitleDownloadRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(User))
            return UnauthorizedResponse("You do not have permission to download subtitles");

        if (request.Type != MediaTypes.MovieMediaType && request.Type != MediaTypes.TvMediaType)
            return BadRequestResponse($"Invalid type '{request.Type}'. Expected 'movie' or 'tv'.");

        if (string.IsNullOrWhiteSpace(request.DownloadUrl))
            return BadRequestResponse("downloadUrl is required");

        if (string.IsNullOrWhiteSpace(request.Language))
            return BadRequestResponse("language is required");

        Ulid? requestedVideoFileId = null;
        if (!string.IsNullOrWhiteSpace(request.VideoFileId))
        {
            if (!Ulid.TryParse(request.VideoFileId, out Ulid parsedVideoFileId))
                return BadRequestResponse("Invalid videoFileId");
            requestedVideoFileId = parsedVideoFileId;
        }

        string language = Language();
        string country = Country();

        (VideoPlaylistResponseDto? Item, List<VideoPlaylistResponseDto> Playlist) resolved;
        try
        {
            resolved = await videoPlaylistManager.GetPlaylist(
                userId,
                request.Type,
                request.Id.ToString(),
                null,
                language,
                country
            );
        }
        catch (ArgumentException)
        {
            return BadRequestResponse($"Invalid type '{request.Type}'. Expected 'movie' or 'tv'.");
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

        SubtitleCandidate candidate = new(
            Provider: "OpenSubtitles",
            Language: request.Language,
            Rating: 0,
            Downloads: 0,
            IsTrustedUploader: false,
            Fps: null,
            DownloadUrl: request.DownloadUrl,
            Format: (request.Format ?? "srt").Trim().ToLowerInvariant()
        );

        byte[] rawBytes;
        try
        {
            rawBytes = await openSubtitlesAdapter.DownloadAsync(candidate, ct);
        }
        catch (OpenSubtitlesRateLimitException)
        {
            return TooManyRequestsResponse(
                "OpenSubtitles is rate-limited by the upstream provider. Try again in a few minutes."
            );
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return InternalServerErrorResponse($"Failed to download subtitle: {ex.Message}");
        }

        string vttContent;
        try
        {
            vttContent = ConvertToVtt(Encoding.UTF8.GetString(rawBytes), candidate.Format);
        }
        catch (NotSupportedException)
        {
            return UnprocessableEntityResponse(
                $"Subtitle format '{candidate.Format}' is not supported — only SRT and VTT can be "
                    + "converted to the WebVTT sidecar the player expects."
            );
        }

        // Mirrors VideoPlaylistResponseDto.Subtitles(VideoFile)'s (pre-existing, unowned by this
        // slice) URL construction exactly — that private helper is what the NEXT watch response
        // reads to build the track URL, so the sidecar must land at the exact path it expects:
        // "{baseFolder}/subtitles{filenameWithoutExt}.{language}.{type}.{ext}" with no separator
        // between "subtitles" and the filename. Any change to that helper must update this too.
        string filenameNoExt = file.Filename.OrEmpty().Replace(".mp4", "").Replace(".m3u8", "");
        string sidecarFileName =
            $"subtitles{filenameNoExt}.{request.Language}.{DownloadedSubtitleType}.{SidecarExtension}";
        string sidecarPath = storageDriver.CombinePath(file.HostFolder, sidecarFileName);

        try
        {
            byte[] vttBytes = Encoding.UTF8.GetBytes(vttContent);
            await using Stream writeStream = storageDriver.OpenWrite(sidecarPath, overwrite: true);
            await writeStream.WriteAsync(vttBytes, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return InternalServerErrorResponse(
                $"Failed to write subtitle sidecar to storage: {ex.Message}"
            );
        }

        await RegisterSubtitleAsync(file, request.Language, ct);

        string baseFolder = $"/{file.Share}{file.Folder}";
        string trackUrl =
            $"{baseFolder}/subtitles{filenameNoExt}.{request.Language}.{DownloadedSubtitleType}.{SidecarExtension}";

        SubtitleDownloadResultDto result = new(
            File: trackUrl,
            Kind: "subtitles",
            Label: DownloadedSubtitleType,
            Language: request.Language
        );

        return Ok(
            new StatusResponseDto<SubtitleDownloadResultDto>
            {
                Status = "ok",
                Data = result,
                Message = "Subtitle downloaded for {0}",
                Args = [file.Filename],
            }
        );
    }

    /// <summary>
    /// Converts a downloaded candidate's raw text to the WebVTT the server serves. SRT is the
    /// overwhelming majority of OpenSubtitles results and converts losslessly; VTT candidates pass
    /// through unchanged. Anything else (ASS/SSA/SUB) isn't renderable by the VTT-only player, so
    /// it's rejected rather than written as a broken sidecar.
    /// </summary>
    private static string ConvertToVtt(string rawText, string format) =>
        format switch
        {
            "srt" or "subrip" => SubtitleFormatConverter.SrtToVtt(rawText),
            "vtt" or "webvtt" => rawText,
            _ => throw new NotSupportedException(format),
        };

    /// <summary>
    /// Merges the downloaded subtitle into <see cref="VideoFile.Subtitles"/> — the JSON column
    /// <see cref="VideoPlaylistResponseDto"/> reads to build <c>tracks</c> on every future watch
    /// response — replacing any existing entry for the same language + type so re-downloading a
    /// language doesn't leave duplicate track entries.
    /// </summary>
    private async Task RegisterSubtitleAsync(VideoFile file, string language, CancellationToken ct)
    {
        List<VideoPlaylistResponseDto.Subtitle> subtitles;
        try
        {
            subtitles =
                JsonConvert.DeserializeObject<List<VideoPlaylistResponseDto.Subtitle>>(
                    file.Subtitles ?? "[]"
                ) ?? [];
        }
        catch (JsonException)
        {
            subtitles = [];
        }

        subtitles.RemoveAll(s =>
            string.Equals(s.Language, language, StringComparison.OrdinalIgnoreCase)
            && string.Equals(s.Type, DownloadedSubtitleType, StringComparison.OrdinalIgnoreCase)
        );
        subtitles.Add(
            new()
            {
                Language = language,
                Type = DownloadedSubtitleType,
                Ext = SidecarExtension,
            }
        );

        await fileRepository.UpdateVideoFileSubtitlesAsync(
            file.Id,
            JsonConvert.SerializeObject(subtitles),
            ct
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
