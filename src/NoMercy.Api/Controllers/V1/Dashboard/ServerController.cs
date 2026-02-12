using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.DTOs.Common;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers;
using NoMercy.Helpers.Extensions;
using NoMercy.Helpers.Monitoring;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.Networking.Dto;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Queue;
using Serilog.Events;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Configuration = NoMercy.Database.Models.Common.Configuration;
using HttpClient = System.Net.Http.HttpClient;
using Image = NoMercy.Database.Models.Media.Image;
using JobDispatcher = NoMercy.MediaProcessing.Jobs.JobDispatcher;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Server Management")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/server", Order = 10)]
public class ServerController(
    IHostApplicationLifetime appLifetime,
    MediaContext context,
    FileRepository fileRepository,
    JobDispatcher jobDispatcher,
    QueueRunner queueRunner) : BaseController
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
            return Problem(
                title: "Unauthorized.",
                detail: "You do not have permission to access the setup");

        List<Library> libraries = await context.Libraries
            .Include(library => library.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .ThenInclude(folder => folder.EncoderProfileFolder)
            .ThenInclude(encoderProfileFolder => encoderProfileFolder.EncoderProfile)
            .Include(library => library.LibraryUsers
                .Where(x => x.UserId.Equals(userId))
            )
            .ThenInclude(libraryUser => libraryUser.User)
            .ToListAsync();

        int libraryCount = libraries.Count;

        int folderCount = libraries
            .SelectMany(library => library.FolderLibraries)
            .Select(folderLibrary => folderLibrary.Folder)
            .Count();

        int encoderProfileCount = libraries
            .SelectMany(library => library.FolderLibraries)
            .Select(folderLibrary => folderLibrary.Folder)
            .Count(folder => folder.EncoderProfileFolder.Count > 0);

        return Ok(new StatusResponseDto<SetupResponseDto>
        {
            Status = "ok",
            Data = new()
            {
                SetupComplete = libraryCount > 0
                                && folderCount > 0
                                && encoderProfileCount > 0
            }
        });
    }

    [HttpPost]
    [Route("start")]
    public IActionResult StartServer()
    {
        if (!User.IsAllowed())
            return Problem(
                title: "Unauthorized.",
                detail: "You do not have permission to start the server");

        // ApplicationLifetime.StopApplication();
        return Content("Done");
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
    public IActionResult Invalidate([FromBody] InvalidateRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to invalidate the library cache");

        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = request.QueryKey
        });
        
        return Content("Done");
    }

    [HttpPost]
    [Route("restart")]
    public IActionResult RestartServer()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to restart the server");

        // ApplicationLifetime.StopApplication();
        return Content("Done");
    }

    [HttpGet("update/check")]
    public IActionResult CheckForUpdate()
    {
        return Ok(new
        {
            updateAvailable = UpdateChecker.IsUpdateAvailable()
        });
    }

    [HttpPost]
    [Route("shutdown")]
    public IActionResult Shutdown()
    {
        if (!User.IsModerator())
            return Problem(
                "You do not have permission to shutdown the server",
                type: "/docs/errors/forbidden");

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

        Library? library = await context.Libraries.FirstOrDefaultAsync(x => x.Id == request.LibraryId);

        if (library == null)
            return NotFoundResponse("Library not found");

        try
        {
            if (library.Type == "music")
            {
                Logger.App("Adding music files to library", LogEventLevel.Verbose);
                string directoryPath = Path.GetFullPath(request.Files[0].Path);
                jobDispatcher.DispatchJob<ProcessReleaseFolderJob>(
                    library.Id,
                    request.FolderId,
                    request.Files[0].Id.ToGuid(),
                    directoryPath);

                return Ok(request);
            }

            foreach (AddFile file in request.Files)
            {
                string filePath = Path.GetFullPath(file.Path);
                jobDispatcher.DispatchJob<EncodeVideoJob>(
                    library.Id,
                    request.FolderId,
                    file.Id,
                    filePath
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

            return Ok(new StatusResponseDto<List<DirectoryTree>>
            {
                Status = "ok",
                Data = array
            });
        }
        catch (Exception ex)
        {
            return UnprocessableEntity(new StatusResponseDto<List<DirectoryTree>>
            {
                Status = "error",
                Message = ex.Message
            });
        }
    }

    [HttpPost]
    [Route("filelist")]
    public async Task<IActionResult> FileList([FromBody] FileListRequest request)
    {
        if (!User.IsModerator())
            return Problem(
                title: "Unauthorized.",
                detail: "You do not have permission to view files");

        if (request.Type == "music")
        {
            List<FileItem> fileList = await FileRepository.GetMusicBrainzReleasesInDirectory(request.Folder);
            return Ok(new DataResponseDto<FileListResponseDto>
            {
                Data = new()
                {
                    Status = "ok",
                    Files = fileList
                        .OrderBy(file => file.Path)
                        .ToList()
                }
            });
        }
        else
        {
            List<FileItem> fileList = await fileRepository.GetFilesInDirectory(request.Folder, request.Type);

            return Ok(new DataResponseDto<FileListResponseDto>
            {
                Data = new()
                {
                    Status = "ok",
                    Files = fileList
                        .OrderBy(file => file.Path)
                        .ToList()
                }
            });
        }
    }

    [NonAction]
    private string DeviceName()
    {
        Configuration? device = context.Configuration.FirstOrDefault(device => device.Key == "serverName");
        return device?.Value ?? Environment.MachineName;
    }

    [HttpGet]
    [Route("info")]
    [ResponseCache(Duration = 3600)]
    public IActionResult ServerInfo()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view server information");

        bool setupComplete = context.Libraries.Any()
                             && context.Folders.Any()
                             && context.EncoderProfiles.Any();

        return Ok(new StatusResponseDto<ServerInfoDto>
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
                SetupComplete = setupComplete
            }
        });
    }


    [HttpPatch]
    [Route("info")]
    public async Task<IActionResult> UpdateServerInfo([FromBody] ServerUpdateRequest request)
    {
        Guid userId = User.UserId();
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update server information");

        Configuration? configuration = await context.Configuration
            .AsTracking()
            .FirstOrDefaultAsync(configuration => configuration.Key == "serverName");

        try
        {
            if (configuration == null)
            {
                configuration = new()
                {
                    Key = "serverName",
                    Value = request.Name,
                    ModifiedBy = userId
                };
                await context.Configuration.AddAsync(configuration);
            }
            else
            {
                configuration.Value = request.Name;
                configuration.ModifiedBy = userId;
            }

            await context.SaveChangesAsync();

            HttpClient client = new();
            client.BaseAddress = new(Config.ApiServerBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.DefaultRequestHeaders.Authorization = new("Bearer", Globals.Globals.AccessToken);

            HttpRequestMessage httpRequestMessage = new(HttpMethod.Patch, "name")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["id"] = Info.DeviceId.ToString(),
                    ["name"] = request.Name
                })
            };

            string response = await client
                .SendAsync(httpRequestMessage)
                .Result.Content.ReadAsStringAsync();

            StatusResponseDto<string>? data = JsonConvert.DeserializeObject<StatusResponseDto<string>>(response);

            if (data == null)
                return UnprocessableEntity(new StatusResponseDto<string>
                {
                    Status = "error",
                    Message = "Server name could not be updated",
                    Args = []
                });

            return Ok(new StatusResponseDto<string>
            {
                Status = data.Status,
                Message = data.Message,
                Args = []
            });
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
            return UnprocessableEntityResponse("Resource monitor could not be started: " + e.Message);
        }

        List<ResourceMonitorDto> storage = StorageMonitor.Main();

        return Ok(new ResourceInfoDto
        {
            Cpu = resource.Cpu,
            Gpu = resource.Gpu,
            Memory = resource.Memory,
            Storage = storage
        });
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
            new()
            {
                Key = "Cache",
                Value = AppFiles.CachePath
            },
            new()
            {
                Key = "Logs",
                Value = AppFiles.LogPath
            },
            new()
            {
                Key = "Transcodes",
                Value = AppFiles.TranscodePath
            },
            new()
            {
                Key = "Configs",
                Value = AppFiles.ConfigPath
            }
        ];

        return Ok(list);
    }

    [HttpGet]
    [Route("/files/${depth:int}/${path:required}")]
    public async Task<IActionResult> Files(string path, int depth)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view files");

        MediaScan mediaScan = new();

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

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return BadRequestResponse("Wallpaper setting is only supported on Windows");

        Image? wallpaper = await context.Images
            .FirstOrDefaultAsync(config => config.FilePath == request.Path);

        if (wallpaper?.FilePath is null)
            return NotFoundResponse("Wallpaper not found");

        string path = Path.Combine(AppFiles.ImagesPath, "original", wallpaper.FilePath.Replace("/", ""));

        string color = GetDominantColor(path);
#pragma warning disable CA1416
        Wallpaper.SilentSet(path, request.Style, request.Color ?? color);
#pragma warning restore CA1416

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Wallpaper set successfully"
        });
    }

    private static string GetDominantColor(string path)
    {
        using Image<Rgb24> image = SixLabors.ImageSharp.Image.Load<Rgb24>(path);
        image.Mutate(x => x
            // Scale the image down preserving the aspect ratio. This will speed up quantization.
            // We use nearest neighbor as it will be the fastest approach.
            .Resize(new ResizeOptions
            {
                Sampler = KnownResamplers.NearestNeighbor,
                Size = new(100, 0)
            })
            // Reduce the color palette to 1 color without dithering.
            .Quantize(new OctreeQuantizer
            {
                Options =
                {
                    MaxColors = 1,
                    Dither = new OrderedDither(1),
                    DitherScale = 1
                }
            }));

        Rgb24 dominant = image[0, 0];

        return dominant.ToHexString();
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

        Networking.Networking.InternalIp = request.Ip;

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = $"IP address changed to {request.Ip}"
        });
    }

    public class ChangeIpRequest
    {
        public string Ip { get; set; } = string.Empty;
    }
}