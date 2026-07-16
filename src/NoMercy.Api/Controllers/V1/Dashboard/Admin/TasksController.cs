// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

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
using NoMercy.Database.Models.Encoder;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Execution;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.Queue.MediaServer;
using NoMercyQueue;
using MediaJobDispatcher = NoMercy.MediaProcessing.Jobs.JobDispatcher;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

[ApiController]
[Tags("Dashboard Tasks")]
[ApiVersion(1.0)]
[Authorize(Policy = "Moderator")]
[Route("api/v{version:apiVersion}/dashboard/tasks", Order = 10)]
public class TasksController(
    MediaContext mediaContext,
    IDbContextFactory<QueueContext> queueContextFactory,
    IEncoderProcessRegistry processRegistry,
    ProcessThrottle processThrottle,
    IEncodingHistoryRepository historyRepository
) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();

        // Cap the load: the queue table retains history and grows unbounded, so
        // materializing every row here was seconds of work for a monitor view that
        // only shows the highest-priority pending tasks.
        List<QueueJob> jobs = await queueContext
            .QueueJobs.OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .Take(UiLimits.MaximumTasksInList)
            .ToListAsync();

        List<TaskDto> list = jobs.Select(job => new TaskDto
            {
                Id = job.Id.ToString(),
                Title = ResolveJobTitle(job),
                Value = 0,
                Type = job.Queue,
                CreatedAt = job.CreatedAt,
                UpdatedAt = job.ReservedAt ?? job.CreatedAt,
            })
            .ToList();

        return Ok(list);
    }

    /// <summary>
    /// Best-effort human-readable job type for the dashboard task list. Falls
    /// back to the raw queue name if the payload can't be deserialized to its
    /// concrete job type (e.g. a stale payload from a removed job class).
    /// </summary>
    private static string ResolveJobTitle(QueueJob job)
    {
        try
        {
            return SerializationHelper.Deserialize<object>(job.Payload).GetType().Name;
        }
        catch (Exception)
        {
            return job.Queue;
        }
    }

    [HttpPost]
    public IActionResult Store()
    {
        return Ok(new PlaceholderResponse { Data = [] });
    }

    [HttpPatch]
    public IActionResult Update()
    {
        return Ok(new PlaceholderResponse { Data = [] });
    }

    [HttpDelete]
    public IActionResult Destroy()
    {
        return Ok(new PlaceholderResponse { Data = [] });
    }

    [HttpPost]
    [Route("pause/{id:int}")]
    public IActionResult PauseTask(int id)
    {
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
        return Ok(new PlaceholderResponse { Data = [] });
    }

    [HttpGet]
    [Route("queue")]
    public async Task<IActionResult> EncoderQueue()
    {
        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();

        ImmutableList<QueueJob> jobs = queueContext
            .QueueJobs.Where(j => j.Queue == "encoder")
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .ThenBy(j => j.Id)
            .ToImmutableList();

        List<VideoEncodeJob> encoderJobs = jobs.Select(job =>
                job.Payload.FromJson<VideoEncodeJob>()
            )
            .Where(job => job is not null)
            .ToList()!;

        // Parse each job id once — Id is either an int (movie/episode) or a Guid (track).
        List<int> movieOrEpisodeIds = [];
        List<Guid> trackIds = [];

        foreach (VideoEncodeJob encoderJob in encoderJobs)
        {
            int intId = encoderJob.Id.ToInt();
            if (intId != 0)
            {
                movieOrEpisodeIds.Add(intId);
            }
            else
            {
                Guid guidId = encoderJob.Id.ToGuid();
                if (guidId != Guid.Empty)
                    trackIds.Add(guidId);
            }
        }

        List<Ulid> folderIds = encoderJobs.Select(j => j.FolderId).Distinct().ToList();

        // Folders — only the profile include needed for the Profile field; no library graph.
        List<Folder> folders = await mediaContext
            .Folders.AsNoTracking()
            .Where(f => folderIds.Contains(f.Id))
            .Include(f => f.EncodingPresetFolders)
                .ThenInclude(link => link.Preset)
            .ToListAsync();

        Dictionary<Ulid, Folder> folderById = folders.ToDictionary(f => f.Id);

        // Load only the entities actually referenced by queued jobs.
        Dictionary<int, Movie> movieById = [];
        Dictionary<int, Episode> episodeById = [];
        Dictionary<Guid, Track> trackById = [];

        if (movieOrEpisodeIds.Count > 0)
        {
            List<Movie> movies = await mediaContext
                .Movies.AsNoTracking()
                .Where(m => movieOrEpisodeIds.Contains(m.Id))
                .ToListAsync();

            foreach (Movie movie in movies)
                movieById[movie.Id] = movie;

            // Episodes need Tv for CreateTitle (Tv.Title, SeasonNumber, EpisodeNumber).
            List<Episode> episodes = await mediaContext
                .Episodes.AsNoTracking()
                .Where(e => movieOrEpisodeIds.Contains(e.Id))
                .Include(e => e.Tv)
                .ToListAsync();

            foreach (Episode episode in episodes)
                episodeById[episode.Id] = episode;
        }

        if (trackIds.Count > 0)
        {
            // Tracks need AlbumTrack → Album for CreateName.
            List<Track> tracks = await mediaContext
                .Tracks.AsNoTracking()
                .Where(t => trackIds.Contains(t.Id))
                .Include(t => t.AlbumTrack)
                    .ThenInclude(at => at.Album)
                .ToListAsync();

            foreach (Track track in tracks)
                trackById[track.Id] = track;
        }

        QueueJobDto[] queueJobs = encoderJobs
            .Select(
                (j, index) =>
                    new QueueJobDto
                    {
                        Id = jobs[index].Id,
                        Priority = jobs[index].Priority,
                        PayloadId = j.Id,
                        Title = ResolveTitle(j, movieById, episodeById, trackById),
                        Type = j.GetType().Name,
                        Status = j.Status.ToString(),
                        InputFile = j.InputFile,
                        Profile = folderById
                            .GetValueOrDefault(j.FolderId)
                            ?.EncodingPresetFolders.OrderByDescending(link => link.IsDefault)
                            .FirstOrDefault()
                            ?.Preset?.Name,
                    }
            )
            .ToArray();

        // Broadcast current progress for running jobs — reuse already-resolved titles.
        if (EventBusProvider.IsConfigured)
        {
            foreach (QueueJobDto dto in queueJobs.Where(dto => dto.Status == "running"))
            {
                _ = EventBusProvider.Current.PublishAsync(
                    new EncodingProgressBroadcastedEvent
                    {
                        ProgressData = new
                        {
                            Id = dto.PayloadId,
                            Status = "running",
                            dto.Title,
                            Message = "Encoding video",
                        },
                    }
                );
            }
        }

        return Ok(new DataResponseDto<QueueJobDto[]> { Data = queueJobs });
    }

    private static string ResolveTitle(
        VideoEncodeJob j,
        Dictionary<int, Movie> movieById,
        Dictionary<int, Episode> episodeById,
        Dictionary<Guid, Track> trackById
    )
    {
        int intId = j.Id.ToInt();
        if (intId != 0)
        {
            if (movieById.TryGetValue(intId, out Movie? movie))
                return movie.CreateTitle();

            if (episodeById.TryGetValue(intId, out Episode? episode))
                return episode.CreateTitle();
        }

        Guid guidId = j.Id.ToGuid();
        if (guidId != Guid.Empty && trackById.TryGetValue(guidId, out Track? track))
            return track.CreateName();

        return string.Empty;
    }

    [HttpDelete]
    [Route("queue/{id:int}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();
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
        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();

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
        List<EncodingHistory> recent = await historyRepository.GetRecentAsync(
            pageSize: 50,
            pageIndex: 0
        );

        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();
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
        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();

        bool queueExists = await queueContext.QueueJobs.AnyAsync(j => j.Queue == request.QueueName);

        if (!queueExists)
            return BadRequestResponse($"Queue '{request.QueueName}' does not exist");

        // Load ALL jobs for this queue so we can compute priorities without a second trip.
        List<QueueJob> allJobs = await queueContext
            .QueueJobs.Where(j => j.Queue == request.QueueName)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .ThenBy(j => j.Id)
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
        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();
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
        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();

        List<FailedJob> failedJobs = await queueContext
            .FailedJobs.OrderByDescending(j => j.FailedAt)
            .ToListAsync();

        return Ok(new DataResponseDto<List<FailedJob>> { Data = failedJobs });
    }

    [HttpGet]
    [Route("queue/incomplete")]
    public async Task<IActionResult> IncompleteEncodes()
    {
        List<IncompleteEncodeDto> rows = await mediaContext
            .IncompleteEncodes.AsNoTracking()
            .OrderByDescending(r => r.LastSeenAt)
            .Select(r => new IncompleteEncodeDto
            {
                Id = r.Id,
                MediaId = r.MediaId,
                Title = r.Title,
                MissingRenditions = r.MissingRenditions.Split(
                    '\n',
                    StringSplitOptions.RemoveEmptyEntries
                ),
                LastError = r.LastError,
                AttemptsMade = r.AttemptsMade,
                LastSeenAt = r.LastSeenAt,
            })
            .ToListAsync();

        return Ok(new DataResponseDto<List<IncompleteEncodeDto>> { Data = rows });
    }

    [HttpPost]
    [Route("queue/incomplete/{id:int}/retry")]
    public async Task<IActionResult> RetryIncompleteEncode(int id)
    {
        IncompleteEncode? row = await mediaContext.IncompleteEncodes.FindAsync(id);
        if (row is null)
            return NotFoundResponse("Incomplete encode record not found");

        if (Ulid.TryParse(row.FolderId, out Ulid folderUlid))
        {
            // Resolve the library that owns this folder.
            FolderLibrary? folderLibrary = await mediaContext
                .FolderLibrary.AsNoTracking()
                .FirstOrDefaultAsync(fl => fl.FolderId == folderUlid);

            if (folderLibrary is not null)
            {
                try
                {
                    int mediaId = checked((int)row.MediaId);

                    // Pick the best available video file for this media item (movie or episode).
                    VideoFile? videoFile = await mediaContext
                        .VideoFiles.AsNoTracking()
                        .FirstOrDefaultAsync(vf =>
                            vf.MovieId == mediaId || vf.EpisodeId == mediaId
                        );

                    if (videoFile is not null)
                    {
                        string inputFile =
                            videoFile.HostFolder.TrimEnd('/')
                            + "/"
                            + videoFile.Filename.TrimStart('/');

                        try
                        {
                            MediaJobDispatcher dispatcher = new();
                            dispatcher.DispatchJob<VideoEncodeJob>(
                                folderLibrary.LibraryId,
                                folderLibrary.FolderId,
                                row.MediaId.ToString(),
                                inputFile
                            );
                        }
                        catch (InvalidOperationException)
                        {
                            // Queue not running (e.g. server restart in progress) — the
                            // quarantine row is removed regardless so the admin can retry
                            // again once the queue is back up.
                        }
                    }
                }
                catch (OverflowException)
                {
                    // MediaId exceeds int range — cannot safely look up VideoFile.
                    // Row is still removed so the admin can clear corrupt quarantine entries.
                }
            }
        }

        mediaContext.IncompleteEncodes.Remove(row);
        await mediaContext.SaveChangesAsync();

        return Ok(new StatusResponseDto<string> { Message = "Re-queued", Status = "success" });
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

public class IncompleteEncodeDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("media_id")]
    public long MediaId { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("missing_renditions")]
    public string[] MissingRenditions { get; set; } = [];

    [JsonProperty("last_error")]
    public string? LastError { get; set; }

    [JsonProperty("attempts_made")]
    public int AttemptsMade { get; set; }

    [JsonProperty("last_seen_at")]
    public DateTime LastSeenAt { get; set; }
}
