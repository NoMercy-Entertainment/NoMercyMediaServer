using System.Collections.Concurrent;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Helpers.Extensions;
using NoMercy.Helpers.Monitoring;
using NoMercy.Helpers.Wallpaper;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using NoMercy.Storage;
using NoMercyQueue.Core.Interfaces;
using Serilog.Events;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Configuration = NoMercy.Database.Models.Common.Configuration;
using HttpClient = System.Net.Http.HttpClient;
using Image = NoMercy.Database.Models.Media.Image;
using JobDispatcher = NoMercy.MediaProcessing.Jobs.JobDispatcher;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

[ApiController]
[Tags("Dashboard Server Management")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/server", Order = 10)]
public class ServerController(
    IHostApplicationLifetime appLifetime,
    AppDbContext appContext,
    FileRepository fileRepository,
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
    IImageRepository imageRepository
) : BaseController
{
    private IHostApplicationLifetime ApplicationLifetime { get; } = appLifetime;

    [HttpGet]
    public IActionResult Index()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to access the dashboard");

        return Ok();
    }

    [HttpGet]
    [Route("setup")]
    public async Task<IActionResult> Setup()
    {
        Guid userId = User.UserId();
        if (!User.IsModerator())
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
            .Count(folder => folder.EncoderProfileFolder.Count > 0);

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
    public IActionResult StartServer()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to start the server");

        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost]
    [Route("stop")]
    public IActionResult StopServer()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to stop the server");

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
    public async Task<IActionResult> Invalidate([FromBody] InvalidateRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse(
                "You do not have permission to invalidate the library cache"
            );

        await eventBus.PublishAsync(new LibraryRefreshEvent { QueryKey = request.QueryKey });

        return Content("Done");
    }

    [HttpPost]
    [Route("restart")]
    public IActionResult RestartServer()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to restart the server");

        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpGet("update/check")]
    public async Task<IActionResult> CheckForUpdate()
    {
        return Ok(new { updateAvailable = await UpdateChecker.IsUpdateAvailableAsync() });
    }

    [HttpPost]
    [Route("shutdown")]
    public IActionResult Shutdown()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to shutdown the server");

        ApplicationLifetime.StopApplication();
        return Content("Done");
    }

    [HttpPost]
    [Route("loglevel")]
    public IActionResult LogLevel(LogEventLevel level)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to set the log level");

        Logger.SetLogLevel(level);

        return Content("Log level set to " + level);
    }

    [HttpPost]
    [Route("addfiles")]
    public async Task<IActionResult> AddFiles([FromBody] AddFilesRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to add files");

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
                Logger.App("Adding music files to library", LogEventLevel.Verbose);
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

            foreach (AddFile file in request.Files)
            {
                string filePath =
                    isRemoteDriver || isRemoteSource ? file.Path : Path.GetFullPath(file.Path);

                jobDispatcher.DispatchJob<VideoEncodeJob>(
                    library.Id,
                    request.FolderId,
                    file.Id,
                    filePath,
                    sourceDriverId
                );
            }
            return Ok(request);
        }
        catch (Exception e)
        {
            Logger.App(e, LogEventLevel.Error);
            return BadRequestResponse(e.Message);
        }
    }

    [HttpPost]
    [Route("directorytree")]
    public IActionResult DirectoryTree([FromBody] PathRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view folders");

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
    public async Task<IActionResult> FileList([FromBody] FileListRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view files");

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        Logger.App(
            $"[FileList] folder={request.Folder} type={request.Type} driver={request.DriverId}",
            LogEventLevel.Information
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
                effectiveDriver
            );

            Logger.App(
                $"[FileList] returned {fileList.Count} entries in {sw.ElapsedMilliseconds}ms (music)",
                LogEventLevel.Information
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
                ? await fileRepository.GetFilesInDirectory(
                    request.Folder,
                    request.Type,
                    resolvedStorage
                )
                : await fileRepository.GetFilesInDirectory(request.Folder, request.Type);

            Logger.App(
                $"[FileList] returned {fileList.Count} entries in {sw.ElapsedMilliseconds}ms",
                LogEventLevel.Information
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
    public async Task<IActionResult> ServerInfo()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view server information");

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
        if (!User.IsModerator())
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
            client.BaseAddress = new(Config.ApiServerBaseUrl);

            string? token = Globals.Globals.AccessToken;
            if (string.IsNullOrEmpty(token))
            {
                return StatusCode(503, new { message = "Re-authentication in progress" });
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
    public IActionResult Resources()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view server resources");

        Resource? resource;
        try
        {
            resource = ResourceMonitor.Monitor();
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
    public IActionResult ServerPaths()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view server paths");

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
    public async Task<IActionResult> Files(string path, int depth)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view files");

        MediaScan mediaScan = new(storageDriver);

        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .Process(path, depth);

        await mediaScan.DisposeAsync();

        return Ok(folders);
    }

    [HttpPatch]
    [Route("workers/{worker}/{count:int:min(0)}")]
    public async Task<IActionResult> UpdateWorkers(string worker, int count)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update workers");

        if (await queueRunner.SetWorkerCount(worker, count, User.UserId()))
            return Ok($"{worker} worker count set to {count}");

        return BadRequestResponse($"{worker} worker count could not be set to {count}");
    }

    [HttpGet]
    [Route("storage")]
    public IActionResult Storage()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view server paths");

        // StorageJob storageJob = new(StorageMonitor.Storage);
        // JobDispatcher.Dispatch(storageJob, "data", 1000);

        return Ok(StorageMonitor.Storage);
    }

    [HttpPost]
    [Route("wallpaper")]
    public async Task<IActionResult> SetWallpaper([FromBody] WallpaperRequest request)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to set wallpaper");

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
    public async Task<IActionResult> ChangeIp([FromBody] ChangeIpRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to change the IP address");

        if (string.IsNullOrEmpty(request.Ip))
            return BadRequestResponse("New IP address is required");

        Logger.App($"Changing IP address to {request.Ip}");

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
