using System.ComponentModel.DataAnnotations.Schema;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Data.Logic;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Helpers.Extensions;
using NoMercy.MediaProcessing.Images;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags(tags: "Dashboard Specials")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/specials", Order = 11)]
public class SpecialsController(MediaContext mediaContext, IDbContextFactory<MediaContext> contextFactory) : BaseController
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
                    Title = li.Title
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
                    UserId = lui.UserId
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

    [HttpGet]
    [Route("{id:ulid}")]
    public async Task<IActionResult> Show(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view the special");

        Special? special = await mediaContext.Specials
            .Where(special => special.Id == id)
            .FirstOrDefaultAsync();

        if (special is null)
            return Ok(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "Library {0} does not exist.",
                Args = [id.ToString()]
            });

        return Ok(new StatusResponseDto<Special>
        {
            Status = "ok",
            Data = special,
            Message = "Successfully retrieved {0} special.",
            Args = [special.Title]
        });
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
            return Ok(new StatusResponseDto<string>
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
            return Ok(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "Something went wrong updating the special: {0}",
                Args = [e.Message]
            });
        }

        return Ok(new StatusResponseDto<string>
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
                return Ok(new StatusResponseDto<string>
                {
                    Status = "error",
                    Message = "Library {0} does not exist.",
                    Args = [id.ToString()]
                });

            mediaContext.Specials.Remove(special);
            await mediaContext.SaveChangesAsync();

            return Ok(new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "Successfully deleted {0} special.",
                Args = [special.Title]
            });
        }
        catch (Exception e)
        {
            return Ok(new StatusResponseDto<string>
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
            return Ok(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "No specials exist.",
                Args = []
            });

        return Ok(new StatusResponseDto<string>
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
            return NotFound(new StatusResponseDto<List<string?>>
            {
                Status = "error",
                Message = "No specials exist."
            });

        // const int depth = 1;

        List<string?> titles = [];

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
        //             case Config.MovieMediaType:
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
        //                 MovieImportJob addMovieJob = new MovieImportJob(id:res[0].Id, specialId:special.Id.ToString());
        //                 JobDispatcher.Dispatch(addMovieJob, "queue", 5);
        //                 break;
        //             }
        //             case Config.TvMediaType:
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
        //                 ShowImportJob addShowJob = new ShowImportJob(id:res[0].Id, specialId:special.Id.ToString());
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

        return Ok(new StatusResponseDto<List<string?>>
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

        LibraryLogic specialLogic = new(id, mediaContext);

        if (await specialLogic.Process())
            return Ok(new StatusResponseDto<List<dynamic>>
            {
                Status = "ok",
                Data = specialLogic.Titles,
                Message = "Rescanning {0} special.",
                Args = [specialLogic.Id]
            });

        return NotFound(new StatusResponseDto<List<dynamic>>
        {
            Status = "error",
            Message = "Library {0} does not exist.",
            Args = [id]
        });
    }

    [HttpGet]
    [Route("{id:ulid}/items")]
    public async Task<IActionResult> GetItems(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view special items");

        await using MediaContext ctx = new();
        List<SpecialItem> items = await ctx.SpecialItems
            .AsNoTracking()
            .Where(si => si.SpecialId == id)
            .Include(si => si.Movie)
                .ThenInclude(m => m!.VideoFiles)
            .Include(si => si.Episode)
                .ThenInclude(e => e!.Tv)
            .Include(si => si.Episode)
                .ThenInclude(e => e!.VideoFiles)
            .OrderBy(si => si.Order)
            .ToListAsync();

        List<SpecialItemResponseDto> result = items
            .Select(si =>
            {
                if (si.MovieId is not null && si.Movie is not null)
                    return new SpecialItemResponseDto
                    {
                        Id = si.Id.ToString(),
                        Order = si.Order,
                        MediaType = "movie",
                        MediaId = si.Movie.Id,
                        Title = si.Movie.Title,
                        Overview = si.Movie.Overview,
                        Still = null,
                        Poster = si.Movie.Poster,
                        Year = si.Movie.ReleaseDate?.Year,
                        ShowTitle = null,
                        SeasonNumber = null,
                        EpisodeNumber = null,
                        Available = si.Movie.VideoFiles.Count > 0
                    };

                if (si.EpisodeId is not null && si.Episode is not null)
                    return new SpecialItemResponseDto
                    {
                        Id = si.Id.ToString(),
                        Order = si.Order,
                        MediaType = "episode",
                        MediaId = si.Episode.Id,
                        Title = si.Episode.Title ?? "",
                        Overview = si.Episode.Overview,
                        Still = si.Episode.Still,
                        Poster = null,
                        Year = si.Episode.AirDate?.Year,
                        ShowTitle = si.Episode.Tv?.Title,
                        SeasonNumber = si.Episode.SeasonNumber,
                        EpisodeNumber = si.Episode.EpisodeNumber,
                        Available = si.Episode.VideoFiles.Count > 0
                    };

                return null;
            })
            .Where(x => x is not null)
            .ToList()!;

        return Ok(result);
    }

    [HttpPatch]
    [Route("{id:ulid}/items")]
    public async Task<IActionResult> UpdateItems(Ulid id, [FromBody] SpecialItemsUpdateRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update special items");

        await using MediaContext ctx = new();
        Special? special = await ctx.Specials
            .Where(s => s.Id == id)
            .FirstOrDefaultAsync();

        if (special is null)
            return NotFoundResponse($"Special {id} does not exist.");

        List<SpecialItem> existingItems = await ctx.SpecialItems
            .Where(si => si.SpecialId == id)
            .ToListAsync();

        ctx.SpecialItems.RemoveRange(existingItems);

        List<SpecialItem> newItems = request.Items.Select(item => new SpecialItem
        {
            SpecialId = id,
            Order = item.Order,
            MovieId = item.MediaType == "movie" ? item.MediaId : null,
            EpisodeId = item.MediaType == "episode" ? item.MediaId : null
        }).ToList();

        await ctx.SpecialItems.AddRangeAsync(newItems);
        await ctx.SaveChangesAsync();

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Successfully updated special items."
        });
    }

    [HttpGet]
    [Route("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct = default)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to search");

        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<SpecialSearchResultDto>());

        string normalizedQuery = q.ToLower();

        Task<List<SpecialSearchResultDto>> moviesTask = Task.Run(async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(ct);
            List<Movie> movies = await ctx.Movies
                .AsNoTracking()
                .Where(m => m.Title.ToLower().Contains(normalizedQuery))
                .Include(m => m.VideoFiles)
                .Take(25)
                .ToListAsync(ct);

            return movies.Select(m => new SpecialSearchResultDto
            {
                Id = m.Id,
                MediaType = "movie",
                Title = m.Title,
                Overview = m.Overview,
                Still = null,
                Poster = m.Poster,
                Year = m.ReleaseDate?.Year,
                ShowTitle = null,
                SeasonNumber = null,
                EpisodeNumber = null,
                Available = m.VideoFiles.Count > 0
            }).ToList();
        });

        Task<List<SpecialSearchResultDto>> episodesTask = Task.Run(async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(ct);
            List<Episode> episodes = await ctx.Episodes
                .AsNoTracking()
                .Where(e => (e.Title != null && e.Title.ToLower().Contains(normalizedQuery))
                         || e.Tv.Title.ToLower().Contains(normalizedQuery))
                .Include(e => e.Tv)
                .Include(e => e.VideoFiles)
                .OrderBy(e => e.Tv.Title)
                .ThenBy(e => e.SeasonNumber)
                .ThenBy(e => e.EpisodeNumber)
                .Take(25)
                .ToListAsync(ct);

            return episodes.Select(e => new SpecialSearchResultDto
            {
                Id = e.Id,
                MediaType = "episode",
                Title = e.Title ?? "",
                Overview = e.Overview,
                Still = e.Still,
                Poster = null,
                Year = e.AirDate?.Year,
                ShowTitle = e.Tv?.Title,
                SeasonNumber = e.SeasonNumber,
                EpisodeNumber = e.EpisodeNumber,
                Available = e.VideoFiles.Count > 0
            }).ToList();
        });

        await Task.WhenAll(moviesTask, episodesTask);

        List<SpecialSearchResultDto> results = [..await moviesTask, ..await episodesTask];

        return Ok(results);
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

    [NotMapped]
    public class SpecialItemResponseDto
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("order")] public int Order { get; set; }
        [JsonProperty("media_type")] public string MediaType { get; set; } = string.Empty;
        [JsonProperty("media_id")] public int MediaId { get; set; }
        [JsonProperty("title")] public string Title { get; set; } = string.Empty;
        [JsonProperty("overview")] public string? Overview { get; set; }
        [JsonProperty("still")] public string? Still { get; set; }
        [JsonProperty("poster")] public string? Poster { get; set; }
        [JsonProperty("year")] public int? Year { get; set; }
        [JsonProperty("show_title")] public string? ShowTitle { get; set; }
        [JsonProperty("season_number")] public int? SeasonNumber { get; set; }
        [JsonProperty("episode_number")] public int? EpisodeNumber { get; set; }
        [JsonProperty("available")] public bool Available { get; set; }
    }

    [NotMapped]
    public class SpecialSearchResultDto
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("media_type")] public string MediaType { get; set; } = string.Empty;
        [JsonProperty("title")] public string Title { get; set; } = string.Empty;
        [JsonProperty("overview")] public string? Overview { get; set; }
        [JsonProperty("still")] public string? Still { get; set; }
        [JsonProperty("poster")] public string? Poster { get; set; }
        [JsonProperty("year")] public int? Year { get; set; }
        [JsonProperty("show_title")] public string? ShowTitle { get; set; }
        [JsonProperty("season_number")] public int? SeasonNumber { get; set; }
        [JsonProperty("episode_number")] public int? EpisodeNumber { get; set; }
        [JsonProperty("available")] public bool Available { get; set; }
    }

    [NotMapped]
    public class SpecialItemsUpdateRequest
    {
        [JsonProperty("items")] public List<SpecialItemUpdateDto> Items { get; set; } = [];
    }

    [NotMapped]
    public class SpecialItemUpdateDto
    {
        [JsonProperty("media_type")] public string MediaType { get; set; } = string.Empty;
        [JsonProperty("media_id")] public int MediaId { get; set; }
        [JsonProperty("order")] public int Order { get; set; }
    }
}