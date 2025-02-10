using System.ComponentModel.DataAnnotations.Schema;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Data.Logic;
using NoMercy.Data.Logic.Seeds;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Images;
using NoMercy.Networking;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags(tags: "Dashboard Specials")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/specials", Order = 11)]
public class SpecialsController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view specials");

        await using MediaContext mediaContext = new();
        List<Special> specials = await mediaContext.Specials
            .AsNoTracking()
            .ToListAsync();

        return Ok(new SpecialsResponseDto
        {
            Data = specials.Select(special => new SpecialsResponseItemDto(special))
        });
    }

    [HttpPost]
    public async Task<IActionResult> Store()
    {
        Guid userId = User.UserId();
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create a new special");

        try
        {
            await using MediaContext mediaContext = new();
            int specials = await mediaContext.Specials.CountAsync();

            Special special = new()
            {
                Id = Ulid.NewUlid(),
                Title = $"special {specials}"
            };

            await mediaContext.Specials.Upsert(special)
                .On(l => new { l.Id })
                .WhenMatched((ls, li) => new()
                {
                    Id = li.Id,
                    Title = li.Title,
                    UpdatedAt = li.UpdatedAt
                })
                .RunAsync();

            await mediaContext.SpecialUser.Upsert(new()
                {
                    SpecialId = special.Id,
                    UserId = userId
                })
                .On(lu => new { lu.SpecialId, lu.UserId })
                .WhenMatched((lus, lui) => new()
                {
                    SpecialId = lui.SpecialId,
                    UserId = lui.UserId,
                })
                .RunAsync();

            return Ok(new StatusResponseDto<Special>
            {
                Status = "ok",
                Data = special,
                Message = "Successfully created a new special.",
                Args = []
            });
        }
        catch (Exception e)
        {
            return Ok(new StatusResponseDto<Special>
            {
                Status = "error",
                Message = "Something went wrong creating a new special: {0}",
                Args = [e.Message]
            });
        }
    }

    [HttpPatch]
    [Route("{id:ulid}")]
    public async Task<IActionResult> Update(Ulid id, [FromBody] SpecialUpdateRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update the special");

        await using MediaContext mediaContext = new();
        Special? special = await mediaContext.Specials
            .Where(special => special.Id == id)
            .FirstOrDefaultAsync();

        if (special is null)
            return Ok(new StatusResponseDto<string>()
            {
                Status = "error",
                Message = "Library {0} does not exist.",
                Args = [id.ToString()]
            });

        try
        {
            if ((request.Poster is not null && special.Poster != request.Poster) 
                || (request.Backdrop is not null && special.Backdrop != request.Backdrop) 
                || (request.Logo is not null && special.Logo != request.Logo))
            {
                special.Poster = request.Poster;
                
                special._colorPalette = await MovieDbImageManager
                    .MultiColorPalette([
                        new("poster", request.Poster),
                        new("backdrop", request.Backdrop),
                        new("logo", request.Logo)
                    ]);
            }
            
            if (request.Title is not null)
                special.Title = request.Title;
            
            if (request.Overview is not null)
                special.Overview = request.Overview;
            
            if (request.Poster is not null)
                special.Poster = request.Poster;
            
            if (request.Backdrop is not null)
                special.Backdrop = request.Backdrop;
            
            if (request.Logo is not null)
                special.Logo = request.Logo;
            
            await mediaContext.SaveChangesAsync();

        }
        catch (Exception e)
        {
            return Ok(new StatusResponseDto<string>()
            {
                Status = "error",
                Message = "Something went wrong updating the special: {0}",
                Args = [e.Message]
            });
        }

        return Ok(new StatusResponseDto<string>()
        {
            Status = "ok",
            Message = "Successfully updated {0} special.",
            Args = [special.Title]
        });
    }

    [HttpDelete]
    [Route("{id:ulid}")]
    public async Task<IActionResult> Delete(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete the special");

        try
        {
            await using MediaContext mediaContext = new();
            Special? special = await mediaContext.Specials.FindAsync(keyValues: id);

            if (special is null)
                return Ok(new StatusResponseDto<string>()
                {
                    Status = "error",
                    Message = "Library {0} does not exist.",
                    Args = [id.ToString()]
                });

            mediaContext.Specials.Remove(special);
            await mediaContext.SaveChangesAsync();

            return Ok(new StatusResponseDto<string>()
            {
                Status = "ok",
                Message = "Successfully deleted {0} special.",
                Args = [special.Title]
            });
        }
        catch (Exception e)
        {
            return Ok(new StatusResponseDto<string>()
            {
                Status = "error",
                Message = "Something went wrong deleting the special: {0}",
                Args = [e.Message]
            });
        }
    }

    [HttpPatch]
    [Route("sort")]
    public async Task<IActionResult> Sort(Ulid id, [FromBody] LibrarySortRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to sort the specials");

        await using MediaContext mediaContext = new();
        List<Special> specials = await mediaContext.Specials
            .AsTracking()
            .ToListAsync();

        if (specials.Count == 0)
            return Ok(new StatusResponseDto<string>()
            {
                Status = "error",
                Message = "No specials exist.",
                Args = []
            });

        return Ok(new StatusResponseDto<string>()
        {
            Status = "ok",
            Message = "Successfully sorted specials.",
            Args = []
        });
    }

    [HttpPost]
    [Route("rescan")]
    public async Task<IActionResult> RescanAll()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to rescan all specials");

        await using MediaContext mediaContext = new();
        List<Special> specialsList = await mediaContext.Specials
            .ToListAsync();

        if (specialsList.Count == 0)
            return NotFound(new StatusResponseDto<List<string?>>()
            {
                Status = "error",
                Message = "No specials exist."
            });

        // const int depth = 1;

        List<string?> titles = new();

        // foreach (var special in specialsList)
        // {
        //     LibraryLogic specialLogic = new(special.Id);
        //     await specialLogic.Process();
        //
        //     List<MediaFolder> folders = new();
        //     MediaScan mediaScan = new();
        //
        //     string[] paths = special.Items
        //         .Select(folderLibrary => folderLibrary.Folder.Path)
        //         .ToArray();
        //
        //     foreach (var path in paths)
        //     {
        //         var list = await mediaScan
        //             .Process(path, 2);
        //
        //         folders.AddRange(list);
        //     }
        //
        //     await mediaScan.DisposeAsync();
        //
        //     foreach (var folder in folders)
        //     {
        //         if (folder.Parsed is null) continue;
        //
        //         switch (special.Type)
        //         {
        //             case "movie":
        //             {
        //                 SearchClient searchClient = new();
        //
        //                 var paginatedMovieResponse = await searchClient.Movie(folder.Parsed.Title!, folder.Parsed.Year!);
        //
        //                 if (paginatedMovieResponse?.Results.Length <= 0) continue;
        //
        //                 // List<Movie> res = Str.SortByMatchPercentage(paginatedMovieResponse.Results, m => m.Title, folder.Parsed.Title);
        //                 List<Movie> res = paginatedMovieResponse?.Results.ToList() ?? [];
        //                 if (res.Count is 0) continue;
        //
        //                 titles.Add(res[0].Title);
        //
        //                 AddMovieJob addMovieJob = new AddMovieJob(id:res[0].Id, specialId:special.Id.ToString());
        //                 JobDispatcher.Dispatch(addMovieJob, "queue", 5);
        //                 break;
        //             }
        //             case "tv":
        //             {
        //                 SearchClient searchClient = new();
        //
        //                 var paginatedTvShowResponse = await searchClient.TvShow(folder.Parsed.Title!, folder.Parsed.Year!);
        //
        //                 if (paginatedTvShowResponse?.Results.Length <= 0) continue;
        //
        //                 // List<TvShow> res = Str.SortByMatchPercentage(paginatedTvShowResponse.Results, m => m.Name, folder.Parsed.Title);
        //                 List<TvShow> res = paginatedTvShowResponse?.Results.ToList() ?? [];
        //                 if (res.Count is 0) continue;
        //
        //                 titles.Add(res[0].Name);
        //
        //                 AddShowJob addShowJob = new AddShowJob(id:res[0].Id, specialId:special.Id.ToString());
        //                 JobDispatcher.Dispatch(addShowJob, "queue", 5);
        //                 break;
        //             }
        //             case "music":
        //             {
        //                 Logger.App(folders);
        //                 Logger.App("Music special rescan not implemented.");
        //                 break;
        //             }
        //         }
        //     }
        // }

        return Ok(new StatusResponseDto<List<string?>>()
        {
            Status = "ok",
            Data = titles,
            Message = "Rescanning all specials."
        });
    }

    [HttpPost]
    [Route("{id:ulid}/rescan")]
    public async Task<IActionResult> Rescan(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to rescan the special");

        LibraryLogic specialLogic = new(id);

        if (await specialLogic.Process())
            return Ok(new StatusResponseDto<List<dynamic>>()
            {
                Status = "ok",
                Data = specialLogic.Titles,
                Message = "Rescanning {0} special.",
                Args = [specialLogic.Id]
            });

        return NotFound(new StatusResponseDto<List<dynamic>>()
        {
            Status = "error",
            Message = "Library {0} does not exist.",
            Args = [id]
        });
    }

    [HttpPost]
    [Route("addmarvel")]
    public IActionResult AddMarvel()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to rescan all specials");

        Thread thread = new(() =>
        {
            using MediaContext mediaContext = new();
            SpecialSeed.AddSpecial(mediaContext).Wait();
        });
        thread.Start();

        return Ok(new StatusResponseDto<string>()
        {
            Status = "ok",
            Message = "Rescanning all specials."
        });
    }

    [NotMapped]
    public class SpecialUpdateRequest
    {
        [JsonProperty("id")] public Ulid Id { get; set; }
        [JsonProperty("title")] public string? Title { get; set; }
        [JsonProperty("overview")] public string? Overview { get; set; }
        [JsonProperty("poster")] public string? Poster { get; set; }
        [JsonProperty("backdrop")] public string? Backdrop { get; set; }
        [JsonProperty("logo")] public string? Logo { get; set; }
        
    }
}
