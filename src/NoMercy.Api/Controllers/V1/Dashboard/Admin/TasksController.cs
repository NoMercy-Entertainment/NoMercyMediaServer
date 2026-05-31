using System.Collections.Immutable;
using System.Diagnostics;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Execution;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.Helpers.Extensions;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.Queue.MediaServer;
using NoMercyQueue;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

[ApiController]
[Tags("Dashboard Tasks")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/tasks", Order = 10)]
public class TasksController(
    MediaContext mediaContext,
    IEncoderProcessRegistry processRegistry,
    ProcessThrottle processThrottle,
    EncodingHistoryRepository historyRepository
) : BaseController
{
    [HttpGet]
    public IActionResult Index()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view tasks");

        List<TaskDto> list =
        [
            new()
            {
                Id = "pqiilkpnf8lmwrcxn0l8tngf",
                Title = "Scan media library",
                Value = 0,
                Type = "library",
                CreatedAt = DateTime.Parse("2024-01-25 09:26:56"),
            },
        ];

        return Ok(list);
    }

    [HttpPost]
    public IActionResult Store()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create tasks");

        return Ok(new PlaceholderResponse { Data = [] });
    }

    [HttpPatch]
    public IActionResult Update()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update tasks");

        return Ok(new PlaceholderResponse { Data = [] });
    }

    [HttpDelete]
    public IActionResult Destroy()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete tasks");

        return Ok(new PlaceholderResponse { Data = [] });
    }

    [HttpPost]
    [Route("pause/{id:int}")]
    public IActionResult PauseTask(int id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to pause tasks");

        IReadOnlyCollection<int> pids = processRegistry.GetProcessIds(id);
        if (pids.Count == 0)
            return Ok(false);

        foreach (int pid in pids)
            processThrottle.Suspend(pid);

        return Ok(true);
    }

    [HttpPost]
    [Route("resume/{id:int}")]
    public IActionResult ResumeTask(int id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to resume tasks");

        IReadOnlyCollection<int> pids = processRegistry.GetProcessIds(id);
        if (pids.Count == 0)
            return Ok(false);

        foreach (int pid in pids)
            processThrottle.Resume(pid);

        return Ok(true);
    }

    [HttpGet]
    [Route("runners")]
    public IActionResult RunningTaskWorkers()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view task workers");

        return Ok(new PlaceholderResponse { Data = [] });
    }

    [HttpGet]
    [Route("queue")]
    public async Task<IActionResult> EncoderQueue()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoder queue");

        await using QueueContext queueContext = new();

        ImmutableList<QueueJob> jobs = queueContext
            .QueueJobs.Where(j => j.Queue == "encoder")
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .ToImmutableList();

        List<VideoEncodeJob> encoderJobs = jobs.Select(job =>
                job.Payload.FromJson<VideoEncodeJob>()
            )
            .Where(job => job is not null)
            .ToList()!;

        List<Ulid> folderIds = encoderJobs.Select(j => j.FolderId).ToList();

        // Load folders into memory first
        List<Folder> folders = await mediaContext
            .Folders.Where(f => folderIds.Contains(f.Id))
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
                Profile = folders
                    .FirstOrDefault(f => f.Id == j.FolderId)
                    ?.EncoderProfileFolder.FirstOrDefault()
                    ?.EncoderProfile.Name,
            })
            .ToArray();

        IEnumerable<VideoEncodeJob> runningJobs = encoderJobs.Where(j => j.Status == "running");

        if (EventBusProvider.IsConfigured)
            foreach (VideoEncodeJob job in runningJobs)
                _ = EventBusProvider.Current.PublishAsync(
                    new EncoderProgressBroadcastEvent
                    {
                        ProgressData = new
                        {
                            job.Id,
                            Status = "running",
                            Title = GetTitle(folders, job),
                            Message = "Encoding video",
                        },
                    }
                );

        return Ok(new DataResponseDto<QueueJobDto[]> { Data = queueJobs });
    }

    private static string GetTitle(List<Folder> folders, VideoEncodeJob j)
    {
        Movie? movie = folders
            .FirstOrDefault(f => f.Id == j.FolderId)
            ?.FolderLibraries.FirstOrDefault()
            ?.Library.LibraryMovies.FirstOrDefault(m => m.MovieId == j.Id.ToInt())
            ?.Movie;

        Tv? tv = folders
            .FirstOrDefault(f => f.Id == j.FolderId)
            ?.FolderLibraries.FirstOrDefault()
            ?.Library.LibraryTvs.FirstOrDefault(m => m.Tv.Episodes.Any(e => e.Id == j.Id.ToInt()))
            ?.Tv;

        Episode? episode = tv?.Episodes.FirstOrDefault(e => e.Id == j.Id.ToInt());

        Track? track = folders
            .FirstOrDefault(f => f.Id == j.FolderId)
            ?.FolderLibraries.FirstOrDefault()
            ?.Library.LibraryTracks.FirstOrDefault(m => m.TrackId == j.Id.ToGuid())
            ?.Track;

        return (movie?.CreateTitle() ?? episode?.CreateTitle() ?? track?.CreateName()).OrEmpty();
    }

    [HttpDelete]
    [Route("queue/{id:int}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to clear encoder queue");

        await using QueueContext queueContext = new();
        QueueJob? job = queueContext.QueueJobs.FirstOrDefault(job => job.Id == id);

        if (job is null)
            return NotFoundResponse("Job not found");

        // If the job is currently running, terminate the FFmpeg process(es) tracked
        // for it. Without this the ffmpeg process keeps going after the queue entry
        // is removed — V1 dashboard behavior expects the kill.
        VideoEncodeJob? payload = job.Payload.FromJson<VideoEncodeJob>();
        if (payload is not null && int.TryParse(payload.Id, out int mediaId))
        {
            IReadOnlyCollection<int> pids = processRegistry.GetProcessIds(mediaId);
            foreach (int pid in pids)
            {
                try
                {
                    using Process ffmpegProcess = Process.GetProcessById(pid);
                    ffmpegProcess.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                    // Process may have already exited or the PID may be stale —
                    // clean up the registry entry regardless.
                }
                processRegistry.Unregister(mediaId, pid);
            }
        }

        queueContext.QueueJobs.Remove(job);

        await queueContext.SaveChangesAsync();

        return Ok(new StatusResponseDto<string> { Message = "Job removed", Status = "success" });
    }

    [HttpPatch]
    [Route("queue/{id:int}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] PatchQueueItemDto request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to clear encoder queue");

        await using QueueContext queueContext = new();

        QueueJob? job = queueContext.QueueJobs.FirstOrDefault(job => job.Id == id);

        if (job is null)
            return NotFoundResponse("Job not found");

        job.Priority = request.Priority;

        await queueContext.SaveChangesAsync();

        return Ok(
            new StatusResponseDto<string> { Message = "Priority updated", Status = "success" }
        );
    }

    /// <summary>
    /// Stop dispatching new encoder jobs. In-flight FFmpeg processes keep running —
    /// use the per-task pause endpoint to suspend those separately.
    /// </summary>
    [HttpPost]
    [Route("pause-queue")]
    public async Task<IActionResult> PauseEncoderQueue()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to pause the encoder queue");

        if (QueueRunner.Current is null)
            return Ok(
                new StatusResponseDto<string>
                {
                    Message = "Queue runner not available",
                    Status = "unavailable",
                }
            );

        await QueueRunner.Current.Pause("encoder");
        return Ok(
            new StatusResponseDto<string> { Message = "Encoder queue paused", Status = "success" }
        );
    }

    /// <summary>Resume dispatching jobs from the encoder queue.</summary>
    [HttpPost]
    [Route("resume-queue")]
    public async Task<IActionResult> ResumeEncoderQueue()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to resume the encoder queue");

        if (QueueRunner.Current is null)
            return Ok(
                new StatusResponseDto<string>
                {
                    Message = "Queue runner not available",
                    Status = "unavailable",
                }
            );

        await QueueRunner.Current.Resume("encoder");
        return Ok(
            new StatusResponseDto<string> { Message = "Encoder queue resumed", Status = "success" }
        );
    }

    /// <summary>Source-of-truth paused state for the encoder queue. The
    /// dashboard reads this on every queue refresh so its pause/resume
    /// toggle reflects the persisted state across server restarts —
    /// previously the UI tracked it in a client-side ref that defaulted to
    /// "running" no matter what the server thought.</summary>
    [HttpGet]
    [Route("queue/status")]
    public IActionResult EncoderQueueStatus()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoder queue status");

        bool paused = QueueRunner.Current?.IsPaused("encoder") ?? false;
        return Ok(new { paused });
    }

    /// <summary>
    /// Estimated completion time for the current encoder queue. Based on the
    /// rolling average duration of the most recent 50 successful encodes in
    /// EncodingHistory × remaining queue size. Returns zero when history is
    /// empty (no basis to extrapolate).
    /// </summary>
    [HttpGet]
    [Route("queue/eta")]
    public async Task<IActionResult> EncoderQueueEta()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoder queue ETA");

        List<EncodingHistory> recent = await historyRepository.GetRecentAsync(
            pageSize: 50,
            pageIndex: 0
        );

        await using QueueContext queueContext = new();
        int queueDepth = await queueContext.QueueJobs.CountAsync(j => j.Queue == "encoder");

        if (recent.Count == 0 || queueDepth == 0)
        {
            return Ok(
                new
                {
                    queueDepth,
                    averageEncodeSeconds = 0.0,
                    estimatedSecondsRemaining = 0.0,
                    basedOnSamples = recent.Count,
                }
            );
        }

        double avgSeconds = recent.Average(h => h.DurationSeconds);
        double etaSeconds = avgSeconds * queueDepth;

        return Ok(
            new
            {
                queueDepth,
                averageEncodeSeconds = avgSeconds,
                estimatedSecondsRemaining = etaSeconds,
                basedOnSamples = recent.Count,
            }
        );
    }

    /// <summary>
    /// Reorder pending items in a named queue to match the supplied ordered list of job IDs.
    /// Running items (ReservedAt != null) are never moved. IDs not found in the queue are
    /// silently ignored. Queue items whose IDs were not supplied are appended after the
    /// reordered block, preserving their relative order.
    /// </summary>
    [HttpPost]
    [Route("reorder")]
    public async Task<IActionResult> ReorderQueue([FromBody] ReorderQueueDto request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to reorder the queue");

        await using QueueContext queueContext = new();

        bool queueExists = await queueContext.QueueJobs.AnyAsync(j => j.Queue == request.QueueName);

        if (!queueExists)
            return BadRequestResponse($"Queue '{request.QueueName}' does not exist");

        // Load ALL jobs for this queue so we can compute priorities without a second trip.
        List<QueueJob> allJobs = await queueContext
            .QueueJobs.Where(j => j.Queue == request.QueueName)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .ToListAsync();

        // Split into running (reserved) and pending.
        List<QueueJob> runningJobs = allJobs.Where(j => j.ReservedAt != null).ToList();
        List<QueueJob> pendingJobs = allJobs.Where(j => j.ReservedAt == null).ToList();

        // Build ordered list: requested IDs first (in request order), then the rest.
        HashSet<int> requestedSet = request.OrderedJobIds.ToHashSet();

        List<QueueJob> reordered =
        [
            .. request
                .OrderedJobIds.Select(id => pendingJobs.FirstOrDefault(j => j.Id == id))
                .Where(j => j is not null)
                .Cast<QueueJob>(),
            .. pendingJobs.Where(j => !requestedSet.Contains(j.Id)),
        ];

        // Assign descending priority values so the first item is dispatched first.
        // Start high enough to not collide with running jobs.
        int basePriority = reordered.Count;
        for (int i = 0; i < reordered.Count; i++)
            reordered[i].Priority = basePriority - i;

        await queueContext.SaveChangesAsync();

        // Return the new ordering of ALL jobs for the queue.
        List<QueueJob> resultJobs = [.. runningJobs, .. reordered];

        QueueJobDto[] result = resultJobs
            .Select(j => new QueueJobDto
            {
                Id = j.Id,
                Priority = j.Priority,
                PayloadId = string.Empty,
                Status = j.ReservedAt != null ? "running" : "pending",
            })
            .ToArray();

        return Ok(new DataResponseDto<QueueJobDto[]> { Data = result });
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

        return Ok(new StatusResponseDto<string> { Message = message, Status = "success" });
    }

    [HttpGet]
    [Route("failed")]
    public async Task<IActionResult> GetFailedJobs()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view failed jobs");

        await using QueueContext queueContext = new();

        List<FailedJob> failedJobs = await queueContext
            .FailedJobs.OrderByDescending(j => j.FailedAt)
            .ToListAsync();

        return Ok(new DataResponseDto<List<FailedJob>> { Data = failedJobs });
    }
}

public class QueueJobDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("payload_id")]
    public string PayloadId { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("input_file")]
    public string InputFile { get; set; } = string.Empty;

    [JsonProperty("profile")]
    public string? Profile { get; set; }

    [JsonProperty("priority")]
    public int Priority { get; set; }
}

public class PatchQueueItemDto
{
    [JsonProperty("priority")]
    public int Priority { get; set; }
}

public class ReorderQueueDto
{
    [JsonProperty("queue_name")]
    public string QueueName { get; set; } = string.Empty;

    [JsonProperty("ordered_job_ids")]
    public List<int> OrderedJobIds { get; set; } = [];
}
