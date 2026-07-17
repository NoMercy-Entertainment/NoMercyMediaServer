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
[Tags("Dashboard Server Management")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/server", Order = 10)]
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
    [Route("setup")]
    public async Task<IActionResult> Setup()
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to access the setup");

        List<Library> libraries = await libraryRepository.GetLibraries(userId);

        int libraryCount = libraries.Count;

        int folderCount = libraries
            .SelectMany(library => library.FolderLibraries)
            .Select(folderLibrary => folderLibrary.Folder)
            .Count();

        int encoderProfileCount = libraries
            .SelectMany(library => library.FolderLibraries)
            .Select(folderLibrary => folderLibrary.Folder)
            .Count(folder => folder.EncodingPresetFolders.Count > 0);

        return Ok(
            new StatusResponseDto<SetupResponseDto>
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
    [Route("start")]
    [Authorize(Policy = "MediaAccess")]
    public IActionResult StartServer()
    {
        return NotImplementedResponse("Starting the server via the API is not implemented.");
    }

    [HttpPost]
    [Route("stop")]
    [Authorize(Policy = "Moderator")]
    public IActionResult StopServer()
    {
        ApplicationLifetime.StopApplication();
        return Content("Done");
    }

    public class InvalidateRequest
    {
        [JsonProperty("queryKey")]
        public dynamic[] QueryKey { get; set; } = [];
    }

    [HttpPost]
    [Route("invalidate")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Invalidate([FromBody] InvalidateRequest request)
    {
        await eventBus.PublishAsync(new LibraryRefreshedEvent { QueryKey = request.QueryKey });

        return Content("Done");
    }

    [HttpPost]
    [Route("restart")]
    [Authorize(Policy = "Moderator")]
    public IActionResult RestartServer()
    {
        return NotImplementedResponse("Restarting the server via the API is not implemented.");
    }

    [HttpGet("update/check")]
    public async Task<IActionResult> CheckForUpdate()
    {
        return Ok(new { updateAvailable = await updateChecker.IsUpdateAvailableAsync() });
    }

    [HttpPost]
    [Route("shutdown")]
    [Authorize(Policy = "Moderator")]
    public IActionResult Shutdown()
    {
        ApplicationLifetime.StopApplication();
        return Content("Done");
    }

    [HttpPost]
    [Route("loglevel")]
    [Authorize(Policy = "Moderator")]
    public IActionResult LogLevel(LogEventLevel level)
    {
        Logger.SetLogLevel(level);

        return Content("Log level set to " + level);
    }

    [HttpPost]
    [Route("addfiles")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> AddFiles([FromBody] AddFilesRequest request)
    {
        Library? library = await libraryRepository.GetLibraryByIdLiteAsync(request.LibraryId);

        if (library == null)
            return NotFoundResponse("Library not found");

        // Determine whether the folder lives on a remote driver. When it does,
        // the path from filelist is already driver-relative — do not call
        // Path.GetFullPath, which would prepend the process working directory.
        Folder? folder = await folderRepository.GetFolderByIdAsync(request.FolderId);

        bool isRemoteDriver =
            folder?.Driver is not null
            && !string.Equals(folder.Driver.Type, "local", StringComparison.OrdinalIgnoreCase);

        // When source_driver_id is provided (NFS/SMB source different from the
        // destination library folder), the path is driver-relative and must not
        // be expanded via Path.GetFullPath.
        Ulid? sourceDriverId = null;
        bool isRemoteSource = false;
        if (!string.IsNullOrWhiteSpace(request.SourceDriverId))
        {
            if (Ulid.TryParse(request.SourceDriverId, out Ulid parsedSourceDriver))
            {
                sourceDriverId = parsedSourceDriver;
                isRemoteSource = true;
            }
        }

        try
        {
            if (library.Type == "music")
            {
                logger.LogTrace("Adding music files to library");
                string directoryPath =
                    isRemoteDriver || isRemoteSource
                        ? request.Files[0].Path
                        : Path.GetFullPath(request.Files[0].Path);

                jobDispatcher.DispatchJob<ReleaseImportJob>(
                    library.Id,
                    request.FolderId,
                    request.Files[0].Id.ToGuid(),
                    directoryPath
                );

                return Ok(request);
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
                    isRemoteDriver || isRemoteSource ? file.Path : Path.GetFullPath(file.Path);

                VideoEncodeJob job = new()
                {
                    LibraryId = library.Id,
                    FolderId = request.FolderId,
                    Id = file.Id,
                    InputFile = filePath,
                    SourceDriverId = sourceDriverId,
                    PresetId = library.EncodePresetId,
                };
                jobDispatcher.Dispatch(job, job.QueueName, job.Priority);
            }
            return Ok(request);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to add file to library");
            return BadRequestResponse(e.Message);
        }
    }

    [HttpPost]
    [Route("directorytree")]
    [Authorize(Policy = "Moderator")]
    public IActionResult DirectoryTree([FromBody] PathRequest request)
    {
        try
        {
            List<DirectoryTree> array = fileRepository.GetDirectoryTree(request.Folder);

            return Ok(new StatusResponseDto<List<DirectoryTree>> { Status = "ok", Data = array });
        }
        catch (Exception)
        {
            return InternalServerErrorResponse(
                "Something went wrong retrieving the directory tree"
            );
        }
    }

    // Synthetic folder ID for ad-hoc filelist browse — mirrors StorageBrowserController's pattern.
    private static Ulid SyntheticFileListFolderId(Ulid driverId) => driverId;

    [HttpPost]
    [Route("filelist")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> FileList([FromBody] FileListRequest request)
    {
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogInformation(
            "[FileList] folder={Folder} type={Type} driver={DriverId}",
            request.Folder,
            request.Type,
            request.DriverId
        );

        IStorage? resolvedStorage = null;
        if (!string.IsNullOrWhiteSpace(request.DriverId))
        {
            if (!Ulid.TryParse(request.DriverId, out Ulid driverId))
                return BadRequestResponse("driver_id is not a valid ULID.");

            resolvedStorage = storageFactory.For(
                folderId: SyntheticFileListFolderId(driverId),
                driverId: driverId,
                subPath: string.Empty
            );
        }

        if (request.Type == "music")
        {
            IStorageDriver effectiveDriver = resolvedStorage is not null
                ? FileRepository.StorageDriverFromStorage(resolvedStorage)
                : fileRepository.StorageDriver;

            List<FileItem> fileList = await FileRepository.GetMusicBrainzReleasesInDirectory(
                request.Folder,
                effectiveDriver,
                audioFingerprinter
            );

            logger.LogInformation(
                "[FileList] returned {Count} entries in {ElapsedMilliseconds}ms (music)",
                fileList.Count,
                sw.ElapsedMilliseconds
            );

            return Ok(
                new DataResponseDto<FileListResponseDto>
                {
                    Data = new() { Status = "ok", Files = SortFileList(fileList) },
                }
            );
        }
        else
        {
            List<FileItem> fileList = resolvedStorage is not null
                ? await fileListService.GetFilesInDirectory(
                    request.Folder,
                    request.Type,
                    resolvedStorage
                )
                : await fileListService.GetFilesInDirectory(request.Folder, request.Type);

            logger.LogInformation(
                "[FileList] returned {Count} entries in {ElapsedMilliseconds}ms",
                fileList.Count,
                sw.ElapsedMilliseconds
            );

            return Ok(
                new DataResponseDto<FileListResponseDto>
                {
                    Data = new() { Status = "ok", Files = SortFileList(fileList) },
                }
            );
        }
    }

    // Order by show → season → episode → path so SxxExx files line up the way
    // the operator expects (S01E01 before S01E10 before S02E01); falls back to
    // Path string for files without a season/episode match.
    private static List<FileItem> SortFileList(List<FileItem> files) =>
        files
            .OrderBy(f => f.Parsed?.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Parsed?.Season ?? int.MaxValue)
            .ThenBy(f => f.Parsed?.Episode ?? int.MaxValue)
            .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    [NonAction]
    private string DeviceName()
    {
        Configuration? device = appContext.Configuration.FirstOrDefault(device =>
            device.Key == "serverName"
        );
        return device?.Value ?? Environment.MachineName;
    }

    [HttpGet]
    [Route("info")]
    [ResponseCache(NoStore = true)]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> ServerInfo()
    {
        bool setupComplete = await libraryRepository.HasCompletedSetupAsync();

        return Ok(
            new StatusResponseDto<ServerInfoDto>
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
    [Route("info")]
    public async Task<IActionResult> UpdateServerInfo([FromBody] ServerUpdateRequest request)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to update server information");

        Configuration? configuration = await appContext
            .Configuration.AsTracking()
            .FirstOrDefaultAsync(configuration => configuration.Key == "serverName");

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
                await appContext.Configuration.AddAsync(configuration);
            }
            else
            {
                configuration.Value = request.Name;
                configuration.ModifiedBy = userId;
            }

            await appContext.SaveChangesAsync();

            HttpClient client = httpClientFactory.CreateClient(HttpClientNames.General);
            client.BaseAddress = new(ExternalServicesConfig.Current.ApiServerBaseUrl);

            string? token = authTokenStore.AccessToken;
            if (string.IsNullOrEmpty(token))
            {
                return ServiceUnavailableResponse("Re-authentication in progress");
            }

            client.DefaultRequestHeaders.Authorization = new("Bearer", token);

            HttpRequestMessage httpRequestMessage = new(HttpMethod.Patch, "name")
            {
                Content = new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["id"] = Info.DeviceId.ToString(),
                        ["name"] = request.Name,
                    }
                ),
            };

            using HttpResponseMessage httpResponse = await client.SendAsync(httpRequestMessage);
            string response = await httpResponse.Content.ReadAsStringAsync();

            StatusResponseDto<string>? data = JsonConvert.DeserializeObject<
                StatusResponseDto<string>
            >(response);

            if (data == null)
                return UnprocessableEntityResponse("Server name could not be updated");

            return Ok(
                new StatusResponseDto<string>
                {
                    Status = data.Status,
                    Message = data.Message,
                    Args = [],
                }
            );
        }
        catch (Exception e)
        {
            return UnprocessableEntityResponse("Server name could not be updated: " + e.Message);
        }
    }

    [HttpGet]
    [Route("resources")]
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
                "Resource monitor could not be started: " + e.Message
            );
        }

        List<ResourceMonitorDto> storage = StorageMonitor.Main();

        return Ok(
            new ResourceInfoDto
            {
                Cpu = resource.Cpu,
                Gpu = resource.Gpu,
                Memory = resource.Memory,
                Storage = storage,
            }
        );
    }

    [HttpGet]
    [Route("paths")]
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

        return Ok(list);
    }

    [HttpGet]
    [Route("/files/${depth:int}/${path:required}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Files(string path, int depth)
    {
        MediaScan mediaScan = new(storageDriver);

        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .Process(path, depth);

        await mediaScan.DisposeAsync();

        return Ok(folders);
    }

    [HttpPatch]
    [Route("workers/{worker}/{count:int:min(0)}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> UpdateWorkers(string worker, int count)
    {
        if (await queueRunner.SetWorkerCount(worker, count, User.UserId()))
            return Ok($"{worker} worker count set to {count}");

        return BadRequestResponse($"{worker} worker count could not be set to {count}");
    }

    [HttpGet]
    [Route("storage")]
    [Authorize(Policy = "Moderator")]
    public IActionResult Storage()
    {
        // StorageJob storageJob = new(StorageMonitor.Storage);
        // JobDispatcher.Dispatch(storageJob, "data", 1000);

        return Ok(StorageMonitor.Storage);
    }

    [HttpPost]
    [Route("wallpaper")]
    [Authorize(Policy = "Owner")]
    public async Task<IActionResult> SetWallpaper([FromBody] WallpaperRequest request)
    {
        if (!wallpaperService.IsSupported)
            return BadRequestResponse("Wallpaper setting is not supported on this platform");

        Image? wallpaper = await imageRepository.GetImageByFilePathAsync(request.Path);

        if (wallpaper?.FilePath is null)
            return NotFoundResponse("Wallpaper not found");

        string path = Path.Combine(
            AppFiles.ImagesPath,
            "original",
            wallpaper.FilePath.Replace("/", "")
        );

        string color = request.Color ?? await GetDominantColorAsync(path);

        wallpaperService.SetSilent(path, request.Style, color);

        return Ok(
            new StatusResponseDto<string> { Status = "ok", Message = "Wallpaper set successfully" }
        );
    }

    private static readonly ConcurrentDictionary<string, string> DominantColorCache = new();

    private static async Task<string> GetDominantColorAsync(string path)
    {
        if (DominantColorCache.TryGetValue(path, out string? cached))
            return cached;

        string color = await Task.Run(() =>
        {
            using Image<Rgb24> image = SixLabors.ImageSharp.Image.Load<Rgb24>(path);
            image.Mutate(x =>
                x.Resize(
                        new ResizeOptions
                        {
                            Sampler = KnownResamplers.NearestNeighbor,
                            Size = new(100, 0),
                        }
                    )
                    .Quantize(new OctreeQuantizer { Options = { MaxColors = 1 } })
            );

            Rgb24 dominant = image[0, 0];
            return dominant.ToHexString();
        });

        DominantColorCache.TryAdd(path, color);
        return color;
    }

    [HttpPost]
    [Route("changeIp")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> ChangeIp([FromBody] ChangeIpRequest request)
    {
        if (string.IsNullOrEmpty(request.Ip))
            return BadRequestResponse("New IP address is required");

        logger.LogInformation("Changing IP address to {Ip}", request.Ip);

        networkDiscovery.InternalIp = request.Ip;

        return Ok(
            new StatusResponseDto<string>
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
