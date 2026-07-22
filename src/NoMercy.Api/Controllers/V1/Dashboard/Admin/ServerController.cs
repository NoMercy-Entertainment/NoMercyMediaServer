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

using System.Collections.Concurrent;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.Monitoring;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.NmSystem.Wallpaper;
using NoMercy.Providers.AcoustId;
using NoMercy.Providers.Helpers;
using NoMercy.Storage;
using NoMercyQueue;
using Serilog.Events;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Configuration = NoMercy.Database.Models.Common.Configuration;
using HttpClient = System.Net.Http.HttpClient;
using IJobDispatcher = NoMercy.MediaProcessing.Jobs.IJobDispatcher;
using Image = NoMercy.Database.Models.Media.Image;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

[ApiController]
[Tags(tags: "Dashboard Server Management")]
[ApiVersion(version: 1.0)]
[Authorize]
[Route(template: "api/v{version:apiVersion}/dashboard/server", Order = 10)]
public class ServerController(
    ILogger<ServerController> logger,
    ResourceMonitor resourceMonitor,
    IUpdateChecker updateChecker,
    IHostApplicationLifetime appLifetime,
    AppDbContext appContext,
    FileRepository fileRepository,
    IFileListService fileListService,
    IJobDispatcher jobDispatcher,
    QueueRunner queueRunner,
    IEventBus eventBus,
    IWallpaperService wallpaperService,
    INetworkDiscovery networkDiscovery,
    IHttpClientFactory httpClientFactory,
    IStorageDriver storageDriver,
    IStorageFactory storageFactory,
    ILibraryRepository libraryRepository,
    IFolderRepository folderRepository,
    IImageRepository imageRepository,
    IAuthTokenStore authTokenStore,
    IAudioFingerprinter audioFingerprinter
) : BaseController
{
    private IHostApplicationLifetime ApplicationLifetime { get; } = appLifetime;

    [HttpGet]
    [Authorize(Policy = "Moderator")]
    public IActionResult Index()
    {
        return Ok();
    }

    [HttpGet]
    [Route(template: "setup")]
    public async Task<IActionResult> Setup()
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsModerator(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to access the setup");

        List<Library> libraries = await libraryRepository.GetLibraries(userId: userId);

        int libraryCount = libraries.Count;

        int folderCount = libraries
            .SelectMany(selector: library => library.FolderLibraries)
            .Select(selector: folderLibrary => folderLibrary.Folder)
            .Count();

        int encoderProfileCount = libraries
            .SelectMany(selector: library => library.FolderLibraries)
            .Select(selector: folderLibrary => folderLibrary.Folder)
            .Count(predicate: folder => folder.EncodingPresetFolders.Count > 0);

        return Ok(
            value: new StatusResponseDto<SetupResponseDto>
            {
                Status = "ok",
                Data = new()
                {
                    SetupComplete = libraryCount > 0 && folderCount > 0 && encoderProfileCount > 0,
                },
            }
        );
    }

    [HttpPost]
    [Route(template: "start")]
    [Authorize(Policy = "MediaAccess")]
    public IActionResult StartServer()
    {
        return NotImplementedResponse(detail: "Starting the server via the API is not implemented.");
    }

    [HttpPost]
    [Route(template: "stop")]
    [Authorize(Policy = "Moderator")]
    public IActionResult StopServer()
    {
        ApplicationLifetime.StopApplication();
        return Content(content: "Done");
    }

    public class InvalidateRequest
    {
        [JsonProperty(propertyName: "queryKey")]
        public dynamic[] QueryKey { get; set; } = [];
    }

    [HttpPost]
    [Route(template: "invalidate")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Invalidate([FromBody] InvalidateRequest request)
    {
        await eventBus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = request.QueryKey });

        return Content(content: "Done");
    }

    [HttpPost]
    [Route(template: "restart")]
    [Authorize(Policy = "Moderator")]
    public IActionResult RestartServer()
    {
        return NotImplementedResponse(detail: "Restarting the server via the API is not implemented.");
    }

    [HttpGet(template: "update/check")]
    public async Task<IActionResult> CheckForUpdate()
    {
        return Ok(value: new { updateAvailable = await updateChecker.IsUpdateAvailableAsync() });
    }

    [HttpPost]
    [Route(template: "shutdown")]
    [Authorize(Policy = "Moderator")]
    public IActionResult Shutdown()
    {
        ApplicationLifetime.StopApplication();
        return Content(content: "Done");
    }

    [HttpPost]
    [Route(template: "loglevel")]
    [Authorize(Policy = "Moderator")]
    public IActionResult LogLevel(LogEventLevel level)
    {
        Logger.SetLogLevel(level: level);

        return Content(content: "Log level set to " + level);
    }

    [HttpPost]
    [Route(template: "addfiles")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> AddFiles([FromBody] AddFilesRequest request)
    {
        Library? library = await libraryRepository.GetLibraryByIdLiteAsync(id: request.LibraryId);

        if (library == null)
            return NotFoundResponse(detail: "Library not found");

        // Determine whether the folder lives on a remote driver. When it does,
        // the path from filelist is already driver-relative — do not call
        // Path.GetFullPath, which would prepend the process working directory.
        Folder? folder = await folderRepository.GetFolderByIdAsync(folderId: request.FolderId);

        bool isRemoteDriver =
            folder?.Driver is not null
            && !string.Equals(a: folder.Driver.Type, b: "local", comparisonType: StringComparison.OrdinalIgnoreCase);

        // When source_driver_id is provided (NFS/SMB source different from the
        // destination library folder), the path is driver-relative and must not
        // be expanded via Path.GetFullPath.
        Ulid? sourceDriverId = null;
        bool isRemoteSource = false;
        if (!string.IsNullOrWhiteSpace(value: request.SourceDriverId))
        {
            if (Ulid.TryParse(base32: request.SourceDriverId, ulid: out Ulid parsedSourceDriver))
            {
                sourceDriverId = parsedSourceDriver;
                isRemoteSource = true;
            }
        }

        try
        {
            if (library.Type == "music")
            {
                logger.LogTrace(message: "Adding music files to library");
                string directoryPath =
                    isRemoteDriver || isRemoteSource
                        ? request.Files[0].Path
                        : Path.GetFullPath(path: request.Files[0].Path);

                jobDispatcher.DispatchJob<ReleaseImportJob>(
                    libraryId: library.Id,
                    folderId: request.FolderId,
                    releaseId: request.Files[0].Id.ToGuid(),
                    filePath: directoryPath
                );

                return Ok(value: request);
            }

            // Manual "add files" is a deliberate operator action to import and
            // process the selected files — typically staged in a download/source
            // location off the library root (source_driver_id set). It always
            // encodes the explicit file: FileRescanJob only re-walks existing
            // library folders and cannot see a file staged elsewhere, so routing
            // a manual import through it silently drops it. Library.AutoEncodeOnScan
            // gates only the automatic file-watcher path (AutoEncodeSubscriber),
            // never this manual import. A configured EncodePresetId narrows the
            // encode to that one preset; a null value keeps the folder's presets.
            foreach (AddFile file in request.Files)
            {
                string filePath =
                    isRemoteDriver || isRemoteSource ? file.Path : Path.GetFullPath(path: file.Path);

                VideoEncodeJob job = new()
                {
                    LibraryId = library.Id,
                    FolderId = request.FolderId,
                    Id = file.Id,
                    InputFile = filePath,
                    SourceDriverId = sourceDriverId,
                    PresetId = library.EncodePresetId,
                };
                jobDispatcher.Dispatch(job: job, onQueue: job.QueueName, priority: job.Priority);
            }
            return Ok(value: request);
        }
        catch (Exception e)
        {
            logger.LogError(exception: e, message: "Failed to add file to library");
            return BadRequestResponse(detail: e.Message);
        }
    }

    [HttpPost]
    [Route(template: "directorytree")]
    [Authorize(Policy = "Moderator")]
    public IActionResult DirectoryTree([FromBody] PathRequest request)
    {
        try
        {
            List<DirectoryTree> array = fileRepository.GetDirectoryTree(folder: request.Folder);

            return Ok(value: new StatusResponseDto<List<DirectoryTree>> { Status = "ok", Data = array });
        }
        catch (Exception)
        {
            return InternalServerErrorResponse(
                detail: "Something went wrong retrieving the directory tree"
            );
        }
    }

    // Synthetic folder ID for ad-hoc filelist browse — mirrors StorageBrowserController's pattern.
    private static Ulid SyntheticFileListFolderId(Ulid driverId) => driverId;

    [HttpPost]
    [Route(template: "filelist")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> FileList([FromBody] FileListRequest request)
    {
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogInformation(
            message: "[FileList] folder={Folder} type={Type} driver={DriverId}", args: [request.Folder, request.Type, request.DriverId]
        );

        IStorage? resolvedStorage = null;
        if (!string.IsNullOrWhiteSpace(value: request.DriverId))
        {
            if (!Ulid.TryParse(base32: request.DriverId, ulid: out Ulid driverId))
                return BadRequestResponse(detail: "driver_id is not a valid ULID.");

            resolvedStorage = storageFactory.For(
                folderId: SyntheticFileListFolderId(driverId: driverId),
                driverId: driverId,
                subPath: string.Empty
            );
        }

        if (request.Type == "music")
        {
            IStorageDriver effectiveDriver = resolvedStorage is not null
                ? FileRepository.StorageDriverFromStorage(storage: resolvedStorage)
                : fileRepository.StorageDriver;

            List<FileItem> fileList = await FileRepository.GetMusicBrainzReleasesInDirectory(
                folder: request.Folder,
                storageDriver: effectiveDriver,
                audioFingerprinter: audioFingerprinter
            );

            logger.LogInformation(
                message: "[FileList] returned {Count} entries in {ElapsedMilliseconds}ms (music)", args: [fileList.Count, sw.ElapsedMilliseconds]
            );

            return Ok(
                value: new DataResponseDto<FileListResponseDto>
                {
                    Data = new() { Status = "ok", Files = SortFileList(files: fileList) },
                }
            );
        }
        else
        {
            List<FileItem> fileList = resolvedStorage is not null
                ? await fileListService.GetFilesInDirectory(
                    directoryPath: request.Folder,
                    libraryType: request.Type,
                    storage: resolvedStorage
                )
                : await fileListService.GetFilesInDirectory(directoryPath: request.Folder, libraryType: request.Type);

            logger.LogInformation(
                message: "[FileList] returned {Count} entries in {ElapsedMilliseconds}ms", args: [fileList.Count, sw.ElapsedMilliseconds]
            );

            return Ok(
                value: new DataResponseDto<FileListResponseDto>
                {
                    Data = new() { Status = "ok", Files = SortFileList(files: fileList) },
                }
            );
        }
    }

    // Order by show → season → episode → path so SxxExx files line up the way
    // the operator expects (S01E01 before S01E10 before S02E01); falls back to
    // Path string for files without a season/episode match.
    private static List<FileItem> SortFileList(List<FileItem> files) =>
        files
            .OrderBy(keySelector: f => f.Parsed?.Title ?? string.Empty, comparer: StringComparer.OrdinalIgnoreCase)
            .ThenBy(keySelector: f => f.Parsed?.Season ?? int.MaxValue)
            .ThenBy(keySelector: f => f.Parsed?.Episode ?? int.MaxValue)
            .ThenBy(keySelector: f => f.Path, comparer: StringComparer.OrdinalIgnoreCase)
            .ToList();

    [NonAction]
    private string DeviceName()
    {
        Configuration? device = appContext.Configuration.FirstOrDefault(predicate: device =>
            device.Key == "serverName"
        );
        return device?.Value ?? Environment.MachineName;
    }

    [HttpGet]
    [Route(template: "info")]
    [ResponseCache(NoStore = true)]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> ServerInfo()
    {
        bool setupComplete = await libraryRepository.HasCompletedSetupAsync();

        return Ok(
            value: new StatusResponseDto<ServerInfoDto>
            {
                Status = "ok",
                Data = new()
                {
                    Server = DeviceName(),
                    Cpu = Info.CpuNames,
                    Gpu = Info.GpuNames,
                    Os = $"{Info.Platform.ToTitleCase()} {Info.OsVersion}",
                    Arch = Info.Architecture,
                    Version = Software.GetReleaseVersion(),
                    BootTime = Info.StartTime,
                    SetupComplete = setupComplete,
                },
            }
        );
    }

    [HttpPatch]
    [Route(template: "info")]
    public async Task<IActionResult> UpdateServerInfo([FromBody] ServerUpdateRequest request)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsModerator(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to update server information");

        Configuration? configuration = await appContext
            .Configuration.AsTracking()
            .FirstOrDefaultAsync(predicate: configuration => configuration.Key == "serverName");

        try
        {
            if (configuration == null)
            {
                configuration = new()
                {
                    Key = "serverName",
                    Value = request.Name,
                    ModifiedBy = userId,
                };
                await appContext.Configuration.AddAsync(entity: configuration);
            }
            else
            {
                configuration.Value = request.Name;
                configuration.ModifiedBy = userId;
            }

            await appContext.SaveChangesAsync();

            HttpClient client = httpClientFactory.CreateClient(name: HttpClientNames.General);
            client.BaseAddress = new(uriString: ExternalServicesConfig.Current.ApiServerBaseUrl);

            string? token = authTokenStore.AccessToken;
            if (string.IsNullOrEmpty(value: token))
            {
                return ServiceUnavailableResponse(detail: "Re-authentication in progress");
            }

            client.DefaultRequestHeaders.Authorization = new(scheme: "Bearer", parameter: token);

            HttpRequestMessage httpRequestMessage = new(method: HttpMethod.Patch, requestUri: "name")
            {
                Content = new FormUrlEncodedContent(
                    nameValueCollection: new Dictionary<string, string>
                    {
                        [key: "id"] = Info.DeviceId.ToString(),
                        [key: "name"] = request.Name,
                    }
                ),
            };

            using HttpResponseMessage httpResponse = await client.SendAsync(request: httpRequestMessage);
            string response = await httpResponse.Content.ReadAsStringAsync();

            StatusResponseDto<string>? data = JsonConvert.DeserializeObject<
                StatusResponseDto<string>
            >(value: response);

            if (data == null)
                return UnprocessableEntityResponse(detail: "Server name could not be updated");

            return Ok(
                value: new StatusResponseDto<string>
                {
                    Status = data.Status,
                    Message = data.Message,
                    Args = [],
                }
            );
        }
        catch (Exception e)
        {
            return UnprocessableEntityResponse(detail: "Server name could not be updated: " + e.Message);
        }
    }

    [HttpGet]
    [Route(template: "resources")]
    [ResponseCache(NoStore = true)]
    [Authorize(Policy = "Moderator")]
    public IActionResult Resources()
    {
        Resource? resource;
        try
        {
            resource = resourceMonitor.Monitor();
        }
        catch (Exception e)
        {
            return UnprocessableEntityResponse(
                detail: "Resource monitor could not be started: " + e.Message
            );
        }

        List<ResourceMonitorDto> storage = StorageMonitor.Main();

        return Ok(
            value: new ResourceInfoDto
            {
                Cpu = resource.Cpu,
                Gpu = resource.Gpu,
                Memory = resource.Memory,
                Storage = storage,
            }
        );
    }

    [HttpGet]
    [Route(template: "paths")]
    [ResponseCache(Duration = 3600)]
    [Authorize(Policy = "Moderator")]
    public IActionResult ServerPaths()
    {
        List<ServerPathsDto> list =
        [
            new() { Key = "Cache", Value = AppFiles.CachePath },
            new() { Key = "Logs", Value = AppFiles.LogPath },
            new() { Key = "Transcodes", Value = AppFiles.TranscodePath },
            new() { Key = "Configs", Value = AppFiles.ConfigPath },
        ];

        return Ok(value: list);
    }

    [HttpGet]
    [Route(template: "/files/${depth:int}/${path:required}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Files(string path, int depth)
    {
        MediaScan mediaScan = new(driver: storageDriver);

        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .Process(rootFolder: path, depth: depth);

        await mediaScan.DisposeAsync();

        return Ok(value: folders);
    }

    [HttpPatch]
    [Route(template: "workers/{worker}/{count:int:min(0)}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> UpdateWorkers(string worker, int count)
    {
        if (await queueRunner.SetWorkerCount(name: worker, max: count, userId: User.UserId()))
            return Ok(value: $"{worker} worker count set to {count}");

        return BadRequestResponse(detail: $"{worker} worker count could not be set to {count}");
    }

    [HttpGet]
    [Route(template: "storage")]
    [Authorize(Policy = "Moderator")]
    public IActionResult Storage()
    {
        // StorageJob storageJob = new(StorageMonitor.Storage);
        // JobDispatcher.Dispatch(storageJob, "data", 1000);

        return Ok(value: StorageMonitor.Storage);
    }

    [HttpPost]
    [Route(template: "wallpaper")]
    [Authorize(Policy = "Owner")]
    public async Task<IActionResult> SetWallpaper([FromBody] WallpaperRequest request)
    {
        if (!wallpaperService.IsSupported)
            return BadRequestResponse(detail: "Wallpaper setting is not supported on this platform");

        Image? wallpaper = await imageRepository.GetImageByFilePathAsync(filePath: request.Path);

        if (wallpaper?.FilePath is null)
            return NotFoundResponse(detail: "Wallpaper not found");

        string path = Path.Combine(
            path1: AppFiles.ImagesPath,
            path2: "original",
            path3: wallpaper.FilePath.Replace(oldValue: "/", newValue: "")
        );

        string color = request.Color ?? await GetDominantColorAsync(path: path);

        wallpaperService.SetSilent(imagePath: path, style: request.Style, hexColor: color);

        return Ok(
            value: new StatusResponseDto<string> { Status = "ok", Message = "Wallpaper set successfully" }
        );
    }

    private static readonly ConcurrentDictionary<string, string> DominantColorCache = new();

    private static async Task<string> GetDominantColorAsync(string path)
    {
        if (DominantColorCache.TryGetValue(key: path, value: out string? cached))
            return cached;

        string color = await Task.Run(function: () =>
        {
            using Image<Rgb24> image = SixLabors.ImageSharp.Image.Load<Rgb24>(path: path);
            image.Mutate(operation: x =>
                x.Resize(
                        options: new ResizeOptions
                        {
                            Sampler = KnownResamplers.NearestNeighbor,
                            Size = new(width: 100, height: 0),
                        }
                    )
                    .Quantize(quantizer: new OctreeQuantizer { Options = { MaxColors = 1 } })
            );

            Rgb24 dominant = image[x: 0, y: 0];
            return dominant.ToHexString();
        });

        DominantColorCache.TryAdd(key: path, value: color);
        return color;
    }

    [HttpPost]
    [Route(template: "changeIp")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> ChangeIp([FromBody] ChangeIpRequest request)
    {
        if (string.IsNullOrEmpty(value: request.Ip))
            return BadRequestResponse(detail: "New IP address is required");

        logger.LogInformation(message: "Changing IP address to {Ip}", args: request.Ip);

        networkDiscovery.InternalIp = request.Ip;

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = $"IP address changed to {request.Ip}",
            }
        );
    }

    public class ChangeIpRequest
    {
        public string Ip { get; set; } = string.Empty;
    }
}
