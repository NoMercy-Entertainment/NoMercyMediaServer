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
using NoMercy.Data.Jobs;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.Helpers.Monitoring;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using NoMercy.Queue;
using Serilog.Events;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using AppFiles = NoMercy.NmSystem.AppFiles;
using Configuration = NoMercy.Database.Models.Configuration;
using Image = NoMercy.Database.Models.Image;
using VideoDto = NoMercy.Api.Controllers.V1.Dashboard.DTO.VideoDto;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Server Management")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/server", Order = 10)]
public partial class ServerController(IHostApplicationLifetime appLifetime, MediaContext context) : BaseController
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

        await using MediaContext context = new();
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

        foreach (AddFile file in request.Files)
        {
            JobDispatcher.Dispatch(new EncodeVideoJob
            {
                FolderId = request.FolderId,
                Id = file.Id,
                InputFile = file.Path,
            }, "encoder");
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
            array = directories.Select(d => CreateDirectoryTreeDto(folder, d))
                .OrderBy(file => file.Path)
                .ToList();
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

        return new()
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
    private string DeviceName()
    {
        Configuration? device = context.Configuration.FirstOrDefault(device => device.Key == "serverName");
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
            Data = new()
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
        FileInfo[] videoFiles = directoryInfo.GetFiles()
            .Where(file => file.Extension is ".mkv" or ".mp4" or ".avi" or ".webm" or ".flv")
            .ToArray();

        FileInfo[] audioFiles = directoryInfo.GetFiles()
            .Where(file => file.Extension is ".mp3" or ".flac" or ".wav" or ".m4a")
            .ToArray();

        List<FileItemDto> fileList = [];
        if (videoFiles.Length == 0 && audioFiles.Length == 0)
            return fileList;

        if (audioFiles.Length > 0 && videoFiles.Length == 0)
        {
            const string pattern = @"(?<library_folder>.+?)[\\\/]((?<letter>.{1})?|\[(?<type>.+?)\])[\\\/](?<artist>.+?)?[\\\/]?(\[(?<year>\d{4})\]|\[(?<releaseType>Singles)\])\s?(?<album>.*)?";
            Match match = Regex.Match(directoryPath, pattern);

            int year = match.Groups["year"].Success ? Convert.ToInt32(match.Groups["year"].Value) : 0;
            string albumName = match.Groups["album"].Success ? match.Groups["album"].Value : Regex.Replace(directoryInfo.Name, @"\[\d{4}\]\s?", "");
            // string artistName = match.Groups["artist"].Success ? match.Groups["artist"].Value : string.Empty;
            // string releaseType = match.Groups["releaseType"].Success ? match.Groups["releaseType"].Value : string.Empty;
            // string libraryFolder = (match.Groups["library_folder"].Success ? match.Groups["library_folder"].Value : null) ?? Regex.Split(directoryPath, pattern)?[0] ?? string.Empty;

            Parallel.ForEach(audioFiles, (file) =>
            {
                fileList.Add(new()
                {
                    Size = file.Length,
                    Mode = (int)file.Attributes,
                    Name = Path.Combine(directoryPath, file.Name),
                    Parent = directoryPath,
                    Parsed = new(directoryPath)
                    {
                        Title = albumName + " - " + Path.GetFileNameWithoutExtension(file.Name),
                        Year = year.ToString(),
                        IsSeries = false,
                        IsSuccess = true
                    },
                    Match = new()
                    {
                        Title = albumName,
                    },
                    File = Path.Combine(directoryPath, file.FullName)
                });
            });
        }
        else if (videoFiles.Length > 0)
        {
            foreach (FileInfo file in videoFiles)
            {
                try
                {
                    MovieOrEpisodeDto match = new();
                    TmdbSearchClient searchClient = new();
                    MovieDetector movieDetector = new();
                    
                    string title = file.FullName.Replace("v2", "");
                    // remove any text in square brackets that may cause year to match incorrectly
                    title = RemoveBracketedString().Replace(title, string.Empty);
                    
                    IMediaAnalysis mediaAnalysis = await FFProbe.AnalyseAsync(file.FullName);

                    MovieFile parsed = movieDetector.GetInfo(title);
                    
                    parsed.Year ??= Str.MatchYearRegex()
                        .Match(title)
                        .Value;


                    if (parsed.Title == null) continue;

                    Regex regex = MatchNumbers();
                    Match match2 = regex.Match(parsed.Title);

                    if (match2.Success)
                    {
                        parsed.Season = 1;
                        parsed.Episode = int.Parse(match2.Value);

                        parsed.Title = regex.Split(parsed.Title).FirstOrDefault();
                    }

                    switch (type)
                    {
                        case "anime" or "tv":
                        {
                            TmdbPaginatedResponse<TmdbTvShow>? shows =
                                await searchClient.TvShow(parsed.Title ?? "", parsed.Year);
                            TmdbTvShow? show = shows?.Results.FirstOrDefault();
                            if (show == null || !parsed.Season.HasValue || !parsed.Episode.HasValue) continue;

                            bool hasShow = context.Tvs
                                .Any(item => item.Id == show.Id);

                            Ulid libraryId = await context.Libraries
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

                            Episode? episode = context.Episodes
                                .Where(item => item.TvId == show.Id)
                                .Where(item => item.SeasonNumber == parsed.Season)
                                .FirstOrDefault(item => item.EpisodeNumber == parsed.Episode);

                            if (episode == null)
                            {
                                TmdbEpisodeClient episodeClient =
                                    new(show.Id, parsed.Season.Value, parsed.Episode.Value);
                                TmdbEpisodeDetails? details = await episodeClient.Details();
                                if (details == null) continue;

                                Season? season = await context.Seasons
                                    .FirstOrDefaultAsync(season => season.TvId == show.Id && season.SeasonNumber == details.SeasonNumber);

                                episode = new()
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

                                context.Episodes.Add(episode);
                                await context.SaveChangesAsync();
                            }

                            match = new()
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
                            TmdbPaginatedResponse<TmdbMovie>? movies =
                                await searchClient.Movie(parsed.Title ?? "", parsed.Year);
                            TmdbMovie? movie = movies?.Results.FirstOrDefault();
                            if (movie == null) continue;

                            Movie? movieItem = context.Movies
                                .FirstOrDefault(item => item.Id == movie.Id);

                            if (movieItem == null)
                            {
                                TmdbMovieClient movieClient = new(movie.Id);
                                TmdbMovieDetails? details = await movieClient.Details();
                                if (details == null) continue;

                                bool hasMovie = context.Movies
                                    .Any(item => item.Id == movie.Id);

                                Ulid libraryId = await context.Libraries
                                    .Where(item => item.Type == type)
                                    .Select(item => item.Id)
                                    .FirstOrDefaultAsync();

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

                                movieItem = new()
                                {
                                    Id = details.Id,
                                    Title = details.Title,
                                    Overview = details.Overview,
                                    Poster = details.PosterPath
                                };
                            }

                            match = new()
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

                    fileList.Add(new()
                    {
                        Size = file.Length,
                        Mode = (int)file.Attributes,
                        Name = Path.GetFileNameWithoutExtension(file.Name),
                        Parent = parentPath,
                        Parsed = parsed,
                        Match = match,
                        File = file.FullName,
                        Streams = new()
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
                } catch (Exception e)
                {
                    Logger.App(e.Message, LogEventLevel.Error);
                }
            }
        }

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

        await using MediaContext context = new();
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
            client.DefaultRequestHeaders.Add("User-Agent", ApiInfo.UserAgent);
            client.DefaultRequestHeaders.Authorization = new("Bearer", Auth.AccessToken);

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
                Key = "Metadata",
                Value = AppFiles.MetadataPath
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

        if (await QueueRunner.SetWorkerCount(worker, count, User.UserId()))
            return Ok($"{worker} worker count set to {count}");

        return BadRequestResponse($"{worker} worker count could not be set to {count}");
    }
    
    [HttpGet]
    [Route("storage")]
    public IActionResult Storage()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view server paths");
        
        StorageJob storageJob = new(StorageMonitor.Storage);
        JobDispatcher.Dispatch(storageJob, "data", 1000);
        
        return Ok(StorageMonitor.Storage);
    }
    
    [HttpPost]
    [Route("wallpaper")]
    public async Task<IActionResult> SetWallpaper([FromBody] WallpaperRequest request)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to set wallpaper");
        
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return BadRequestResponse("Wallpaper setting is only supported on Windows");
        }

        await using MediaContext mediaContext = new();
        Image? wallpaper = await mediaContext.Images
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
        image.Mutate(
            x => x
                // Scale the image down preserving the aspect ratio. This will speed up quantization.
                // We use nearest neighbor as it will be the fastest approach.
                .Resize(new ResizeOptions()
                {
                    Sampler = KnownResamplers.NearestNeighbor,
                    Size = new(100, 0)
                })
                // Reduce the color palette to 1 color without dithering.
                .Quantize(new OctreeQuantizer()
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

    [GeneratedRegex(@"\[.*?\]")]
    private static partial Regex RemoveBracketedString();
    [GeneratedRegex(@"\d+")]
    private static partial Regex MatchNumbers();
}
