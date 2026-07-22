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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.Middleware;
using NoMercy.Authorization;
using NoMercy.Data.DTOs;
using NoMercy.Data.Repositories;
using NoMercy.Data.Requests;
using NoMercy.Database;
using NoMercy.Database.Activity;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Analysis;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Domain;
using NoMercy.Storage;
using FolderPresetDto = NoMercy.Data.DTOs.Encoder.FolderPresetDto;
using IDefaultEncodingPresetLinker = NoMercy.MediaProcessing.Libraries.IDefaultEncodingPresetLinker;
using IJobDispatcher = NoMercy.MediaProcessing.Jobs.IJobDispatcher;

namespace NoMercy.Api.Controllers.V1.Dashboard.Media;

[ApiController]
[Tags(tags: "Dashboard Libraries")]
[ApiVersion(version: 1.0)]
[Authorize]
[Route(template: "api/v{version:apiVersion}/dashboard/libraries", Order = 10)]
public class LibrariesController(
    ILibraryRepository libraryRepository,
    IEncodingPresetRepository encodingPresetRepository,
    IFolderRepository folderRepository,
    IJobDispatcher jobDispatcher,
    ILanguageRepository languageRepository,
    IDbContextFactory<MediaContext> mediaContextFactory,
    IActivityLogger activityLogger,
    IStorageDriver storageDriver,
    IStorageFactory storageFactory,
    IDefaultEncodingPresetLinker defaultEncodingPresetLinker,
    IMediaAnalyzer mediaAnalyzer,
    ILogger<LibrariesController> logger
) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        Guid userId = User.UserId();

        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view libraries");

        IEnumerable<Library> libraries = await libraryRepository.GetLibraries(userId: userId);

        return Ok(
            value: new LibrariesDto
            {
                Data = libraries.Select(selector: library => new LibrariesResponseItemDto(library: library)),
            }
        );
    }

    [HttpPost]
    public async Task<IActionResult> Store()
    {
        Guid userId = User.UserId();

        if (!AuthPolicy.IsModerator(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to create a new library");

        try
        {
            await using MediaContext mediaContext =
                await mediaContextFactory.CreateDbContextAsync();
            int libraries = await mediaContext.Libraries.CountAsync();

            Library library = new()
            {
                Id = Ulid.NewUlid(),
                Title = $"Library {libraries}",
                AutoRefreshInterval = 30,
                ChapterImages = true,
                ExtractChapters = true,
                ExtractChaptersDuring = true,
                PerfectSubtitleMatch = true,
                Realtime = true,
                SpecialSeasonName = "Specials",
                Type = MediaTypes.MovieMediaType,
                Order = 99,
            };

            await libraryRepository.AddLibraryAsync(library: library, userId: userId);

            try
            {
                await activityLogger.LogConfigurationAsync(
                    type: "config.library_added",
                    userId: userId,
                    deviceId: Ulid.Empty,
                    configKey: $"library.{library.Id}",
                    oldValue: null,
                    newValue: new
                    {
                        id = library.Id.ToString(),
                        name = library.Title,
                        type = library.Type,
                    }
                );
            }
            catch (Exception ex)
            {
                logger.LogWarning(message: "Failed to log library created: {Message}", args: ex.Message);
            }

            return Ok(
                value: new StatusResponseDto<Library>
                {
                    Status = "ok",
                    Data = library,
                    Message = "Successfully created a new library.",
                    Args = [],
                }
            );
        }
        catch (Exception)
        {
            return InternalServerErrorResponse(detail: "Something went wrong creating the library");
        }
    }

    [HttpPatch]
    [Route(template: "{id:ulid}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Update(Ulid id, [FromBody] LibraryUpdateRequest request)
    {
        Library? library = await libraryRepository.GetLibraryByIdAsync(id: id);
        if (library is null)
            return NotFoundResponse(detail: "Library not found");

        if (request.EncodePresetId.HasValue)
        {
            EncodingPreset? encodingPreset = await encodingPresetRepository.GetByIdAsync(
                id: request.EncodePresetId.Value
            );
            if (encodingPreset is null)
                return NotFoundResponse(detail: "Encoding preset not found");
        }

        Guid userId = User.UserId();
        bool? oldRealtime = request.Realtime.HasValue ? library.Realtime : null;

        try
        {
            // Only update fields that are provided in the request
            if (request.Title != null)
                library.Title = request.Title;

            if (request.PerfectSubtitleMatch.HasValue)
                library.PerfectSubtitleMatch = request.PerfectSubtitleMatch.Value;

            if (request.Realtime.HasValue)
                library.Realtime = request.Realtime.Value;

            if (request.AutoEncodeOnScan.HasValue)
                library.AutoEncodeOnScan = request.AutoEncodeOnScan.Value;

            if (request.EncodePresetId.HasValue)
                library.EncodePresetId = request.EncodePresetId.Value;

            if (request.SpecialSeasonName != null)
                library.SpecialSeasonName = request.SpecialSeasonName;

            if (request.Type != null)
                library.Type = request.Type;

            await libraryRepository.UpdateLibraryAsync(library: library);

            // Only update subtitles if provided
            if (request.Subtitles != null)
            {
                List<Language> languages = await languageRepository.GetLanguagesAsync();
                List<int> languageIds = request
                    .Subtitles.Select(selector: subtitle =>
                        languages.FirstOrDefault(predicate: l => l.Iso6391 == subtitle)
                    )
                    .OfType<Language>()
                    .Select(selector: language => language.Id)
                    .ToList();

                await libraryRepository.SetLibraryLanguagesAsync(libraryId: library.Id, languageIds: languageIds);
            }
        }
        catch (Exception e)
        {
            logger.LogError(exception: e, message: e.Message);
            return InternalServerErrorResponse(
                detail: $"Something went wrong updating the library: {e.GetType().Name}: {e.Message}"
            );
        }

        if (oldRealtime.HasValue)
        {
            try
            {
                await activityLogger.LogConfigurationAsync(
                    type: "config.library_scan_schedule_changed",
                    userId: userId,
                    deviceId: Ulid.Empty,
                    configKey: $"library.{library.Id}.scan_schedule",
                    oldValue: new { realtime = oldRealtime.Value },
                    newValue: new { realtime = library.Realtime }
                );
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    message: "Failed to log library scan schedule change: {Message}",
                    args: ex.Message
                );
            }
        }

        // Only update folder libraries if provided
        if (request.FolderLibrary != null)
        {
            try
            {
                List<Folder> folders = await folderRepository.GetFoldersByLibraryIdAsync(
                    folderLibraries: request.FolderLibrary
                );
                FolderLibrary[] folderLibraries = folders
                    .Select(selector: folder => new FolderLibrary
                    {
                        LibraryId = library.Id,
                        FolderId = folder.Id,
                    })
                    .ToArray();

                await folderRepository.SyncFolderLibraryAsync(folderLibraries: folderLibraries, folders: folders);
            }
            catch (Exception e)
            {
                logger.LogError(exception: e, message: e.Message);
                return InternalServerErrorResponse(
                    detail: $"Something went wrong updating the library folders: {e.GetType().Name}: {e.Message}"
                );
            }

            try
            {
                List<EncodingPresetFolder> encodingPresetFolders = [];

                List<Folder> folders = await folderRepository.GetFoldersByLibraryIdAsync(
                    folderLibraries: request.FolderLibrary
                );

                foreach (FolderLibraryDto folder in request.FolderLibrary)
                {
                    Folder? folderDb = folders.FirstOrDefault(predicate: f => f.Id == folder.FolderId);
                    if (folderDb is null)
                        continue;

                    foreach (FolderPresetDto profile in folder.Folder.EncoderProfiles)
                    {
                        EncodingPreset? encodingPreset =
                            await encodingPresetRepository.GetByIdAsync(id: profile.Id);
                        if (encodingPreset is null)
                            continue;

                        encodingPresetFolders.Add(
                            item: new() { FolderId = folderDb.Id, PresetId = encodingPreset.Id }
                        );
                    }
                }

                await libraryRepository.SyncEncodingPresetFolderAsync(
                    encodingPresetFolders: encodingPresetFolders,
                    folders: folders
                );
            }
            catch (Exception e)
            {
                logger.LogError(exception: e, message: e.Message);
                return InternalServerErrorResponse(
                    detail: $"Something went wrong updating the library encoder profiles: {e.GetType().Name}: {e.Message}"
                );
            }
        }

        return Ok(
            value: new StatusResponseDto<Library>
            {
                Status = "ok",
                Message = "Successfully updated {0} library.",
                Args = [library.Title],
                Data = library,
            }
        );
    }

    [HttpDelete]
    [Route(template: "{id:ulid}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Delete(Ulid id)
    {
        Library? library = await libraryRepository.GetLibraryByIdAsync(id: id);

        if (library is null)
            return NotFoundResponse(detail: "Library not found");

        Guid userId = User.UserId();

        try
        {
            await libraryRepository.DeleteLibraryAsync(library: library);

            // Remove all associated folders from the middleware immediately
            foreach (FolderLibrary fl in library.FolderLibraries)
                DynamicStaticFilesMiddleware.RemoveFolder(folderId: fl.FolderId);

            await using (
                MediaContext refreshContext = await mediaContextFactory.CreateDbContextAsync()
            )
            {
                await UserCacheService.RefreshFolderIdsAsync(context: refreshContext);
            }

            if (EventBusProvider.IsConfigured)
            {
                await EventBusProvider.Current.PublishAsync(
                    @event: new LibraryDeletedEvent { LibraryId = library.Id, LibraryName = library.Title }
                );

                foreach (FolderLibrary fl in library.FolderLibraries)
                    await EventBusProvider.Current.PublishAsync(
                        @event: new FolderPathRemovedEvent { RequestPath = fl.FolderId }
                    );
            }

            try
            {
                await activityLogger.LogConfigurationAsync(
                    type: "config.library_removed",
                    userId: userId,
                    deviceId: Ulid.Empty,
                    configKey: $"library.{library.Id}",
                    oldValue: new { id = library.Id.ToString(), name = library.Title },
                    newValue: null
                );
            }
            catch (Exception ex)
            {
                logger.LogWarning(message: "Failed to log library removed: {Message}", args: ex.Message);
            }

            return Ok(
                value: new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Successfully deleted {0} library.",
                    Args = [library.Title],
                }
            );
        }
        catch (Exception e)
        {
            logger.LogError(exception: e, message: e.Message);
            return InternalServerErrorResponse(detail: "Something went wrong deleting the library");
        }
    }

    [HttpPatch]
    [Route(template: "sort")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Sort([FromBody] LibrarySortRequest request)
    {
        List<Library> libraries = await libraryRepository.GetAllLibrariesAsync();

        if (libraries.Count == 0)
            return NotFoundResponse(detail: "No libraries exist");

        try
        {
            foreach (LibrarySortRequestItem item in request.Libraries)
            {
                Library? lib = libraries.FirstOrDefault(predicate: l => l.Id == item.Id);
                if (lib is null)
                    continue;
                lib.Order = item.Order;
                await libraryRepository.UpdateLibraryAsync(library: lib);
            }

            return Ok(
                value: new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Successfully sorted libraries.",
                    Args = [],
                }
            );
        }
        catch (Exception)
        {
            return InternalServerErrorResponse(detail: "Something went wrong sorting the libraries");
        }
    }

    [HttpPost]
    [Route(template: "rescan")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Rescan()
    {
        List<Library> librariesList = await libraryRepository.GetAllLibrariesAsync();

        if (librariesList.Count == 0)
            return NotFoundResponse(detail: "No libraries found to rescan");

        foreach (Library library in librariesList)
        {
            foreach (LibraryMovie movie in library.LibraryMovies)
            {
                jobDispatcher.DispatchJob<FileRescanJob>(id: movie.MovieId, libraryId: movie.LibraryId);
            }

            foreach (LibraryTv show in library.LibraryTvs)
            {
                jobDispatcher.DispatchJob<FileRescanJob>(id: show.TvId, libraryId: show.LibraryId);
            }
        }

        return Ok(
            value: new StatusResponseDto<List<string?>>
            {
                Status = "ok",
                Message = "Rescanning all libraries.",
            }
        );
    }

    [HttpPost]
    [Route(template: "{id:ulid}/rescan")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Rescan(Ulid id)
    {
        Library? library = await libraryRepository.GetLibraryByIdAsync(id: id);

        if (library is null)
            return NotFoundResponse(detail: "Library not found");

        foreach (LibraryMovie movie in library.LibraryMovies)
        {
            jobDispatcher.DispatchJob<FileRescanJob>(id: movie.MovieId, libraryId: movie.LibraryId);
        }

        foreach (LibraryTv show in library.LibraryTvs)
        {
            jobDispatcher.DispatchJob<FileRescanJob>(id: show.TvId, libraryId: show.LibraryId);
        }

        return Ok(
            value: new StatusResponseDto<List<dynamic>>
            {
                Status = "ok",
                Message = "Rescanning {0} library.",
                Args = [library.Title],
            }
        );
    }

    [HttpPost]
    [Route(template: "refresh")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> RefreshAll()
    {
        List<Library> librariesList = await libraryRepository.GetAllLibrariesAsync();

        if (librariesList.Count == 0)
            return NotFoundResponse(detail: "No libraries found to refresh");

        List<string?> titles = [];

        foreach (Library library in librariesList)
        {
            jobDispatcher.DispatchJob<LibraryRescanJob>(libraryId: library.Id);
        }

        return Ok(
            value: new StatusResponseDto<List<string?>>
            {
                Status = "ok",
                Data = titles,
                Message = "Rescanning all libraries.",
            }
        );
    }

    [HttpPost]
    [Route(template: "{id:ulid}/refresh")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Refresh(Ulid id)
    {
        Library? library = await libraryRepository.GetLibraryByIdAsync(id: id);

        if (library is null)
            return NotFoundResponse(detail: "Library not found");

        jobDispatcher.DispatchJob<LibraryRescanJob>(libraryId: id);

        return Ok(
            value: new StatusResponseDto<List<dynamic>>
            {
                Status = "ok",
                Message = "Rescanning {0} library.",
                Args = [library.Title],
            }
        );
    }

    [HttpPost]
    [Route(template: "scan-new")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> ScanNewAll()
    {
        List<Library> librariesList = await libraryRepository.GetAllLibrariesAsync();

        if (librariesList.Count == 0)
            return NotFoundResponse(detail: "No libraries found to scan");

        foreach (Library library in librariesList)
        {
            jobDispatcher.DispatchJob<LibraryScanJob>(libraryId: library.Id);
        }

        return Ok(
            value: new StatusResponseDto<List<string?>>
            {
                Status = "ok",
                Message = "Scanning all libraries for new items.",
            }
        );
    }

    [HttpPost]
    [Route(template: "{id:ulid}/scan-new")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> ScanNew(Ulid id)
    {
        Library? library = await libraryRepository.GetLibraryByIdAsync(id: id);

        if (library is null)
            return NotFoundResponse(detail: "Library not found");

        jobDispatcher.DispatchJob<LibraryScanJob>(libraryId: id);

        return Ok(
            value: new StatusResponseDto<List<dynamic>>
            {
                Status = "ok",
                Message = "Scanning {0} library for new items.",
                Args = [library.Title],
            }
        );
    }

    [HttpPost]
    [Route(template: "{id:ulid}/folders")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> AddFolder(Ulid id, [FromBody] FolderRequest request)
    {
        Library? library = await libraryRepository.GetLibraryByIdAsync(id: id);

        if (library is null)
            return NotFoundResponse(detail: "Library not found");

        if (request.DriverId == default)
            return BadRequestResponse(detail: "driver_id is required. Every folder must have a driver.");

        // Captured before the upsert so we can tell a brand-new folder from
        // one that already existed (e.g. attaching the same NFS path to a
        // second library). Only a genuinely new folder gets the default
        // auto-encode preset link — never an existing/pre-slice folder.
        Folder? preExistingFolder = await folderRepository.GetFolderByDriverAndPathAsync(
            driverId: request.DriverId,
            requestPath: request.Path
        );
        bool isNewFolder = preExistingFolder is null;

        try
        {
            Folder folder = new()
            {
                Id = Ulid.NewUlid(),
                Path = request.Path,
                DriverId = request.DriverId,
            };

            await folderRepository.AddFolderAsync(folder: folder);
        }
        catch (Exception ex)
        {
            logger.LogError(
                message: "[AddFolder] failed for library={Id} driver={DriverId} path='{Path}': {Ex}", args: [id, request.DriverId, request.Path, ex]
            );
            return InternalServerErrorResponse(detail: "Something went wrong adding the folder");
        }

        // Scope the lookup to the same driver so two folders with the same
        // sub-path on different drivers (e.g. NFS Anime/Anime + S3 Anime-S3
        // both mapped to library "Anime") don't cross-link.
        Folder? pathAsync = await folderRepository.GetFolderByDriverAndPathAsync(
            driverId: request.DriverId,
            requestPath: request.Path
        );

        if (pathAsync is null)
            return NotFoundResponse(detail: "Folder not found");

        FolderLibrary folderLibrary = new() { LibraryId = library.Id, FolderId = pathAsync.Id };

        await folderRepository.AddFolderLibraryAsync(folderLibrary: folderLibrary);

        if (isNewFolder)
        {
            await defaultEncodingPresetLinker.AttachDefaultIfMissingAsync(folderId: pathAsync.Id);

            // A brand-new folder can already contain media on disk (the
            // common "point me at my existing library" flow). Dispatch the
            // same scan-new job the scan-new endpoint uses so pre-existing
            // files get imported without the user having to hit that
            // endpoint manually. Never runs for a pre-existing folder being
            // attached to another library — isNewFolder guards that — so
            // upgrades never trigger a scan for folders that were already
            // known before this slice shipped.
            jobDispatcher.DispatchJob<LibraryScanJob>(libraryId: library.Id);
        }

        // Register the folder with the middleware directly so it can serve files immediately
        DynamicStaticFilesMiddleware.AddFolder(folderId: pathAsync.Id, driverId: pathAsync.DriverId, subPath: pathAsync.Path);
        await using MediaContext refreshContext = await mediaContextFactory.CreateDbContextAsync();
        await UserCacheService.RefreshFolderIdsAsync(context: refreshContext);

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                @event: new FolderPathAddedEvent
                {
                    RequestPath = pathAsync.Id,
                    DriverId = pathAsync.DriverId,
                    SubPath = pathAsync.Path,
                }
            );
        }

        return Ok(
            value: new StatusResponseDto<FolderLibrary>
            {
                Status = "ok",
                Message = "Successfully added folder to {0} library.",
                Args = [pathAsync.Path],
                Data = folderLibrary,
            }
        );
    }

    [HttpPatch]
    [Route(template: "{id:ulid}/folders/{folderId:ulid}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> UpdateFolder(
        Ulid id,
        Ulid folderId,
        [FromBody] FolderRequest request
    )
    {
        Folder? folder = await folderRepository.GetFolderByIdAsync(folderId: folderId);

        if (folder is null)
            return NotFoundResponse(detail: "Folder not found");

        try
        {
            folder.Path = request.Path;
            await folderRepository.UpdateFolderAsync(folder: folder);

            // Update the middleware directly so it can serve files from the new path immediately
            DynamicStaticFilesMiddleware.RemoveFolder(folderId: folder.Id);
            DynamicStaticFilesMiddleware.AddFolder(folderId: folder.Id, driverId: folder.DriverId, subPath: folder.Path);
            await using (
                MediaContext refreshContext = await mediaContextFactory.CreateDbContextAsync()
            )
            {
                await UserCacheService.RefreshFolderIdsAsync(context: refreshContext);
            }

            if (EventBusProvider.IsConfigured)
            {
                await EventBusProvider.Current.PublishAsync(
                    @event: new FolderPathRemovedEvent { RequestPath = folder.Id }
                );
                await EventBusProvider.Current.PublishAsync(
                    @event: new FolderPathAddedEvent
                    {
                        RequestPath = folder.Id,
                        DriverId = folder.DriverId,
                        SubPath = folder.Path,
                    }
                );
            }

            return Ok(
                value: new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Successfully updated folder {0}.",
                    Args = [folder.Path],
                }
            );
        }
        catch (Exception)
        {
            return InternalServerErrorResponse(detail: "Something went wrong updating the library folder");
        }
    }

    [HttpDelete]
    [Route(template: "{id:ulid}/folders/{folderId:ulid}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> DeleteFolder(Ulid id, Ulid folderId)
    {
        Folder? folder = await folderRepository.GetFolderByIdAsync(folderId: folderId);

        if (folder is null)
            return NotFoundResponse(detail: "Folder not found");

        try
        {
            await folderRepository.DeleteFolderAsync(folder: folder);

            // Remove the folder from the middleware immediately
            DynamicStaticFilesMiddleware.RemoveFolder(folderId: folder.Id);
            await using (
                MediaContext refreshContext = await mediaContextFactory.CreateDbContextAsync()
            )
            {
                await UserCacheService.RefreshFolderIdsAsync(context: refreshContext);
            }

            if (EventBusProvider.IsConfigured)
            {
                await EventBusProvider.Current.PublishAsync(
                    @event: new FolderPathRemovedEvent { RequestPath = folder.Id }
                );
            }

            return Ok(
                value: new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Successfully deleted folder {0}.",
                    Args = [folder.Path],
                }
            );
        }
        catch (Exception ex)
        {
            // Surface the underlying failure (FK constraint, missing dep,
            // event-bus crash) so future delete-folder regressions don't
            // require Stoney to grep for a generic 500 in production logs.
            logger.LogError(
                message: "[DeleteFolder] folder={FolderId} library={Id} failed: {Ex}", args: [folderId, id, ex]
            );
            return InternalServerErrorResponse(detail: "Something went wrong deleting the library folder");
        }
    }

    [HttpPost]
    [Route(template: "{id:ulid}/folders/{folderId:ulid}/encoder_profiles")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> AddEncoderProfile(
        Ulid id,
        Ulid folderId,
        [FromBody] ProfilesRequest request
    )
    {
        Folder? folder = await folderRepository.GetFolderByIdAsync(folderId: folderId);

        if (folder is null)
            return NotFoundResponse(detail: "Folder not found");

        try
        {
            EncodingPresetFolder[] encodingPresetFolder = request
                .Profiles.Select(selector: profile => new EncodingPresetFolder
                {
                    FolderId = folder.Id,
                    PresetId = Ulid.Parse(base32: profile),
                })
                .ToArray();

            await libraryRepository.AddEncodingPresetFolderAsync(encodingPresetFolders: encodingPresetFolder);

            return Ok(
                value: new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Successfully added encoder profile to {0} folder.",
                    Args = [folder.Path],
                }
            );
        }
        catch (Exception)
        {
            return InternalServerErrorResponse(detail: "Something went wrong adding the encoder profile");
        }
    }

    [HttpDelete]
    [Route(template: "{id:ulid}/folders/{folderId:ulid}/encoder_profiles/{encoderProfileId:ulid}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> DeleteEncoderProfile(
        Ulid id,
        Ulid folderId,
        Ulid encoderProfileId
    )
    {
        EncodingPreset? encodingPreset = await encodingPresetRepository.GetByIdAsync(
            id: encoderProfileId
        );

        if (encodingPreset is null)
            return NotFoundResponse(detail: "Encoder profile not found");

        try
        {
            await using MediaContext context = await mediaContextFactory.CreateDbContextAsync();
            await context
                .EncodingPresetFolders.Where(predicate: link =>
                    link.FolderId == folderId && link.PresetId == encoderProfileId
                )
                .ExecuteDeleteAsync();

            return Ok(
                value: new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Successfully deleted encoder profile {0}.",
                    Args = [encodingPreset.Name],
                }
            );
        }
        catch (Exception)
        {
            return InternalServerErrorResponse(detail: "Something went wrong deleting the encoder profile");
        }
    }

    [HttpPost]
    [Route(template: "move")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Move([FromBody] MoveRequest request)
    {
        Folder? folder = await folderRepository.GetFolderByIdAsync(folderId: request.FolderId);

        if (folder is null)
            return NotFoundResponse(detail: "Folder not found");

        try
        {
            await using MediaContext mediaContext =
                await mediaContextFactory.CreateDbContextAsync();

            FileRepository fileRepository = new(context: mediaContext, storageDriver: storageDriver);
            FileManager fileManager = new(
                fileRepository: fileRepository,
                storageFactory: storageFactory,
                storageDriver: storageDriver,
                mediaAnalyzer: mediaAnalyzer
            );

            await fileManager.MoveToLibraryFolder(id: request.Id, folder: folder);

            return Ok(
                value: new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Successfully moved item {0}.",
                    Args = [request.Id.ToString()],
                }
            );
        }
        catch (Exception)
        {
            return InternalServerErrorResponse(detail: "Something went wrong moving the item");
        }
    }
}
