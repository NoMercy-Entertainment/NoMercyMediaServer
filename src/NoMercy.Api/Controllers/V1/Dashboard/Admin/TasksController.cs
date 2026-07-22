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
[Tags(tags: "Dashboard Tasks")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "Moderator")]
[Route(template: "api/v{version:apiVersion}/dashboard/tasks", Order = 10)]
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
            .QueueJobs.OrderByDescending(keySelector: j => j.Priority)
            .ThenBy(keySelector: j => j.CreatedAt)
            .ThenBy(keySelector: j => j.Id)
            .Take(count: UiLimits.MaximumTasksInList)
            .ToListAsync();

        List<TaskDto> list = jobs.Select(selector: job => new TaskDto
            {
                Id = job.Id.ToString(),
                Title = ResolveJobTitle(job: job),
                Value = 0,
                Type = job.Queue,
                CreatedAt = job.CreatedAt,
                UpdatedAt = job.ReservedAt ?? job.CreatedAt,
            })
            .ToList();

        return Ok(value: list);
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
            return SerializationHelper.Deserialize<object>(data: job.Payload).GetType().Name;
        }
        catch (Exception)
        {
            return job.Queue;
        }
    }

    [HttpPost]
    public IActionResult Store()
    {
        return Ok(value: new PlaceholderResponse { Data = [] });
    }

    [HttpPatch]
    public IActionResult Update()
    {
        return Ok(value: new PlaceholderResponse { Data = [] });
    }

    [HttpDelete]
    public IActionResult Destroy()
    {
        return Ok(value: new PlaceholderResponse { Data = [] });
    }

    [HttpPost]
    [Route(template: "pause/{id:int}")]
    public IActionResult PauseTask(int id)
    {
        IReadOnlyCollection<int> pids = processRegistry.GetProcessIds(jobId: id);
        if (pids.Count == 0)
            return Ok(value: false);

        foreach (int pid in pids)
            processThrottle.Suspend(processId: pid);

        return Ok(value: true);
    }

    [HttpPost]
    [Route(template: "resume/{id:int}")]
    public IActionResult ResumeTask(int id)
    {
        IReadOnlyCollection<int> pids = processRegistry.GetProcessIds(jobId: id);
        if (pids.Count == 0)
            return Ok(value: false);

        foreach (int pid in pids)
            processThrottle.Resume(processId: pid);

        return Ok(value: true);
    }

    [HttpGet]
    [Route(template: "runners")]
    public IActionResult RunningTaskWorkers()
    {
        return Ok(value: new PlaceholderResponse { Data = [] });
    }

    [HttpGet]
    [Route(template: "queue")]
    public async Task<IActionResult> EncoderQueue()
    {
        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();

        ImmutableList<QueueJob> jobs = queueContext
            .QueueJobs.Where(predicate: j => j.Queue == "encoder")
            .OrderByDescending(keySelector: j => j.Priority)
            .ThenBy(keySelector: j => j.CreatedAt)
            .ThenBy(keySelector: j => j.Id)
            .ToImmutableList();

        // Each parsed payload stays paired with the row it came from: a payload
        // that fails to deserialize is dropped here, so indexing the unfiltered
        // row list by the parsed list's position would hand every later job the
        // preceding row's id, priority and reservation.
        List<QueueJobEntry> encoderJobs = jobs.Select(selector: row => new QueueJobEntry(
                Row: row,
                Job: row.Payload.FromJson<VideoEncodeJob>()
            ))
            .Where(predicate: entry => entry.Job is not null)
            .ToList();

        // Parse each job id once — Id is either an int (movie/episode) or a Guid (track).
        List<int> movieOrEpisodeIds = [];
        List<Guid> trackIds = [];

        foreach (VideoEncodeJob encoderJob in encoderJobs.Select(selector: entry => entry.Job!))
        {
            int intId = encoderJob.Id.ToInt();
            if (intId != 0)
            {
                movieOrEpisodeIds.Add(item: intId);
            }
            else
            {
                Guid guidId = encoderJob.Id.ToGuid();
                if (guidId != Guid.Empty)
                    trackIds.Add(item: guidId);
            }
        }

        List<Ulid> folderIds = encoderJobs.Select(selector: entry => entry.Job!.FolderId).Distinct().ToList();

        // Folders — only the profile include needed for the Profile field; no library graph.
        List<Folder> folders = await mediaContext
            .Folders.AsNoTracking()
            .Where(predicate: f => folderIds.Contains(f.Id))
            .Include(navigationPropertyPath: f => f.EncodingPresetFolders)
                .ThenInclude(navigationPropertyPath: link => link.Preset)
            .ToListAsync();

        Dictionary<Ulid, Folder> folderById = folders.ToDictionary(keySelector: f => f.Id);

        // Load only the entities actually referenced by queued jobs.
        Dictionary<int, Movie> movieById = [];
        Dictionary<int, Episode> episodeById = [];
        Dictionary<Guid, Track> trackById = [];

        if (movieOrEpisodeIds.Count > 0)
        {
            List<Movie> movies = await mediaContext
                .Movies.AsNoTracking()
                .Where(predicate: m => movieOrEpisodeIds.Contains(m.Id))
                .ToListAsync();

            foreach (Movie movie in movies)
                movieById[key: movie.Id] = movie;

            // Episodes need Tv for CreateTitle (Tv.Title, SeasonNumber, EpisodeNumber).
            List<Episode> episodes = await mediaContext
                .Episodes.AsNoTracking()
                .Where(predicate: e => movieOrEpisodeIds.Contains(e.Id))
                .Include(navigationPropertyPath: e => e.Tv)
                .ToListAsync();

            foreach (Episode episode in episodes)
                episodeById[key: episode.Id] = episode;
        }

        if (trackIds.Count > 0)
        {
            // Tracks need AlbumTrack → Album for CreateName.
            List<Track> tracks = await mediaContext
                .Tracks.AsNoTracking()
                .Where(predicate: t => trackIds.Contains(t.Id))
                .Include(navigationPropertyPath: t => t.AlbumTrack)
                    .ThenInclude(navigationPropertyPath: at => at.Album)
                .ToListAsync();

            foreach (Track track in tracks)
                trackById[key: track.Id] = track;
        }

        QueueJobDto[] queueJobs = encoderJobs
            .Select(selector: entry =>
            {
                VideoEncodeJob j = entry.Job!;
                return new QueueJobDto
                {
                    Id = entry.Row.Id,
                    Priority = entry.Row.Priority,
                    PayloadId = j.Id,
                    Title = ResolveTitle(j: j, movieById: movieById, episodeById: episodeById, trackById: trackById),
                    Type = j.GetType().Name,
                    // Liveness comes from the row, never from the payload: the
                    // payload is the snapshot serialized at enqueue time and is
                    // never rewritten, so its Status is "pending" for a job's
                    // whole life. Reading it left running encodes displayed as
                    // pending and, because the catch-up broadcast below only
                    // fires for "running", suppressed their progress entirely.
                    Status = entry.Row.ReservedAt is not null ? "running" : "pending",
                    InputFile = j.InputFile,
                    Profile = folderById
                        .GetValueOrDefault(key: j.FolderId)
                        ?.EncodingPresetFolders.OrderByDescending(keySelector: link => link.IsDefault)
                        .FirstOrDefault()
                        ?.Preset?.Name,
                };
            })
            .ToArray();

        // Broadcast current progress for running jobs — reuse already-resolved titles.
        if (EventBusProvider.IsConfigured)
        {
            foreach (QueueJobDto dto in queueJobs.Where(predicate: dto => dto.Status == "running"))
            {
                _ = EventBusProvider.Current.PublishAsync(
                    @event: new EncodingProgressBroadcastedEvent
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

        return Ok(value: new DataResponseDto<QueueJobDto[]> { Data = queueJobs });
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
            if (movieById.TryGetValue(key: intId, value: out Movie? movie))
                return movie.CreateTitle();

            if (episodeById.TryGetValue(key: intId, value: out Episode? episode))
                return episode.CreateTitle();
        }

        Guid guidId = j.Id.ToGuid();
        if (guidId != Guid.Empty && trackById.TryGetValue(key: guidId, value: out Track? track))
            return track.CreateName();

        return string.Empty;
    }

    [HttpDelete]
    [Route(template: "queue/{id:int}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();
        QueueJob? job = queueContext.QueueJobs.FirstOrDefault(predicate: job => job.Id == id);

        if (job is null)
            return NotFoundResponse(detail: "Job not found");

        // If the job is currently running, terminate the FFmpeg process(es) tracked
        // for it. Without this the ffmpeg process keeps going after the queue entry
        // is removed — V1 dashboard behavior expects the kill.
        VideoEncodeJob? payload = job.Payload.FromJson<VideoEncodeJob>();
        if (payload is not null && int.TryParse(s: payload.Id, result: out int mediaId))
        {
            IReadOnlyCollection<int> pids = processRegistry.GetProcessIds(jobId: mediaId);
            foreach (int pid in pids)
            {
                try
                {
                    using Process ffmpegProcess = Process.GetProcessById(processId: pid);
                    ffmpegProcess.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                    // Process may have already exited or the PID may be stale —
                    // clean up the registry entry regardless.
                }
                processRegistry.Unregister(jobId: mediaId, processId: pid);
            }
        }

        queueContext.QueueJobs.Remove(entity: job);

        await queueContext.SaveChangesAsync();

        return Ok(value: new StatusResponseDto<string> { Message = "Job removed", Status = "success" });
    }

    [HttpPatch]
    [Route(template: "queue/{id:int}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] PatchQueueItemDto request)
    {
        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();

        QueueJob? job = queueContext.QueueJobs.FirstOrDefault(predicate: job => job.Id == id);

        if (job is null)
            return NotFoundResponse(detail: "Job not found");

        job.Priority = request.Priority;

        await queueContext.SaveChangesAsync();

        return Ok(
            value: new StatusResponseDto<string> { Message = "Priority updated", Status = "success" }
        );
    }

    /// <summary>
    /// Stop dispatching new encoder jobs. In-flight FFmpeg processes keep running —
    /// use the per-task pause endpoint to suspend those separately.
    /// </summary>
    [HttpPost]
    [Route(template: "pause-queue")]
    public async Task<IActionResult> PauseEncoderQueue()
    {
        if (QueueRunner.Current is null)
            return Ok(
                value: new StatusResponseDto<string>
                {
                    Message = "Queue runner not available",
                    Status = "unavailable",
                }
            );

        await QueueRunner.Current.Pause(name: "encoder");
        return Ok(
            value: new StatusResponseDto<string> { Message = "Encoder queue paused", Status = "success" }
        );
    }

    /// <summary>Resume dispatching jobs from the encoder queue.</summary>
    [HttpPost]
    [Route(template: "resume-queue")]
    public async Task<IActionResult> ResumeEncoderQueue()
    {
        if (QueueRunner.Current is null)
            return Ok(
                value: new StatusResponseDto<string>
                {
                    Message = "Queue runner not available",
                    Status = "unavailable",
                }
            );

        await QueueRunner.Current.Resume(name: "encoder");
        return Ok(
            value: new StatusResponseDto<string> { Message = "Encoder queue resumed", Status = "success" }
        );
    }

    /// <summary>Source-of-truth paused state for the encoder queue. The
    /// dashboard reads this on every queue refresh so its pause/resume
    /// toggle reflects the persisted state across server restarts —
    /// previously the UI tracked it in a client-side ref that defaulted to
    /// "running" no matter what the server thought.</summary>
    [HttpGet]
    [Route(template: "queue/status")]
    public IActionResult EncoderQueueStatus()
    {
        bool paused = QueueRunner.Current?.IsPaused(name: "encoder") ?? false;
        return Ok(value: new { paused });
    }

    /// <summary>
    /// Estimated completion time for the current encoder queue. Based on the
    /// rolling average duration of the most recent 50 successful encodes in
    /// EncodingHistory × remaining queue size. Returns zero when history is
    /// empty (no basis to extrapolate).
    /// </summary>
    [HttpGet]
    [Route(template: "queue/eta")]
    public async Task<IActionResult> EncoderQueueEta()
    {
        List<EncodingHistory> recent = await historyRepository.GetRecentAsync(
            pageSize: 50,
            pageIndex: 0
        );

        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();
        int queueDepth = await queueContext.QueueJobs.CountAsync(predicate: j => j.Queue == "encoder");

        if (recent.Count == 0 || queueDepth == 0)
        {
            return Ok(
                value: new
                {
                    queueDepth,
                    averageEncodeSeconds = 0.0,
                    estimatedSecondsRemaining = 0.0,
                    basedOnSamples = recent.Count,
                }
            );
        }

        double avgSeconds = recent.Average(selector: h => h.DurationSeconds);
        double etaSeconds = avgSeconds * queueDepth;

        return Ok(
            value: new
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
    [Route(template: "reorder")]
    public async Task<IActionResult> ReorderQueue([FromBody] ReorderQueueDto request)
    {
        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();

        bool queueExists = await queueContext.QueueJobs.AnyAsync(predicate: j => j.Queue == request.QueueName);

        if (!queueExists)
            return BadRequestResponse(detail: $"Queue '{request.QueueName}' does not exist");

        // Load ALL jobs for this queue so we can compute priorities without a second trip.
        List<QueueJob> allJobs = await queueContext
            .QueueJobs.Where(predicate: j => j.Queue == request.QueueName)
            .OrderByDescending(keySelector: j => j.Priority)
            .ThenBy(keySelector: j => j.CreatedAt)
            .ThenBy(keySelector: j => j.Id)
            .ToListAsync();

        // Split into running (reserved) and pending.
        List<QueueJob> runningJobs = allJobs.Where(predicate: j => j.ReservedAt != null).ToList();
        List<QueueJob> pendingJobs = allJobs.Where(predicate: j => j.ReservedAt == null).ToList();

        // Build ordered list: requested IDs first (in request order), then the rest.
        HashSet<int> requestedSet = request.OrderedJobIds.ToHashSet();

        List<QueueJob> reordered =
        [
            .. request
                .OrderedJobIds.Select(selector: id => pendingJobs.FirstOrDefault(predicate: j => j.Id == id))
                .Where(predicate: j => j is not null)
                .Cast<QueueJob>(),
            .. pendingJobs.Where(predicate: j => !requestedSet.Contains(item: j.Id)),
        ];

        // Assign descending priority values so the first item is dispatched first.
        // Start high enough to not collide with running jobs.
        int basePriority = reordered.Count;
        for (int i = 0; i < reordered.Count; i++)
            reordered[index: i].Priority = basePriority - i;

        await queueContext.SaveChangesAsync();

        // Return the new ordering of ALL jobs for the queue.
        List<QueueJob> resultJobs = [.. runningJobs, .. reordered];

        QueueJobDto[] result = resultJobs
            .Select(selector: j => new QueueJobDto
            {
                Id = j.Id,
                Priority = j.Priority,
                PayloadId = string.Empty,
                Status = j.ReservedAt != null ? "running" : "pending",
            })
            .ToArray();

        return Ok(value: new DataResponseDto<QueueJobDto[]> { Data = result });
    }

    [HttpPost]
    [Route(template: "failed/retry")]
    [Route(template: "failed/retry/{id:long?}")]
    public async Task<IActionResult> RetryFailedJobs(long? id = null)
    {
        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();
        using EfQueueContextAdapter adapter = new(context: queueContext);
        JobQueue jobQueue = new(context: adapter);

        if (id.HasValue)
        {
            FailedJob? failedJob = await queueContext.FailedJobs.FindAsync(keyValues: id.Value);
            if (failedJob == null)
                return NotFoundResponse(detail: "Failed job not found");
        }

        jobQueue.RetryFailedJobs(failedJobId: id);

        string message = id.HasValue
            ? "Failed job has been queued for retry"
            : "All failed jobs have been queued for retry";

        return Ok(value: new StatusResponseDto<string> { Message = message, Status = "success" });
    }

    [HttpGet]
    [Route(template: "failed")]
    public async Task<IActionResult> GetFailedJobs()
    {
        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();

        List<FailedJob> failedJobs = await queueContext
            .FailedJobs.OrderByDescending(keySelector: j => j.FailedAt)
            .ToListAsync();

        return Ok(value: new DataResponseDto<List<FailedJob>> { Data = failedJobs });
    }

    [HttpGet]
    [Route(template: "queue/incomplete")]
    public async Task<IActionResult> IncompleteEncodes()
    {
        List<IncompleteEncodeDto> rows = await mediaContext
            .IncompleteEncodes.AsNoTracking()
            .OrderByDescending(keySelector: r => r.LastSeenAt)
            .Select(selector: r => new IncompleteEncodeDto
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

        return Ok(value: new DataResponseDto<List<IncompleteEncodeDto>> { Data = rows });
    }

    [HttpPost]
    [Route(template: "queue/incomplete/{id:int}/retry")]
    public async Task<IActionResult> RetryIncompleteEncode(int id)
    {
        IncompleteEncode? row = await mediaContext.IncompleteEncodes.FindAsync(keyValues: id);
        if (row is null)
            return NotFoundResponse(detail: "Incomplete encode record not found");

        if (Ulid.TryParse(base32: row.FolderId, ulid: out Ulid folderUlid))
        {
            // Resolve the library that owns this folder.
            FolderLibrary? folderLibrary = await mediaContext
                .FolderLibrary.AsNoTracking()
                .FirstOrDefaultAsync(predicate: fl => fl.FolderId == folderUlid);

            if (folderLibrary is not null)
            {
                try
                {
                    int mediaId = checked((int)row.MediaId);

                    // Pick the best available video file for this media item (movie or episode).
                    VideoFile? videoFile = await mediaContext
                        .VideoFiles.AsNoTracking()
                        .FirstOrDefaultAsync(predicate: vf =>
                            vf.MovieId == mediaId || vf.EpisodeId == mediaId
                        );

                    if (videoFile is not null)
                    {
                        string inputFile =
                            videoFile.HostFolder.TrimEnd(trimChar: '/')
                            + "/"
                            + videoFile.Filename.TrimStart(trimChar: '/');

                        try
                        {
                            MediaJobDispatcher dispatcher = new();
                            dispatcher.DispatchJob<VideoEncodeJob>(
                                libraryId: folderLibrary.LibraryId,
                                folderId: folderLibrary.FolderId,
                                id: row.MediaId.ToString(),
                                inputFile: inputFile
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

        mediaContext.IncompleteEncodes.Remove(entity: row);
        await mediaContext.SaveChangesAsync();

        return Ok(value: new StatusResponseDto<string> { Message = "Re-queued", Status = "success" });
    }

    [HttpDelete]
    [Route(template: "queue/incomplete/{id:int}")]
    public async Task<IActionResult> DeleteIncompleteEncode(int id)
    {
        IncompleteEncode? row = await mediaContext.IncompleteEncodes.FindAsync(keyValues: id);
        if (row is null)
            return NotFoundResponse(detail: "Incomplete encode record not found");

        mediaContext.IncompleteEncodes.Remove(entity: row);
        await mediaContext.SaveChangesAsync();

        return Ok(
            value: new StatusResponseDto<string>
            {
                Message = "Incomplete encode record removed",
                Status = "success",
            }
        );
    }

    [HttpDelete]
    [Route(template: "queue/incomplete")]
    public async Task<IActionResult> DeleteAllIncompleteEncodes()
    {
        int removedCount = await mediaContext.IncompleteEncodes.ExecuteDeleteAsync();

        return Ok(
            value: new StatusResponseDto<int>
            {
                Data = removedCount,
                Message = "Incomplete encode records removed",
                Status = "success",
            }
        );
    }
}

/// <summary>
/// A queue row paired with the job deserialized from its payload. The row is the
/// authority on scheduling state (id, priority, reservation); the payload only
/// describes the work that was requested.
/// </summary>
internal sealed record QueueJobEntry(QueueJob Row, VideoEncodeJob? Job);

public class QueueJobDto
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "payload_id")]
    public string PayloadId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty(propertyName: "input_file")]
    public string InputFile { get; set; } = string.Empty;

    [JsonProperty(propertyName: "profile")]
    public string? Profile { get; set; }

    [JsonProperty(propertyName: "priority")]
    public int Priority { get; set; }
}

public class PatchQueueItemDto
{
    [JsonProperty(propertyName: "priority")]
    public int Priority { get; set; }
}

public class ReorderQueueDto
{
    [JsonProperty(propertyName: "queue_name")]
    public string QueueName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "ordered_job_ids")]
    public List<int> OrderedJobIds { get; set; } = [];
}

public class IncompleteEncodeDto
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "media_id")]
    public long MediaId { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "missing_renditions")]
    public string[] MissingRenditions { get; set; } = [];

    [JsonProperty(propertyName: "last_error")]
    public string? LastError { get; set; }

    [JsonProperty(propertyName: "attempts_made")]
    public int AttemptsMade { get; set; }

    [JsonProperty(propertyName: "last_seen_at")]
    public DateTime LastSeenAt { get; set; }
}
