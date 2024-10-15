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
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
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
using JobDispatcher = NoMercy.MediaProcessing.Jobs.JobDispatcher;
using VideoDto = NoMercy.Api.Controllers.V1.Dashboard.DTO.VideoDto;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Server Management")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/server", Order = 10)]
public class ServerController(IHostApplicationLifetime appLifetime) : BaseController
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

        // ApplicationLifetime.StopApplication();
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

        await using MediaContext mediaContext = new();
        JobDispatcher jobDispatcher = new();

        foreach (AddFile file in request.Files)
        {
            jobDispatcher.DispatchJob(new EncodeVideoJob
            {
                FolderId = request.FolderId,
                Id = file.Id,
                InputFile = file.Path,
            });
        }

        return Ok(request);
    }

    [HttpPost]
    [Route("directorytree")]
    public IActionResult DirectoryTree([FromBody] PathRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view folders");

        string folder = request.Folder;

        List<DirectoryTreeDto> array = [];

        if (string.IsNullOrEmpty(folder) || folder == "/")
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform
                    .Windows))
            {
                DriveInfo[] driveInfo = DriveInfo.GetDrives();
                array = driveInfo.Select(d => CreateDirectoryTreeDto("", d.RootDirectory.ToString()))
                    .OrderBy(file => file.Path)
                    .ToList();

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
        Configuration? device = mediaContext.Configuration.FirstOrDefault(device => device.Key == "serverName");
        return device?.Value ?? Environment.MachineName;
    }

    [HttpPost]
    [Route("filelist")]
    public async Task<IActionResult> FileList([FromBody] FileListRequest request)
    {
        if (!User.IsModerator())
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
                    .OrderBy(file => file.File)
                    .ToList()
            }
        });
    }

    [NonAction]
    private async Task<List<FileItemDto>> GetFilesInDirectory(string directoryPath, string type)
    {
        GlobalFFOptions.Configure(options => options.BinaryFolder = Path.Combine(AppFiles.BinariesPath, "ffmpeg"));

        DirectoryInfo directoryInfo = new(directoryPath);
        FileInfo[] files = directoryInfo.GetFiles()
            .Where(file => file.Extension == ".mkv" || file.Extension == ".mp4" || file.Extension == ".avi" || file.Extension == ".webm" || file.Extension == ".flv")
            .ToArray();

        List<FileItemDto> fileList = new();

        await Parallel.ForEachAsync(files, async (file, t) =>
        {
            IMediaAnalysis mediaAnalysis = await FFProbe.AnalyseAsync(file.FullName, cancellationToken: t);

            MovieDetector movieDetector = new();
            MovieFile parsed = movieDetector.GetInfo(Regex.Replace(file.FullName, @"\[.*?\]", ""));
            parsed.Year ??= Str.MatchYearRegex().Match(file.FullName)
                .Value;

            MovieOrEpisodeDto match = new();

            TmdbSearchClient searchClient = new();

            await using MediaContext mediaContext = new();

            switch (type)
            {
                case "anime" or "tv":
                {
                    TmdbPaginatedResponse<TmdbTvShow>? shows = await searchClient.TvShow(parsed.Title ?? "", parsed.Year);
                    TmdbTvShow? show = shows?.Results.FirstOrDefault();
                    if (show == null || !parsed.Season.HasValue || !parsed.Episode.HasValue) return;

                    bool hasShow = mediaContext.Tvs
                        .Any(item => item.Id == show.Id);

                    Ulid libraryId = await mediaContext.Libraries
                        .Where(item => item.Type == type)
                        .Select(item => item.Id)
                        .FirstOrDefaultAsync();

                    if (!hasShow)
                    {
                        Networking.Networking.SendToAll("Notify", "socket", new NotifyDto
                        {
                            Title = "Show not found",
                            Message = $"Show {show.Name} not found in library, adding now",
                            Type = "info"
                        });
                        AddShowJob job = new()
                        {
                            LibraryId = libraryId,
                            Id = show.Id
                        };
                        await job.Handle();
                    }

                    Episode? episode = mediaContext.Episodes
                        .Where(item => item.TvId == show.Id)
                        .Where(item => item.SeasonNumber == parsed.Season)
                        .FirstOrDefault(item => item.EpisodeNumber == parsed.Episode);

                    if (episode == null)
                    {
                        TmdbEpisodeClient episodeClient = new(show.Id, parsed.Season.Value, parsed.Episode.Value);
                        TmdbEpisodeDetails? details = await episodeClient.Details();
                        if (details == null) return;

                        Season? season = await mediaContext.Seasons
                            .FirstOrDefaultAsync(season => season!.TvId == show.Id && season.SeasonNumber == details.SeasonNumber, cancellationToken: t);

                        episode = new Episode
                        {
                            Id = details.Id,
                            TvId = show.Id,
                            SeasonNumber = details.SeasonNumber,
                            EpisodeNumber = details.EpisodeNumber,
                            Title = details.Name,
                            Overview = details.Overview,
                            Still = details.StillPath,
                            VoteAverage = details.VoteAverage,
                            VoteCount = details.VoteCount,
                            AirDate = details.AirDate,
                            SeasonId = season?.Id ?? 0,
                            _colorPalette = await MovieDbImageManager.ColorPalette("still", details.StillPath)
                        };

                        mediaContext.Episodes.Add(episode);
                        await mediaContext.SaveChangesAsync(t);
                    }

                    match = new MovieOrEpisodeDto
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
                    TmdbPaginatedResponse<TmdbMovie>? movies = await searchClient.Movie(parsed.Title ?? "", parsed.Year);
                    TmdbMovie? movie = movies?.Results.FirstOrDefault();
                    if (movie == null) return;

                    Movie? movieItem = mediaContext.Movies
                        .FirstOrDefault(item => item.Id == movie.Id);

                    if (movieItem == null)
                    {
                        TmdbMovieClient movieClient = new(movie.Id);
                        TmdbMovieDetails? details = await movieClient.Details();
                        if (details == null) return;

                        bool hasMovie = mediaContext.Movies
                            .Any(item => item.Id == movie.Id);

                        Ulid libraryId = await mediaContext.Libraries
                            .Where(item => item.Type == type)
                            .Select(item => item.Id)
                            .FirstOrDefaultAsync(cancellationToken: t);

                        if (!hasMovie)
                        {
                            Networking.Networking.SendToAll("Notify", "socket", new NotifyDto
                            {
                                Title = "Movie not found",
                                Message = $"Movie {movie.Title} not found in library, adding now",
                                Type = "info"
                            });
                            AddMovieJob job = new()
                            {
                                LibraryId = libraryId,
                                Id = movie.Id
                            };
                            await job.Handle();
                        }

                        movieItem = new Movie
                        {
                            Id = details.Id,
                            Title = details.Title,
                            Overview = details.Overview,
                            Poster = details.PosterPath
                        };
                    }

                    match = new MovieOrEpisodeDto
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
                StreamsDto = new StreamsDto
                {
                    Video = mediaAnalysis.VideoStreams
                        .Select(video => new VideoDto
                        {
                            Index = video.Index,
                            Width = video.Height,
                            Height = video.Width
                        }),
                    Audio = mediaAnalysis.AudioStreams
                        .Select(stream => new AudioDto
                        {
                            Index = stream.Index,
                            Language = stream.Language
                        }),
                    Subtitle = mediaAnalysis.SubtitleStreams
                        .Select(stream => new SubtitleDto
                        {
                            Index = stream.Index,
                            Language = stream.Language
                        })
                }
            });
        });

        return fileList.OrderBy(file => file.Name).ToList();
    }

    [HttpGet]
    [Route("info")]
    public IActionResult ServerInfo()
    {
        if (!User.IsModerator())
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

    [HttpPatch]
    [Route("info")]
    public async Task<IActionResult> Update([FromBody] ServerUpdateRequest request)
    {
        Guid userId = User.UserId();
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update server information");

        await using MediaContext mediaContext = new();
        Configuration? configuration = await mediaContext.Configuration
            .AsTracking()
            .FirstOrDefaultAsync(configuration => configuration.Key == "serverName");

        try
        {
            if (configuration == null)
            {
                configuration = new Configuration
                {
                    Key = "serverName",
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
            client.BaseAddress = new Uri(Config.ApiServerBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", ApiInfo.UserAgent);
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
        if (!User.IsModerator())
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
        Guid userId = User.UserId();
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view files");

        MediaScan mediaScan = new();

        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .Process(path, depth);

        await mediaScan.DisposeAsync();

        return Ok();
    }

    [HttpPatch]
    [Route("workers/{worker}/{count:int:min(0)}")]
    public async Task<IActionResult> UpdateWorkers(string worker, int count)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update workers");

        if (await QueueRunner.SetWorkerCount(worker, count, User.UserId()))
            return Ok($"{worker} worker count set to {count}");

        return BadRequestResponse($"{worker} worker count could not be set to {count}");
    }
}