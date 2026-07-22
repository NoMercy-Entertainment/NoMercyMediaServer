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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.Media.Subtitles.Dtos;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.Services.Video;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Subtitles;
using NoMercy.MediaProcessing.Files;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Networking;
using NoMercy.Storage;

namespace NoMercy.Api.Controllers.V1.Media.Subtitles;

/// <summary>
/// Playback-time subtitle search + download. Reuses the encoder's OpenSubtitles
/// integration (<see cref="IOpenSubtitlesAdapter"/>) to find and fetch subtitles
/// matching whatever the requesting user is currently watching, then persists the
/// chosen candidate as a permanent VTT sidecar next to the video file.
/// </summary>
[ApiController]
[Tags(tags: "Media Subtitles")]
[ApiVersion(version: 1.0)]
[Authorize]
[Route(template: "api/v{version:apiVersion}/subtitles")]
public class SubtitlesController(
    VideoPlaylistManager videoPlaylistManager,
    IVideoFileRepository videoFileRepository,
    IFileRepository fileRepository,
    IFolderRepository folderRepository,
    IOpenSubtitlesAdapter openSubtitlesAdapter,
    IStorageFactory storageFactory,
    ILogger<SubtitlesController> logger
) : BaseController
{
    private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(seconds: 5);

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
    [HttpGet(template: "search")]
    public async Task<IActionResult> Search(
        [FromQuery] string type,
        [FromQuery] int id,
        [FromQuery] string? videoFileId,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view subtitles");

        if (type != MediaTypes.MovieMediaType && type != MediaTypes.TvMediaType)
            return BadRequestResponse(detail: $"Invalid type '{type}'. Expected 'movie' or 'tv'.");

        Ulid? requestedVideoFileId = null;
        if (!string.IsNullOrWhiteSpace(value: videoFileId))
        {
            if (!Ulid.TryParse(base32: videoFileId, ulid: out Ulid parsedVideoFileId))
                return BadRequestResponse(detail: "Invalid videoFileId");
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
                userId: userId,
                type: type,
                listId: id.ToString(),
                itemId: null,
                language: language,
                country: country
            );
        }
        catch (ArgumentException)
        {
            return BadRequestResponse(detail: $"Invalid type '{type}'. Expected 'movie' or 'tv'.");
        }

        VideoPlaylistResponseDto? target = requestedVideoFileId is not null
            ? resolved.Playlist.FirstOrDefault(predicate: p => p.VideoId == requestedVideoFileId.Value)
                ?? resolved.Item
            : resolved.Item;

        if (target is null)
            return NotFoundResponse(detail: "No video found for the given media");

        VideoFile? file = await videoFileRepository.GetByIdAsync(id: target.VideoId, ct: ct);
        if (file is null)
            return NotFoundResponse(detail: "Video file not found");

        string[] languages = ResolveLanguages(query: Request.Query, fallback: language);

        IStorage? storage = await ResolveStorageAsync(file: file);
        (string? movieHash, long fileSize) = storage is null
            ? (null, 0)
            : TryComputeHash(storage: storage, file: file);

        if (storage is null)
            logger.LogWarning(
                message: "No storage backend for video file {VideoFileId} (share {Share}) — "
                         + "falling back to filename/title search", args: [file.Id, file.Share]
            );

        if (openSubtitlesAdapter.IsRateLimited)
            return TooManyRequestsResponse(
                detail: "OpenSubtitles is rate-limited by the upstream provider. Try again in a few minutes."
            );

        IReadOnlyList<SubtitleCandidate> candidates = [];

        if (movieHash is not null)
            candidates = await openSubtitlesAdapter.SearchByHashAsync(
                movieHash: movieHash,
                fileSize: fileSize,
                languages: languages,
                timeout: SearchTimeout,
                ct: ct,
                priority: true
            );

        if (candidates.Count == 0)
            candidates = await openSubtitlesAdapter.SearchByFilenameAsync(
                filename: file.Filename,
                languages: languages,
                timeout: SearchTimeout,
                ct: ct,
                priority: true
            );

        if (candidates.Count == 0)
        {
            bool isTv = type == MediaTypes.TvMediaType;
            string title = (isTv ? target.Show : target.Title) ?? file.Filename;
            int? season = isTv ? target.Season : null;
            int? episode = isTv ? target.Episode : null;
            int? year = target.Year > 0 ? (int)target.Year : null;

            candidates = await openSubtitlesAdapter.SearchByTitleAsync(
                title: title,
                season: season,
                episode: episode,
                year: year,
                languages: languages,
                timeout: SearchTimeout,
                ct: ct,
                priority: true
            );
        }

        // The adapter swallows OpenSubtitlesRateLimitException internally (by design, so an
        // encode never fails on a rate-limited subtitle lookup) and returns an empty list
        // instead. Re-check the flag so a rate-limited search reports 429, not an empty 200.
        if (candidates.Count == 0 && openSubtitlesAdapter.IsRateLimited)
            return TooManyRequestsResponse(
                detail: "OpenSubtitles is rate-limited by the upstream provider. Try again in a few minutes."
            );

        List<SubtitleSearchResultDto> results = candidates.Select(selector: MapToDto).ToList();

        return Ok(
            value: new StatusResponseDto<List<SubtitleSearchResultDto>>
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
    [HttpPost(template: "download")]
    public async Task<IActionResult> Download(
        [FromBody] SubtitleDownloadRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to download subtitles");

        if (request.Type != MediaTypes.MovieMediaType && request.Type != MediaTypes.TvMediaType)
            return BadRequestResponse(detail: $"Invalid type '{request.Type}'. Expected 'movie' or 'tv'.");

        if (string.IsNullOrWhiteSpace(value: request.DownloadUrl))
            return BadRequestResponse(detail: "downloadUrl is required");

        // download_url is the opaque token from subtitles/search, but nothing forces
        // that: it is fetched server-side, so an authenticated user could point it at
        // loopback/LAN/cloud-metadata (SSRF). Reject any URL that is not an http(s)
        // link to a publicly routable host before doing anything else.
        if (!await ServerSideRequestGuard.IsSafePublicHttpUrlAsync(url: request.DownloadUrl, cancellationToken: ct))
            return BadRequestResponse(
                detail: "download_url must be an absolute http(s) URL that resolves to a public host."
            );

        if (string.IsNullOrWhiteSpace(value: request.Language))
            return BadRequestResponse(detail: "language is required");

        Ulid? requestedVideoFileId = null;
        if (!string.IsNullOrWhiteSpace(value: request.VideoFileId))
        {
            if (!Ulid.TryParse(base32: request.VideoFileId, ulid: out Ulid parsedVideoFileId))
                return BadRequestResponse(detail: "Invalid videoFileId");
            requestedVideoFileId = parsedVideoFileId;
        }

        string language = Language();
        string country = Country();

        (VideoPlaylistResponseDto? Item, List<VideoPlaylistResponseDto> Playlist) resolved;
        try
        {
            resolved = await videoPlaylistManager.GetPlaylist(
                userId: userId,
                type: request.Type,
                listId: request.Id.ToString(),
                itemId: null,
                language: language,
                country: country
            );
        }
        catch (ArgumentException)
        {
            return BadRequestResponse(detail: $"Invalid type '{request.Type}'. Expected 'movie' or 'tv'.");
        }

        VideoPlaylistResponseDto? target = requestedVideoFileId is not null
            ? resolved.Playlist.FirstOrDefault(predicate: p => p.VideoId == requestedVideoFileId.Value)
                ?? resolved.Item
            : resolved.Item;

        if (target is null)
            return NotFoundResponse(detail: "No video found for the given media");

        VideoFile? file = await videoFileRepository.GetByIdAsync(id: target.VideoId, ct: ct);
        if (file is null)
            return NotFoundResponse(detail: "Video file not found");

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
            // Someone is sat in front of the player waiting on this, so it jumps the queue ahead
            // of whatever the backlog sweep has already stacked up.
            rawBytes = await openSubtitlesAdapter.DownloadAsync(candidate: candidate, ct: ct, priority: true);
        }
        catch (OpenSubtitlesRateLimitException)
        {
            return TooManyRequestsResponse(
                detail: "OpenSubtitles is rate-limited by the upstream provider. Try again in a few minutes."
            );
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(
                exception: ex,
                message: "Failed to download subtitle from {DownloadUrl}",
                args: request.DownloadUrl
            );
            return InternalServerErrorResponse(detail: $"Failed to download subtitle: {ex.Message}");
        }

        string vttContent;
        try
        {
            vttContent = ConvertToVtt(rawText: Encoding.UTF8.GetString(bytes: rawBytes), format: candidate.Format);
        }
        catch (NotSupportedException)
        {
            logger.LogWarning(
                message: "Subtitle format {Format} is not convertible to WebVTT",
                args: candidate.Format
            );
            return UnprocessableEntityResponse(
                detail: $"Subtitle format '{candidate.Format}' is not supported — only SRT and VTT can be "
                        + "converted to the WebVTT sidecar the player expects."
            );
        }

        // Mirrors VideoPlaylistResponseDto.Subtitles(VideoFile)'s (pre-existing, unowned by this
        // slice) URL construction exactly — that private helper is what the NEXT watch response
        // reads to build the track URL, so the sidecar must land at the exact path it expects:
        // "{baseFolder}/subtitles{filenameWithoutExt}.{language}.{type}.{ext}" with no separator
        // between "subtitles" and the filename. Any change to that helper must update this too.
        string filenameNoExt = file.Filename.OrEmpty().Replace(oldValue: ".mp4", newValue: "").Replace(oldValue: ".m3u8", newValue: "");
        string sidecarFileName =
            $"subtitles{filenameNoExt}.{request.Language}.{DownloadedSubtitleType}.{SidecarExtension}";
        IStorage? storage = await ResolveStorageAsync(file: file);
        if (storage is null)
        {
            logger.LogError(
                message: "Cannot write subtitle sidecar: no storage backend for video file {VideoFileId} "
                         + "(share {Share})", args: [file.Id, file.Share]
            );
            return InternalServerErrorResponse(
                detail: "Could not resolve the storage this video lives on."
            );
        }

        string sidecarPath = storage.CombinePath(parent: file.HostFolder, child: sidecarFileName);

        try
        {
            byte[] vttBytes = Encoding.UTF8.GetBytes(s: vttContent);
            await using Stream writeStream = storage.OpenWrite(path: sidecarPath, overwrite: true);
            await writeStream.WriteAsync(buffer: vttBytes, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(exception: ex, message: "Failed to write subtitle sidecar to {SidecarPath}", args: sidecarPath);
            return InternalServerErrorResponse(
                detail: $"Failed to write subtitle sidecar to storage: {ex.Message}"
            );
        }

        await RegisterSubtitleAsync(file: file, language: request.Language, ct: ct);

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
            value: new StatusResponseDto<SubtitleDownloadResultDto>
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
            "srt" or "subrip" => SubtitleFormatConverter.SrtToVtt(srtContent: rawText),
            "vtt" or "webvtt" => rawText,
            _ => throw new NotSupportedException(message: format),
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
                    value: file.Subtitles ?? "[]"
                ) ?? [];
        }
        catch (JsonException)
        {
            subtitles = [];
        }

        subtitles.RemoveAll(match: s =>
            string.Equals(a: s.Language, b: language, comparisonType: StringComparison.OrdinalIgnoreCase)
            && string.Equals(a: s.Type, b: DownloadedSubtitleType, comparisonType: StringComparison.OrdinalIgnoreCase)
        );
        subtitles.Add(
            item: new()
            {
                Language = language,
                Type = DownloadedSubtitleType,
                Ext = SidecarExtension,
            }
        );

        await fileRepository.UpdateVideoFileSubtitlesAsync(
            videoFileId: file.Id,
            subtitlesJson: JsonConvert.SerializeObject(value: subtitles),
            ct: ct
        );
    }

    /// <summary>
    /// Resolves the storage the file actually lives on. VideoFile.Share is the folder id and the
    /// folder carries the driver, so a media file on any non-default backend is only reachable
    /// through the factory. The injected IStorageDriver is always LocalStorageDriver, which cannot
    /// see NFS/S3/WebDav-backed media at all: it silently failed the moviehash lookup and threw on
    /// the sidecar write.
    /// </summary>
    private async Task<IStorage?> ResolveStorageAsync(VideoFile file)
    {
        if (!Ulid.TryParse(base32: file.Share, ulid: out Ulid folderId))
            return null;

        Folder? folder = await folderRepository.GetFolderByIdAsync(folderId: folderId);
        if (folder is null)
            return null;

        return storageFactory.For(folderId: folder.Id, driverId: folder.DriverId, subPath: string.Empty);
    }

    private static (string? Hash, long FileSize) TryComputeHash(IStorage storage, VideoFile file)
    {
        try
        {
            string path = storage.CombinePath(parent: file.HostFolder, child: file.Filename);
            if (!storage.Exists(path: path))
                return (null, 0);

            long fileSize = storage.Size(path: path);
            using Stream stream = storage.OpenRead(path: path);
            ulong hash = MovieHashHelper.ComputeMovieHash(stream: stream, fileSize: fileSize);
            return (MovieHashHelper.FormatHash(hash: hash), fileSize);
        }
        catch (Exception)
        {
            // File may be on remote/unreachable storage — fall back to filename/title search.
            return (null, 0);
        }
    }

    private static string[] ResolveLanguages(IQueryCollection query, string fallback)
    {
        if (!query.TryGetValue(key: "language", value: out StringValues values) || values.Count == 0)
            return [fallback];

        string[] languages = values
            .SelectMany(selector: v =>
                (v ?? string.Empty).Split(
                    separator: ',',
                    options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
            )
            .Where(predicate: l => !string.IsNullOrWhiteSpace(value: l))
            .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return languages.Length > 0 ? languages : [fallback];
    }

    private static SubtitleSearchResultDto MapToDto(SubtitleCandidate candidate) =>
        new(
            Id: candidate.DownloadUrl,
            DownloadUrl: candidate.DownloadUrl,
            Language: candidate.Language,
            LanguageName: Culture.EnglishLanguageName(code: candidate.Language),
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
