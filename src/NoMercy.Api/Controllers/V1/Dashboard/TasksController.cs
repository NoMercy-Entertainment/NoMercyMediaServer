using System.Collections.Immutable;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.Controllers.V1.Music;
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
using NoMercy.Encoder;
using NoMercy.Encoder.Core;
using NoMercy.Helpers;
using NoMercy.Helpers.Extensions;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.Queue;


namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Tasks")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/tasks", Order = 10)]
public class TasksController(MediaContext mediaContext) : BaseController
{
    [HttpGet]
    public IActionResult Index()
    {
        if (!User.IsModerator())
            return Unauthorized(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "You do not have permission to view tasks"
            });

        List<TaskDto> list =
        [
            new()
            {
                Id = "pqiilkpnf8lmwrcxn0l8tngf",
                Title = "Scan media library",
                Value = 0,
                Type = "library",
                CreatedAt = DateTime.Parse("2024-01-25 09:26:56")
            }
        ];

        return Ok(list);
    }

    [HttpPost]
    public IActionResult Store()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create tasks");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpPatch]
    public IActionResult Update()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update tasks");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpDelete]
    public IActionResult Destroy()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete tasks");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpPost]
    [Route("pause/{id:int}")]
    public async Task<IActionResult> PauseTask(int id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to pause tasks");

        bool status = await FfMpeg.Pause(id);

        return Ok(status);
    }

    [HttpPost]
    [Route("resume/{id:int}")]
    public async Task<IActionResult> ResumeTask(int id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to resume tasks");

        bool status = await FfMpeg.Resume(id);

        return Ok(status);
    }

    [HttpGet]
    [Route("runners")]
    public IActionResult RunningTaskWorkers()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view task workers");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpGet]
    [Route("queue")]
    public async Task<IActionResult> EncoderQueue()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoder queue");

        await using QueueContext queueContext = new();

        ImmutableList<QueueJob> jobs = queueContext.QueueJobs
            .Where(j => j.Queue == "encoder")
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .ToImmutableList();

        List<EncodeVideoJob> encoderJobs = jobs
            .Select(job => job.Payload.FromJson<EncodeVideoJob>())
            .Where(job => job is not null)
            .ToList()!;

        List<Ulid> folderIds = encoderJobs.Select(j => j.FolderId).ToList();

        // Load folders into memory first
        List<Folder> folders = await mediaContext.Folders
            .Where(f => folderIds.Contains(f.Id))
            .Include(f => f.EncoderProfileFolder)
            .ThenInclude(e => e.EncoderProfile)
            .Include(f => f.FolderLibraries)
            .ThenInclude(f => f.Library)
            .ThenInclude(f => f.LibraryTvs)
            .ThenInclude(libraryTv => libraryTv.Tv)
            .ThenInclude(tv => tv.Episodes)
            .Include(f => f.FolderLibraries)
            .ThenInclude(f => f.Library)
            .ThenInclude(f => f.LibraryMovies)
            .ThenInclude(libraryMovie => libraryMovie.Movie)
            .Include(f => f.FolderLibraries)
            .ThenInclude(f => f.Library)
            .ThenInclude(f => f.LibraryTracks)
            .ThenInclude(libraryTrack => libraryTrack.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .ToListAsync();

        QueueJobDto[] queueJobs = encoderJobs
            .Select(j => new QueueJobDto
            {
                Id = jobs.ElementAt(encoderJobs.IndexOf(j)).Id,
                Priority = jobs.ElementAt(encoderJobs.IndexOf(j)).Priority,
                PayloadId = j.Id,
                Title = GetTitle(folders, j),
                Type = j.GetType().Name,
                Status = j.Status.ToString(),
                InputFile = j.InputFile,
                Profile = folders.FirstOrDefault(f => f.Id == j.FolderId)
                    ?.EncoderProfileFolder.FirstOrDefault()
                    ?.EncoderProfile.Name
            }).ToArray();

        IEnumerable<EncodeVideoJob> runningJobs = encoderJobs
            .Where(j => j.Status == "running");

        foreach (EncodeVideoJob job in runningJobs)
            Networking.Networking.SendToAll("encoder-progress", "dashboardHub", new Progress
            {
                Id = job.Id,
                Status = "running",
                Title = GetTitle(folders, job),
                Message = "Encoding video"
            });

        return Ok(new DataResponseDto<QueueJobDto[]>
        {
            Data = queueJobs
        });
    }

    private static string GetTitle(List<Folder> folders, EncodeVideoJob j)
    {
        Movie? movie = folders.FirstOrDefault(f => f.Id == j.FolderId)
            ?.FolderLibraries.FirstOrDefault()
            ?.Library.LibraryMovies.FirstOrDefault(m => m.MovieId == j.Id.ToInt())?.Movie;

        Tv? tv = folders.FirstOrDefault(f => f.Id == j.FolderId)
            ?.FolderLibraries.FirstOrDefault()
            ?.Library.LibraryTvs.FirstOrDefault(m => m.Tv.Episodes.Any(e => e.Id == j.Id.ToInt()))?.Tv;

        Episode? episode = tv?.Episodes.FirstOrDefault(e => e.Id == j.Id.ToInt());

        Track? track = folders.FirstOrDefault(f => f.Id == j.FolderId)
            ?.FolderLibraries.FirstOrDefault()
            ?.Library.LibraryTracks.FirstOrDefault(m => m.TrackId == j.Id.ToGuid())?.Track;

        return movie?.CreateTitle()
               ?? episode?.CreateTitle()
               ?? track?.CreateName()
               ?? string.Empty;
    }

    [HttpDelete]
    [Route("queue/{id:int}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to clear encoder queue");

        await using QueueContext queueContext = new();
        QueueJob? job = queueContext.QueueJobs
            .FirstOrDefault(job => job.Id == id);

        if (job is null)
            return NotFoundResponse("Job not found");

        queueContext.QueueJobs.Remove(job);

        await queueContext.SaveChangesAsync();

        return Ok(new StatusResponseDto<string>
        {
            Message = "Job removed",
            Status = "success"
        });
    }

    [HttpPatch]
    [Route("queue/{id:int}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] PatchQueueItemDto request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to clear encoder queue");

        await using QueueContext queueContext = new();

        QueueJob? job = queueContext.QueueJobs
            .FirstOrDefault(job => job.Id == id);

        if (job is null)
            return NotFoundResponse("Job not found");

        job.Priority = request.Priority;

        await queueContext.SaveChangesAsync();

        return Ok(new StatusResponseDto<string>
        {
            Message = "Priority updated",
            Status = "success"
        });
    }

    [HttpPost]
    [Route("failed/retry")]
    [Route("failed/retry/{id:long?}")]
    public async Task<IActionResult> RetryFailedJobs(long? id = null)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to retry failed jobs");

        await using QueueContext queueContext = new();
        using EfQueueContextAdapter adapter = new(queueContext);
        JobQueue jobQueue = new(adapter);

        if (id.HasValue)
        {
            FailedJob? failedJob = await queueContext.FailedJobs.FindAsync(id.Value);
            if (failedJob == null)
                return NotFoundResponse("Failed job not found");
        }

        jobQueue.RetryFailedJobs(id);

        string message = id.HasValue
            ? "Failed job has been queued for retry"
            : "All failed jobs have been queued for retry";

        return Ok(new StatusResponseDto<string>
        {
            Message = message,
            Status = "success"
        });
    }

    [HttpGet]
    [Route("failed")]
    public async Task<IActionResult> GetFailedJobs()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view failed jobs");

        await using QueueContext queueContext = new();

        List<FailedJob> failedJobs = await queueContext.FailedJobs
            .OrderByDescending(j => j.FailedAt)
            .ToListAsync();

        return Ok(new DataResponseDto<List<FailedJob>>
        {
            Data = failedJobs
        });
    }
}

public class QueueJobDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("payload_id")] public string PayloadId { get; set; } = string.Empty;
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("status")] public string Status { get; set; } = string.Empty;
    [JsonProperty("input_file")] public string InputFile { get; set; } = string.Empty;
    [JsonProperty("profile")] public string? Profile { get; set; }
    [JsonProperty("priority")] public int Priority { get; set; }
}

public class PatchQueueItemDto
{
    [JsonProperty("priority")] public int Priority { get; set; }
}