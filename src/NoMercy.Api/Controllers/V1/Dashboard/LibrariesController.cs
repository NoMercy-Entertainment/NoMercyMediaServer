using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.DTOs.Common;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Helpers.Extensions;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using EncoderProfileDto = NoMercy.Data.Logic.EncoderProfileDto;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Libraries")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/libraries", Order = 10)]
public class LibrariesController(
    LibraryRepository libraryRepository,
    EncoderRepository encoderRepository,
    FolderRepository folderRepository,
    JobDispatcher jobDispatcher,
    LanguageRepository languageRepository
) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        Guid userId = User.UserId();
        
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view libraries");
        
        IEnumerable<Library> libraries = await libraryRepository.GetLibraries(userId);
        
        return Ok(new LibrariesDto
        {
            Data = libraries.Select(library => new LibrariesResponseItemDto(library))
        });
    }

    [HttpPost]
    public async Task<IActionResult> Store()
    {
        Guid userId = User.UserId();
        
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create a new library");
        
        try
        {
            await using MediaContext mediaContext = new();
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
                Type = Config.MovieMediaType,
                Order = 99
            };
            
            await libraryRepository.AddLibraryAsync(library, userId);
            
            return Ok(new StatusResponseDto<Library>
            {
                Status = "ok", 
                Data = library, 
                Message = "Successfully created a new library.", 
                Args = []
            });
        }
        catch (Exception e)
        {
            return Ok(new StatusResponseDto<Library>
            {
                Status = "error", Message = "Something went wrong creating a new library: {0}", Args = [e.Message]
            });
        }
    }

    [HttpPatch]
    [Route("{id:ulid}")]
    public async Task<IActionResult> Update(Ulid id, [FromBody] LibraryUpdateRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update the library");

        Library? library = await libraryRepository.GetLibraryByIdAsync(id);
        if (library is null)
            return NotFound(new StatusResponseDto<string> { Status = "error", Data = "Library not found" });

        try
        {
            // Only update fields that are provided in the request
            if (request.Title != null)
                library.Title = request.Title;
            
            if (request.PerfectSubtitleMatch.HasValue)
                library.PerfectSubtitleMatch = request.PerfectSubtitleMatch.Value;
            
            if (request.Realtime.HasValue)
                library.Realtime = request.Realtime.Value;
            
            if (request.SpecialSeasonName != null)
                library.SpecialSeasonName = request.SpecialSeasonName;
            
            if (request.Type != null)
                library.Type = request.Type;

            // Only update subtitles if provided
            if (request.Subtitles != null)
            {
                library.LanguageLibraries.Clear();
                List<Language> languages = await languageRepository.GetLanguagesAsync();
                foreach (string subtitle in request.Subtitles)
                {
                    Language? language = languages.FirstOrDefault(l => l.Iso6391 == subtitle);
                    if (language is null) continue;
                    library.LanguageLibraries.Add(new()
                    {
                        LibraryId = library.Id, 
                        LanguageId = language.Id
                    });
                }
            }

            await libraryRepository.UpdateLibraryAsync(library);
        }
        catch (Exception e)
        {
            return Ok(new StatusResponseDto<string>
            {
                Status = "error", Message = "Something went wrong updating the library: {0}", Args = [e.Message]
            });
        }

        // Only update folder libraries if provided
        if (request.FolderLibrary != null)
        {
            try
            {
                List<Folder> folders = await folderRepository.GetFoldersByLibraryIdAsync(request.FolderLibrary);
                FolderLibrary[] folderLibraries = folders.Select(folder => new FolderLibrary
                {
                    LibraryId = library.Id, 
                    FolderId = folder.Id
                }).ToArray();
                
                await folderRepository.SyncFolderLibraryAsync(folderLibraries, folders);
            }
            catch (Exception e)
            {
                return UnprocessableEntity(new StatusResponseDto<string>
                {
                    Status = "error",
                    Message = "Something went wrong updating the library folders: {0}",
                    Args = [e.Message]
                });
            }

            try
            {
                List<EncoderProfile> encoderProfiles = await encoderRepository.GetEncoderProfilesAsync();
                List<EncoderProfileFolder> encoderProfileFolders = [];

                List<Folder> folders = await folderRepository.GetFoldersByLibraryIdAsync(request.FolderLibrary);
            
                foreach (FolderLibraryDto folder in request.FolderLibrary)
                {
                    Folder? folderDb = folders.FirstOrDefault(f => f.Id == folder.FolderId);
                    if (folderDb is null) continue;
                    
                    foreach (EncoderProfileDto profile in folder.Folder.EncoderProfiles)
                    {
                        EncoderProfile? encoderProfile = encoderProfiles.FirstOrDefault(ep => ep.Id == profile.Id);
                        if (encoderProfile is null) continue;
                    
                        encoderProfileFolders.Add(new()
                        {
                            FolderId = folderDb.Id, 
                            EncoderProfileId = encoderProfile.Id
                        });
                    }
                }

                await libraryRepository.SyncEncoderProfileFolderAsync(encoderProfileFolders, folders);
            }
            catch (Exception e)
            {
                Logger.App(e);
                return Ok(new StatusResponseDto<string>
                {
                    Status = "error",
                    Message = "Something went wrong updating the library encoder profiles: {0}",
                    Args = [e.Message]
                });
            }
        }

        return Ok(new StatusResponseDto<Library>
        {
            Status = "ok", 
            Message = "Successfully updated {0} library.", 
            Args = [library.Title],
            Data = library
        });
    }

    [HttpDelete]
    [Route("{id:ulid}")]
    public async Task<IActionResult> Delete(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete the library");

        Library? library = await libraryRepository.GetLibraryByIdAsync(id);

        if (library is null)
            return Ok(new StatusResponseDto<string>
            {
                Status = "error", Message = "Library {0} does not exist.", Args = [id.ToString()]
            });
        
        try
        {
            await libraryRepository.DeleteLibraryAsync(library);

            if (EventBusProvider.IsConfigured)
            {
                await EventBusProvider.Current.PublishAsync(new LibraryDeletedEvent
                {
                    LibraryId = library.Id,
                    LibraryName = library.Title
                });

                foreach (FolderLibrary fl in library.FolderLibraries)
                    await EventBusProvider.Current.PublishAsync(new FolderPathRemovedEvent
                    {
                        RequestPath = fl.FolderId
                    });
            }

            return Ok(new StatusResponseDto<string>
            {
                Status = "ok", Message = "Successfully deleted {0} library.", Args = [library.Title]
            });
        }
        catch (Exception e)
        {
            return Ok(new StatusResponseDto<string>
            {
                Status = "error", Message = "Something went wrong deleting the library: {0}",
                Args = [e.InnerException?.Message ?? e.Message]
            });
        }
    }

    [HttpPatch]
    [Route("sort")]
    public async Task<IActionResult> Sort(Ulid id, [FromBody] LibrarySortRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to sort the libraries");

        List<Library> libraries = await libraryRepository.GetAllLibrariesAsync();

        if (libraries.Count == 0)
            return Ok(new StatusResponseDto<string> { Status = "error", Message = "No libraries exist.", Args = [] });

        try
        {
            foreach (LibrarySortRequestItem item in request.Libraries)
            {
                Library? lib = libraries.FirstOrDefault(l => l.Id == item.Id);
                if (lib is null) continue;
                lib.Order = item.Order;
                await libraryRepository.UpdateLibraryAsync(lib);
            }
            
            return Ok(new StatusResponseDto<string>
            {
                Status = "ok", Message = "Successfully sorted libraries.", Args = []
            });
        }
        catch (Exception e)
        {
            return Problem(title: "Internal Server Error.",
                detail: $"Something went wrong sorting the libraries {e.Message}", instance: HttpContext.Request.Path,
                statusCode: StatusCodes.Status403Forbidden, type: "/docs/errors/internal-server-error");
        }
    }

    [HttpPost]
    [Route("rescan")]
    public async Task<IActionResult> Rescan()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to rescan all libraries");

        List<Library> librariesList = await libraryRepository.GetAllLibrariesAsync();

        if (librariesList.Count == 0)
            return NotFound(new StatusResponseDto<List<string?>>
            {
                Status = "error", Message = "No libraries found to rescan.", Args = []
            });

        await using MediaContext mediaContext = new();

        foreach (Library library in librariesList)
        {
            foreach (LibraryMovie movie in library.LibraryMovies)
            {
                jobDispatcher.DispatchJob<FileRescanJob>(movie.MovieId, movie.LibraryId);
            }

            foreach (LibraryTv show in library.LibraryTvs)
            {
                jobDispatcher.DispatchJob<FileRescanJob>(show.TvId, show.LibraryId);
            }
        }

        return Ok(new StatusResponseDto<List<string?>>
        {
            Status = "ok", Message = "Rescanning all libraries."
        });
    }

    [HttpPost]
    [Route("{id:ulid}/rescan")]
    public async Task<IActionResult> Rescan(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to refresh the library");

        Library? library = await libraryRepository.GetLibraryByIdAsync(id);
        
        if (library is null)
            return NotFound(new StatusResponseDto<string> { Status = "error", Data = "Library not found" });

        await using MediaContext mediaContext = new();

        foreach (LibraryMovie movie in library.LibraryMovies)
        {
            jobDispatcher.DispatchJob<FileRescanJob>(movie.MovieId, movie.LibraryId);
        }

        foreach (LibraryTv show in library.LibraryTvs)
        {
            jobDispatcher.DispatchJob<FileRescanJob>(show.TvId, show.LibraryId);
        }

        return Ok(new StatusResponseDto<List<dynamic>>
        {
            Status = "ok", Message = "Rescanning {0} library.", Args = [library.Title]
        });
    }

    [HttpPost]
    [Route("refresh")]
    public async Task<IActionResult> RefreshAll()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to refresh all libraries");

        List<Library> librariesList = await libraryRepository.GetAllLibrariesAsync();

        if (librariesList.Count == 0)
        {
            return NotFound(new StatusResponseDto<List<string?>>
            {
                Status = "error", Message = "No libraries found to refresh.", Args = []
            });
        }

        List<string?> titles = [];
        
        foreach (Library library in librariesList)
        {
            jobDispatcher.DispatchJob<LibraryRescanJob>(library.Id);
        }

        return Ok(new StatusResponseDto<List<string?>>
        {
            Status = "ok", Data = titles, Message = "Rescanning all libraries."
        });
    }

    [HttpPost]
    [Route("{id:ulid}/refresh")]
    public async Task<IActionResult> Refresh(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to refresh the library");

        Library? library = await libraryRepository.GetLibraryByIdAsync(id);
        
        if (library is null)
            return NotFound(new StatusResponseDto<string> { Status = "error", Data = "Library not found" });

        jobDispatcher.DispatchJob<LibraryRescanJob>(id);

        return Ok(new StatusResponseDto<List<dynamic>>
        {
            Status = "ok", Message = "Rescanning {0} library.", Args = [library.Title]
        });
    }

    [HttpPost]
    [Route("scan-new")]
    public async Task<IActionResult> ScanNewAll()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to scan all libraries");

        List<Library> librariesList = await libraryRepository.GetAllLibrariesAsync();

        if (librariesList.Count == 0)
        {
            return NotFound(new StatusResponseDto<List<string?>>
            {
                Status = "error", Message = "No libraries found to scan.", Args = []
            });
        }

        foreach (Library library in librariesList)
        {
            jobDispatcher.DispatchJob<LibraryScanJob>(library.Id);
        }

        return Ok(new StatusResponseDto<List<string?>>
        {
            Status = "ok", Message = "Scanning all libraries for new items."
        });
    }

    [HttpPost]
    [Route("{id:ulid}/scan-new")]
    public async Task<IActionResult> ScanNew(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to scan the library");

        Library? library = await libraryRepository.GetLibraryByIdAsync(id);

        if (library is null)
            return NotFound(new StatusResponseDto<string> { Status = "error", Data = "Library not found" });

        jobDispatcher.DispatchJob<LibraryScanJob>(id);

        return Ok(new StatusResponseDto<List<dynamic>>
        {
            Status = "ok", Message = "Scanning {0} library for new items.", Args = [library.Title]
        });
    }

    [HttpPost]
    [Route("{id:ulid}/folders")]
    public async Task<IActionResult> AddFolder(Ulid id, [FromBody] FolderRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to add a new folder to the library");

        Library? library = await libraryRepository.GetLibraryByIdAsync(id);
        
        if (library is null)
            return NotFoundResponse("Library not found");

        try
        {
            Folder folder = new() { Id = Ulid.NewUlid(), Path = request.Path };
            
            await folderRepository.AddFolderAsync(folder);
            
            if (EventBusProvider.IsConfigured)
            {
                await EventBusProvider.Current.PublishAsync(new FolderPathAddedEvent
                {
                    RequestPath = library.Id,
                    PhysicalPath = folder.Path
                });
            }
        }
        catch (Exception e)
        {
            return UnprocessableEntity(new StatusResponseDto<string>
            {
                Status = "error", Message = "Something went wrong adding the folder: {0}", Args = [e.Message]
            });
        }

        Folder? pathAsync = await folderRepository.GetFolderByPathAsync(request.Path);
        
        if (pathAsync is null)
        {
            return NotFound(new StatusResponseDto<string>
            {
                Status = "error", Message = "Folder {0} does not exist.", Args = [id.ToString()]
            });
        }

        FolderLibrary folderLibrary = new()
        {
            LibraryId = library.Id, 
            FolderId = pathAsync.Id
        };
        
        await folderRepository.AddFolderLibraryAsync(folderLibrary);

        return Ok(new StatusResponseDto<FolderLibrary>
        {
            Status = "ok", 
            Message = "Successfully added folder to {0} library.", 
            Args = [pathAsync.Path],
            Data = folderLibrary
        });
    }

    [HttpPatch]
    [Route("{id:ulid}/folders/{folderId:ulid}")]
    public async Task<IActionResult> UpdateFolder(Ulid id, Ulid folderId, [FromBody] FolderRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update the library folder");

        Folder? folder = await folderRepository.GetFolderByIdAsync(folderId);
        
        if (folder is null)
            return NotFound(new StatusResponseDto<string> { Status = "error", Data = "Folder not found" });

        try
        {
            folder.Path = request.Path;
            await folderRepository.UpdateFolderAsync(folder);
            
            if (EventBusProvider.IsConfigured)
            {
                await EventBusProvider.Current.PublishAsync(new FolderPathRemovedEvent
                {
                    RequestPath = folder.Id
                });
                await EventBusProvider.Current.PublishAsync(new FolderPathAddedEvent
                {
                    RequestPath = id,
                    PhysicalPath = folder.Path
                });
            }
            
            return Ok(new StatusResponseDto<string>
            {
                Status = "ok", Message = "Successfully updated folder {0}.", Args = [folder.Path]
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntity(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "Something went wrong updating the library folder: {0}",
                Args = [e.Message]
            });
        }
    }

    [HttpDelete]
    [Route("{id:ulid}/folders/{folderId:ulid}")]
    public async Task<IActionResult> DeleteFolder(Ulid id, Ulid folderId)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete the library folder");

        Folder? folder = await folderRepository.GetFolderByIdAsync(folderId);
        
        if (folder is null)
            return NotFound(new StatusResponseDto<string> { Status = "error", Data = "Folder not found" });

        try
        {
            await folderRepository.DeleteFolderAsync(folder);
            
            if (EventBusProvider.IsConfigured)
            {
                await EventBusProvider.Current.PublishAsync(new FolderPathRemovedEvent
                {
                    RequestPath = folder.Id
                });
            }

            return Ok(new StatusResponseDto<string>
            {
                Status = "ok", Message = "Successfully deleted folder {0}.", Args = [folder.Path]
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntity(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "Something went wrong deleting the library folder: {0}",
                Args = [e.Message]
            });
        }
    }

    [HttpPost]
    [Route("{id:ulid}/folders/{folderId:ulid}/encoder_profiles")]
    public async Task<IActionResult> AddEncoderProfile(Ulid id, Ulid folderId, [FromBody] ProfilesRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to add a new encoder profile to the folder");

        Folder? folder = await folderRepository.GetFolderByIdAsync(folderId);
        
        if (folder is null)
            return NotFound(new StatusResponseDto<string>
            {
                Status = "error", 
                Data = "Folder not found"
            });

        try
        {
            EncoderProfileFolder[] encoderProfileFolder = request.Profiles.Select(profile =>
                new EncoderProfileFolder
                {
                    FolderId = folder.Id, 
                    EncoderProfileId = Ulid.Parse(profile)
                }).ToArray();

            await libraryRepository.AddEncoderProfileFolderAsync(encoderProfileFolder);
            
            return Ok(new StatusResponseDto<string>
            {
                Status = "ok", 
                Message = "Successfully added encoder profile to {0} folder.", 
                Args = [folder.Path]
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntity(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "Something went wrong adding the encoder profile: {0}",
                Args = [e.Message]
            });
        }
    }

    [HttpDelete]
    [Route("{id:ulid}/folders/{folderId:ulid}/encoder_profiles/{encoderProfileId:ulid}")]
    public async Task<IActionResult> DeleteEncoderProfile(Ulid id, Ulid folderId, Ulid encoderProfileId)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete the encoder profile");

        EncoderProfile? encoderProfile = await encoderRepository.GetEncoderProfileByIdAsync(encoderProfileId);
        
        if (encoderProfile is null)
            return NotFound(new StatusResponseDto<string>
            {
                Status = "error", 
                Data = "Encoder profile not found"
            });

        try
        {
            await encoderRepository.DeleteEncoderProfileAsync(encoderProfile);
            
            return Ok(new StatusResponseDto<string>
            {
                Status = "ok", Message = "Successfully deleted encoder profile {0}.", Args = [encoderProfile.Name]
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntity(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "Something went wrong deleting the encoder profile: {0}",
                Args = [e.Message]
            });
        }
    }

    [HttpPost]
    [Route("move")]
    public async Task<IActionResult> Move([FromBody] MoveRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to move the library");

        Folder? folder = await folderRepository.GetFolderByIdAsync(request.FolderId);
        
        if (folder is null)
            return NotFound(new StatusResponseDto<string>
            {
                Status = "error", 
                Data = "Folder not found"
            });

        try
        {
            await using MediaContext mediaContext = new();

            FileRepository fileRepository = new(mediaContext);
            FileManager fileManager = new(fileRepository);

            await fileManager.MoveToLibraryFolder(request.Id, folder);

            return Ok(new StatusResponseDto<string>
            {
                Status = "ok", 
                Message = "Successfully moved item {0}.", 
                Args = [request.Id.ToString()]
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntity(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "Something went wrong moving the item: {0}",
                Args = [e.Message]
            });
        }
    }
}