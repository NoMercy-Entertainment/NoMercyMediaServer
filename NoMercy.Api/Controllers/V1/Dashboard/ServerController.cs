using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Asp.Versioning;
using FFMpegCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using MovieFileLibrary;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.Dashboard.DTO;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers.Monitoring;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using NoMercy.Queue;
using Serilog.Events;
using AppFiles = NoMercy.NmSystem.AppFiles;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Server Management")]
[ApiVersion("1")]
[Authorize]
[Route("api/v{Version:apiVersion}/dashboard/server", Order = 10)]
public class ServerController : BaseController
{
    private IHostApplicationLifetime ApplicationLifetime { get; }

    public ServerController(IHostApplicationLifetime appLifetime)
    {
        ApplicationLifetime = appLifetime;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to access the dashboard");

        return Ok();
    }

    [HttpGet]
    [Route("setup")]
    public async Task<IActionResult> Setup()
    {
        Guid userId = HttpContext.User.UserId();
        if (!HttpContext.User.IsModerator())
            return Problem(
                title: "Unauthorized.",
                detail: "You do not have permission to access the setup");

        await using MediaContext mediaContext = new();
        List<Library> libraries = await mediaContext.Libraries
            .Include(library => library.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .ThenInclude(folder => folder.EncoderProfileFolder)
            .ThenInclude(encoderProfileFolder => encoderProfileFolder.EncoderProfile)
            .Include(library => library.LibraryUsers
                .Where(x => x.UserId == userId)
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
            Data = new SetupResponseDto
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
        if (!HttpContext.User.IsAllowed())
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
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to stop the server");

        // ApplicationLifetime.StopApplication();
        return Content("Done");
    }

    [HttpPost]
    [Route("restart")]
    public IActionResult RestartServer()
    {
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to restart the server");

        // ApplicationLifetime.StopApplication();
        return Content("Done");
    }

    [HttpPost]
    [Route("shutdown")]
    public IActionResult Shutdown()
    {
        if (!HttpContext.User.IsModerator())
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
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to set the log level");

        Logger.SetLogLevel(level);

        return Content("Log level set to " + level);
    }

    [HttpPost]
    [Route("addfiles")]
    public IActionResult AddFiles([FromBody] AddFilesRequest request)
    {
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to add files");

        return Ok(request);
    }

    [HttpPost]
    [Route("directorytree")]
    public IActionResult DirectoryTree([FromBody] PathRequest request)
    {
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view folders");

        string folder = request.Folder;

        List<DirectoryTreeDto> array = [];

        if (string.IsNullOrEmpty(folder) || folder == "/")
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform
                    .Windows))
            {
                DriveInfo[] driveInfo = DriveInfo.GetDrives();
                array = driveInfo.Select(d => CreateDirectoryTreeDto("", d.RootDirectory.ToString())).ToList();

                return Ok(new StatusResponseDto<List<DirectoryTreeDto>>
                {
                    Status = "ok",
                    Data = array
                });
            }

            folder = "/";
        }

        if (!Directory.Exists(folder))
            return Ok(new StatusResponseDto<List<DirectoryTreeDto>>
            {
                Status = "ok",
                Data = array
            });

        try
        {
            string[] directories = Directory.GetDirectories(folder);
            array = directories.Select(d => CreateDirectoryTreeDto(folder, d)).ToList();
        }
        catch (Exception ex)
        {
            return UnprocessableEntity(new StatusResponseDto<List<DirectoryTreeDto>>
            {
                Status = "error",
                Message = ex.Message
            });
        }

        return Ok(new StatusResponseDto<List<DirectoryTreeDto>>
        {
            Status = "ok",
            Data = array
        });
    }

    [NonAction]
    private DirectoryTreeDto CreateDirectoryTreeDto(string parent, string path)
    {
        string fullPath = Path.Combine(parent, path);

        FileInfo fileInfo = new(fullPath);

        string type = fileInfo.Attributes.HasFlag(FileAttributes.Directory) ? "folder" : "file";

        string newPath = string.IsNullOrEmpty(fileInfo.Name)
            ? path
            : fileInfo.Name;

        string parentPath = string.IsNullOrEmpty(parent)
            ? "/"
            : Path.Combine(fullPath, @"..\..");

        return new DirectoryTreeDto
        {
            Path = newPath,
            Parent = parentPath,
            FullPath = fullPath.Replace(@"..\", ""),
            Mode = (int)fileInfo.Attributes,
            Size = type == "file" ? int.Parse(fileInfo.Length.ToString()) : null,
            Type = type
        };
    }

    [NonAction]
    private static string DeviceName()
    {
        MediaContext mediaContext = new();
        Configuration? device = mediaContext.Configuration.FirstOrDefault(device => device.Key == "server_name");
        return device?.Value ?? Environment.MachineName;
    }

    [HttpPost]
    [Route("filelist")]
    public async Task<IActionResult> FileList([FromBody] FileListRequest request)
    {
        if (!HttpContext.User.IsModerator())
            return Problem(
                title: "Unauthorized.",
                detail: "You do not have permission to view files");

        List<FileItemDto> fileList = await GetFilesInDirectory(request.Folder, request.Type);

        return Ok(new DataResponseDto<FileListResponseDto>
        {
            Data = new FileListResponseDto()
            {
                Status = "ok",
                Files = fileList
            }
        });
    }

    [NonAction]
    private async Task<List<FileItemDto>> GetFilesInDirectory(string directoryPath, string type)
    {
        GlobalFFOptions.Configure(options => options.BinaryFolder = Path.Combine(AppFiles.BinariesPath, "ffmpeg"));

        DirectoryInfo directoryInfo = new(directoryPath);
        FileInfo[] files = directoryInfo.GetFiles();

        List<FileItemDto> fileList = new();

        foreach (FileInfo file in files)
        {
            IMediaAnalysis mediaAnalysis = await FFProbe.AnalyseAsync(file.FullName);

            MovieDetector movieDetector = new();
            MovieFile parsed = movieDetector.GetInfo(Regex.Replace(file.Name, @"\[.*?\]", ""));
            parsed.Year ??= Str.MatchYearRegex().Match(file.FullName)
                .Value;

            MovieOrEpisode match = new();

            TmdbSearchClient searchClient = new();

            await using MediaContext mediaContext = new();

            switch (type)
            {
                case "anime" or "tv":
                {
                    TmdbPaginatedResponse<TmdbTvShow>? shows = await searchClient.TvShow(parsed.Title, parsed.Year);
                    TmdbTvShow? show = shows?.Results.FirstOrDefault();
                    if (show == null || !parsed.Season.HasValue || !parsed.Episode.HasValue) continue;

                    Episode? episode = mediaContext.Episodes
                        .Where(item => item.TvId == show.Id)
                        .Where(item => item.SeasonNumber == parsed.Season)
                        .FirstOrDefault(item => item.EpisodeNumber == parsed.Episode);

                    if (episode == null)
                    {
                        TmdbEpisodeClient episodeClient = new(show.Id, parsed.Season.Value, parsed.Episode.Value);
                        TmdbEpisodeDetails? details = await episodeClient.Details();
                        if (details == null) continue;

                        episode = new Episode
                        {
                            Id = details.Id,
                            TvId = show.Id,
                            SeasonNumber = details.SeasonNumber,
                            EpisodeNumber = details.EpisodeNumber,
                            Title = details.Name,
                            Overview = details.Overview,
                            Still = details.StillPath
                        };
                    }

                    match = new MovieOrEpisode
                    {
                        Id = episode.Id,
                        Title = episode.Title ?? "",
                        EpisodeNumber = episode.EpisodeNumber,
                        SeasonNumber = episode.SeasonNumber,
                        Still = episode.Still,
                        Duration = mediaAnalysis.Duration,
                        Overview = episode.Overview
                    };

                    parsed.ImdbId = episode.ImdbId;
                    break;
                }
                case "movie":
                {
                    TmdbPaginatedResponse<TmdbMovie>? movies = await searchClient.Movie(parsed.Title, parsed.Year);
                    TmdbMovie? movie = movies?.Results.FirstOrDefault();
                    if (movie == null) continue;

                    Movie? movieItem = mediaContext.Movies
                        .FirstOrDefault(item => item.Id == movie.Id);

                    if (movieItem == null)
                    {
                        TmdbMovieClient movieClient = new(movie.Id);
                        TmdbMovieDetails? details = await movieClient.Details();
                        if (details == null) continue;

                        movieItem = new Movie
                        {
                            Id = details.Id,
                            Title = details.Title,
                            Overview = details.Overview,
                            Poster = details.PosterPath
                        };
                    }

                    match = new MovieOrEpisode
                    {
                        Id = movieItem.Id,
                        Title = movieItem.Title,
                        Still = movieItem.Poster,
                        Duration = mediaAnalysis.Duration,
                        Overview = movieItem.Overview
                    };

                    parsed.ImdbId = movieItem.ImdbId;
                    break;
                }
            }

            string? parentPath = string.IsNullOrEmpty(file.DirectoryName)
                ? "/"
                : Path.GetDirectoryName(Path.Combine(file.DirectoryName, ".."));

            fileList.Add(new FileItemDto
            {
                Size = file.Length,
                Mode = (int)file.Attributes,
                Name = Path.GetFileNameWithoutExtension(file.Name),
                Parent = parentPath,
                Parsed = parsed,
                Match = match,
                File = file.FullName,
                Streams = new Streams
                {
                    Video = mediaAnalysis.VideoStreams
                        .Select(video => new Video
                        {
                            Index = video.Index,
                            Width = video.Height,
                            Height = video.Width
                        }),
                    Audio = mediaAnalysis.AudioStreams
                        .Select(stream => new Audio
                        {
                            Index = stream.Index,
                            Language = stream.Language
                        }),
                    Subtitle = mediaAnalysis.SubtitleStreams
                        .Select(stream => new Subtitle
                        {
                            Index = stream.Index,
                            Language = stream.Language
                        })
                }
            });
        }

        return fileList;
    }

    [HttpGet]
    [Route("info")]
    public IActionResult ServerInfo()
    {
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view server information");

        return Ok(new ServerInfoDto
        {
            Server = DeviceName(),
            Cpu = Info.Cpu,
            Gpu = Info.Gpu,
            Os = Info.Platform.ToTitleCase(),
            Arch = Info.Architecture,
            Version = Info.Version,
            BootTime = Info.StartTime
        });
    }

    [HttpGet]
    [Route("resources")]
    public IActionResult Resources()
    {
        if (!HttpContext.User.IsModerator())
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

        if (resource == null)
            return UnprocessableEntityResponse("Resource monitor could not be started");

        return Ok(new ResourceInfoDto
        {
            Cpu = resource.Cpu,
            Gpu = resource.Gpu,
            Memory = resource.Memory,
            Storage = storage
        });
    }

    [HttpPatch]
    [Route("info")]
    public async Task<IActionResult> Update([FromBody] ServerUpdateRequest request)
    {
        Guid userId = HttpContext.User.UserId();
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update server information");

        await using MediaContext mediaContext = new();
        Configuration? configuration = await mediaContext.Configuration
            .AsTracking()
            .FirstOrDefaultAsync(configuration => configuration.Key == "server_name");

        try
        {
            if (configuration == null)
            {
                configuration = new Configuration
                {
                    Key = "server_name",
                    Value = request.Name,
                    ModifiedBy = userId
                };
                await mediaContext.Configuration.AddAsync(configuration);
            }
            else
            {
                configuration.Value = request.Name;
                configuration.ModifiedBy = userId;
            }

            await mediaContext.SaveChangesAsync();

            HttpClient client = new();

            client.BaseAddress = new Uri("https://api-dev.nomercy.tv/v1/");

            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.AccessToken);

            HttpRequestMessage httpRequestMessage = new(HttpMethod.Patch, "server/name")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["id"] = Info.DeviceId.ToString(),
                    ["server_name"] = request.Name
                })
            };

            string response = await client
                .SendAsync(httpRequestMessage)
                .Result.Content.ReadAsStringAsync();

            StatusResponseDto<string>? data = JsonConvert.DeserializeObject<StatusResponseDto<string>>(response);

            if (data == null)
                return UnprocessableEntity(new StatusResponseDto<string>()
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
    [Route("paths")]
    public IActionResult ServerPaths()
    {
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view server paths");

        List<ServerPathsDto> list =
        [
            new ServerPathsDto
            {
                Key = "Cache",
                Value = AppFiles.CachePath
            },
            new ServerPathsDto
            {
                Key = "Logs",
                Value = AppFiles.LogPath
            },
            new ServerPathsDto
            {
                Key = "Metadata",
                Value = AppFiles.MetadataPath
            },
            new ServerPathsDto
            {
                Key = "Transcodes",
                Value = AppFiles.TranscodePath
            },
            new ServerPathsDto
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
        Guid userId = HttpContext.User.UserId();
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view files");

        MediaScan mediaScan = new();

        ConcurrentBag<MediaFolder> folders = await mediaScan
            .EnableFileListing()
            .Process(path, depth);

        await mediaScan.DisposeAsync();

        return Ok();
    }

    [HttpPatch]
    [Route("workers/{worker}/{count:int:min(0)}")]
    public async Task<IActionResult> UpdateWorkers(string worker, int count)
    {
        if (!HttpContext.User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update workers");

        if (await QueueRunner.SetWorkerCount(worker, count))
            return Ok($"{worker} worker count set to {count}");

        return BadRequestResponse($"{worker} worker count could not be set to {count}");
    }
}