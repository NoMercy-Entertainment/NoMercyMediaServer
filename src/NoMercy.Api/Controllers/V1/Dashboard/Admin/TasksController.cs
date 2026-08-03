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
using NoMercy.Api.Services;
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
using NoMercy.Encoder.Profiles;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs.Support;
using NoMercy.MediaProcessing.Jobs.SubtitleJobs;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Queue.MediaServer;
using NoMercyQueue;
using NoMercyQueue.Core;
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
            .ThenBy(j => j.Id)
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
    /// The presets an encode will run with: every preset linked to its folder,
    /// narrowed to one only when the job named it.
    ///
    /// <para>This is <see cref="VideoEncodeJob.HandleInitialRunAsync"/>'s own
    /// rule, and it has to stay that way or the card describes work the encoder
    /// is not doing. A folder carrying "4K HDR HEVC" and "1080p SDR HEVC"
    /// produces both in one coordinated run, so naming either alone told half
    /// the story. <c>IsDefault</c> selects nothing here — the encoder never
    /// reads it. A named preset the folder does not link runs nothing at all,
    /// which is an empty list rather than a fallback.</para>
    /// </summary>
    private static Ulid[] PresetsFor(VideoEncodeJob job, Dictionary<Ulid, Folder> folderById)
    {
        Ulid[] linked =
            folderById
                .GetValueOrDefault(job.FolderId)
                ?.EncodingPresetFolders.Select(link => link.PresetId)
                .ToArray()
            ?? [];

        if (job.PresetId is not Ulid named)
            return linked;

        return linked.Contains(named) ? [named] : [];
    }

    /// <summary>
    /// What the encode will produce, resolved once per set of presets per
    /// request — a season's worth of episodes shares one folder's presets, and
    /// resolving each walks the inheritance chain and parses JSON.
    ///
    /// <para>Several presets are one merged answer, because they are one
    /// coordinated encode: the runner shares the analysis, the audio and the
    /// subtitles across them and writes a single master playlist listing every
    /// preset's renditions.</para>
    ///
    /// <para>A preset that has been deleted, or whose chain no longer resolves,
    /// leaves the plan off the card rather than failing the whole panel: the
    /// queue is the answer this endpoint owes, and the plan is an extra on top
    /// of it. The cache holds the null too, so a broken preset is attempted
    /// once.</para>
    /// </summary>
    private static QueueJobPlanDto? ResolvePlan(
        Ulid[] presetIds,
        IPresetLookup lookup,
        Dictionary<string, QueueJobPlanDto?> cache
    )
    {
        if (presetIds.Length == 0)
            return null;

        string key = string.Join('|', presetIds);
        if (cache.TryGetValue(key, out QueueJobPlanDto? cached))
            return cached;

        QueueJobPlanDto? plan;
        try
        {
            plan = QueueJobPlanMapper.Merge(
                presetIds
                    .Select(id =>
                        QueueJobPlanMapper.FromProfile(PresetResolver.Resolve(id, lookup))
                    )
                    .ToArray()
            );
        }
        catch (Exception exception)
        {
            Logger.Encoder($"Queue card plan unavailable for {key}: {exception.Message}");
            plan = null;
        }

        cache[key] = plan;
        return plan;
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

        // This panel polls, and the encoder queue is thousands of rows deep on a
        // library mid-encode. Tracking every one of them per poll is pure cost:
        // nothing here is written back.
        ImmutableList<QueueJob> jobs = queueContext
            .QueueJobs.AsNoTracking()
            .Where(j => j.Queue == "encoder")
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .ThenBy(j => j.Id)
            .ToImmutableList();

        // Each parsed payload stays paired with the row it came from: a payload
        // that fails to deserialize is dropped here, so indexing the unfiltered
        // row list by the parsed list's position would hand every later job the
        // preceding row's id, priority and reservation.
        //
        // Parsed once and then split. Deserializing again to find the rows that
        // are not encodes cost a second pass over every payload on the queue,
        // and this endpoint is polled.
        List<QueueJobEntry> parsedJobs = jobs.Select(row => new QueueJobEntry(
                row,
                ReadEncodeJob(row.Payload)
            ))
            .ToList();

        List<QueueJobEntry> encoderJobs = parsedJobs.Where(entry => entry.Job is not null).ToList();

        // Maintenance work sharing the encoder queue — preview rebuilds and
        // subtitle OCR — reported as itself rather than dressed up as an encode.
        // Hours of this can be queued at once, and a panel that shows none of it
        // is telling the operator the server is idle while it works.
        List<MaintenanceJob> maintenanceJobs = parsedJobs
            .Where(entry => entry.Job is null)
            .Select(entry => ReadMaintenanceJob(entry.Row))
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToList();

        await EnrichMaintenanceJobsAsync(maintenanceJobs);

        // Parse each job id once — Id is either an int (movie/episode) or a Guid (track).
        List<int> movieOrEpisodeIds = [];
        List<Guid> trackIds = [];

        foreach (VideoEncodeJob encoderJob in encoderJobs.Select(entry => entry.Job!))
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

        List<Ulid> folderIds = encoderJobs.Select(entry => entry.Job!.FolderId).Distinct().ToList();

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

        // The child task is where the real work — and the real ordering — lives.
        // A coordinator row is deleted and re-inserted on every poll, so neither
        // its id nor its CreatedAt says anything about when its encode was
        // dispatched or when it will run. Its child is enqueued once and never
        // re-created, so the child's autoincrement id IS the execution order the
        // runner will pick them up in.
        List<QueueJob> childRows = queueContext
            .QueueJobs.Where(j => j.Queue.StartsWith("encoder-"))
            .OrderBy(j => j.Id)
            .ToList();

        HashSet<string> activeMediaIds = [];
        Dictionary<string, int> executionOrder = [];

        foreach (QueueJob child in childRows)
        {
            string? mediaId = child.Payload.FromJson<EncodeTaskJob>()?.Id?.ToString();
            if (string.IsNullOrEmpty(mediaId))
                continue;

            // First child wins: a run dispatches its bundles in order, so the
            // earliest one still queued is when this encode gets its turn.
            executionOrder.TryAdd(mediaId, child.Id);

            if (child.ReservedAt is not null)
                activeMediaIds.Add(mediaId);
        }

        Dictionary<Ulid, string> presetNameById = folders
            .SelectMany(folder => folder.EncodingPresetFolders)
            .Where(link => link.Preset is not null)
            .GroupBy(link => link.PresetId)
            .ToDictionary(group => group.Key, group => group.First().Preset!.Name);

        DbPresetLookup presetLookup = new(mediaContext);
        Dictionary<string, QueueJobPlanDto?> planByPresetSet = [];

        QueueJobDto[] queueJobs = encoderJobs
            .Select(entry =>
            {
                VideoEncodeJob j = entry.Job!;
                Ulid[] presetIds = PresetsFor(j, folderById);

                return new QueueJobDto
                {
                    Id = entry.Row.Id,
                    Priority = entry.Row.Priority,
                    PayloadId = j.Id,
                    Title = ResolveTitle(j, movieById, episodeById, trackById),
                    Backdrop = ResolveArtwork(j, movieById, episodeById),
                    Type = j.GetType().Name,
                    Status = IsEncodeInFlight(
                        entry.Row.ReservedAt,
                        activeMediaIds.Contains(j.Id.ToString())
                    )
                        ? "running"
                        : "pending",
                    InputFile = j.InputFile,
                    Profile =
                        presetIds.Length == 0
                            ? null
                            : string.Join(
                                ", ",
                                presetIds.Select(id =>
                                    presetNameById.GetValueOrDefault(id, id.ToString())
                                )
                            ),
                    Plan = ResolvePlan(presetIds, presetLookup, planByPresetSet),
                };
            })
            .Concat(maintenanceJobs.Select(entry => entry.Dto))
            // Ordered here, not in SQL: both row columns SQL could sort on are
            // rewritten on every coordinator poll. Priority still leads, because
            // that is what the runner honours; within a priority the child id is
            // the order the runner will actually pick them up in.
            .OrderByDescending(dto => dto.Priority)
            .ThenBy(dto => executionOrder.GetValueOrDefault(dto.PayloadId, int.MaxValue))
            .ThenBy(dto => dto.Id)
            .ToArray();

        // Catch-up broadcast so a reloaded dashboard gets a card back for work
        // already in flight. Keyed off the reservation rather than the reported
        // status: a coordinator sleeping between poll wake-ups reports running
        // but has no live encoder behind it, and a card for it would only flap
        // in and out as the idle sweep collected it.
        HashSet<int> reservedRowIds = encoderJobs
            .Where(entry => entry.Row.ReservedAt is not null)
            .Select(entry => entry.Row.Id)
            .ToHashSet();

        if (EventBusProvider.IsConfigured)
        {
            foreach (QueueJobDto dto in queueJobs.Where(dto => reservedRowIds.Contains(dto.Id)))
            {
                // Lower-case keys are the encoder-progress contract — every other
                // producer on this channel emits them, and the serializer has no
                // naming strategy, so an anonymous object with PascalCase members
                // reached the dashboard as {Id, Status, Title}. Reading data.status
                // off that gives undefined, which is not in-flight, so the card was
                // never restored and the miss ran the handler's removal branch
                // instead — one refetch per running row per poll, each refetch
                // re-broadcasting the same unreadable payload.
                _ = EventBusProvider.Current.PublishAsync(
                    new EncodingProgressBroadcastedEvent
                    {
                        ProgressData = new
                        {
                            id = dto.PayloadId.ToInt(),
                            status = "running",
                            title = dto.Title,
                            message = "Encoding video",
                        },
                    }
                );
            }
        }

        return Ok(new DataResponseDto<QueueJobDto[]> { Data = queueJobs });
    }

    /// <summary>
    /// Whether an encode is actually being worked on, which is the line the
    /// dashboard splits "encoding now" from "waiting" on.
    ///
    /// <para>Carrying coordinator state is not that line. A coordinator stamps
    /// itself the moment it has decomposed and handed a bundle to the queue, and
    /// with one encoder runner two dozen of those sit behind each other having
    /// done nothing — calling them all in-flight said the whole season was
    /// encoding at once. What does mean work is a runner holding a reservation:
    /// either on the coordinator row itself, or on the child task that carries
    /// the ffmpeg process (<paramref name="hasReservedChild"/>).</para>
    /// </summary>
    /// <summary>
    /// The encode behind a queue row, or null when that row is not an encode.
    ///
    /// <para>The <c>encoder</c> queue is shared: subtitle OCR backfills and preview
    /// rebuilds run there too, deliberately, so maintenance work never competes with
    /// an encode for the same hardware. Deserializing straight into
    /// <see cref="VideoEncodeJob"/> did not reject those — they carry a
    /// <c>FolderId</c> of their own, so the result was a card with a real profile
    /// and nothing else: no title, no id, no artwork, no file. Payloads record
    /// their own type, so ask for it rather than assume it.</para>
    /// </summary>
    internal static VideoEncodeJob? ReadEncodeJob(string payload)
    {
        try
        {
            return SerializationHelper.Deserialize<object>(payload) as VideoEncodeJob;
        }
        catch (Exception)
        {
            // A payload written by a build that has since renamed or removed the
            // job type. Not this endpoint's problem to report.
            return null;
        }
    }

    /// <summary>
    /// A queue row that is maintenance rather than an encode, described as
    /// itself. Returns null for anything this endpoint has no name for, so an
    /// unrecognised job stays out of the panel instead of appearing as a blank.
    ///
    /// <para>The title here is the one the job carries — a folder name. It is a
    /// fallback: <see cref="EncoderQueue"/> replaces it with the library's own
    /// title, and adds the still, for every folder it can resolve.</para>
    /// </summary>
    internal static MaintenanceJob? ReadMaintenanceJob(QueueJob row)
    {
        object? job;
        try
        {
            job = SerializationHelper.Deserialize<object>(row.Payload);
        }
        catch (Exception)
        {
            return null;
        }

        (string kind, string title, string target, string hostFolder) = job switch
        {
            SpriteSheetUpgradeJob sprite => (
                "preview",
                sprite.Title,
                sprite.HostFolder,
                sprite.HostFolder
            ),
            SubtitleOcrBackfillJob ocr => (
                "subtitles",
                $"{ocr.MediaTitle} ({ocr.Language})",
                ocr.SupFileName,
                ocr.HostFolder
            ),
            _ => (string.Empty, string.Empty, string.Empty, string.Empty),
        };

        if (kind.Length == 0)
            return null;

        QueueJobDto dto = new()
        {
            Id = row.Id,
            Priority = row.Priority,
            // What the work is about and what does not move, which is what a list
            // key needs. Two OCR passes over one folder differ by their sup file,
            // so that is the key there; a preview rebuild is one per folder.
            PayloadId = target,
            Title = title,
            Type = job!.GetType().Name,
            Kind = kind,
            Status = row.ReservedAt is not null ? "running" : "pending",
            InputFile = target,
        };

        return new(dto, hostFolder);
    }

    /// <summary>
    /// Gives every maintenance row the title and the still of the file it is
    /// about. The job payload only carries a folder name, and a card built from
    /// that alone is a grey frame over a filename — for work that can occupy the
    /// server for hours. The folder is already the unique key of a
    /// <see cref="VideoFile"/>, so the picture and the proper title are one
    /// query away.
    /// </summary>
    private async Task EnrichMaintenanceJobsAsync(List<MaintenanceJob> maintenanceJobs)
    {
        List<string> hostFolders = maintenanceJobs
            .Select(entry => entry.HostFolder)
            .Where(folder => folder.Length > 0)
            .Distinct()
            .ToList();

        if (hostFolders.Count == 0)
            return;

        List<VideoFile> files = await mediaContext
            .VideoFiles.AsNoTracking()
            .Where(file => hostFolders.Contains(file.HostFolder))
            .Include(file => file.Episode)
                .ThenInclude(episode => episode!.Tv)
            .Include(file => file.Movie)
            .ToListAsync();

        Dictionary<string, VideoFile> fileByFolder = [];
        foreach (VideoFile file in files)
            fileByFolder.TryAdd(file.HostFolder, file);

        foreach (MaintenanceJob entry in maintenanceJobs)
        {
            if (fileByFolder.TryGetValue(entry.HostFolder, out VideoFile? file))
                ApplyMediaDetails(entry.Dto, file);
        }
    }

    /// <summary>
    /// Puts the library's own title and still onto a maintenance card. A file with
    /// neither an episode nor a movie behind it keeps the folder name the job
    /// carried: a card naming the folder still beats a card naming nothing.
    /// </summary>
    internal static void ApplyMediaDetails(QueueJobDto dto, VideoFile file)
    {
        if (file.Episode is { Tv: not null })
        {
            dto.Title = file.Episode.CreateTitle();
            dto.Backdrop = file.Episode.Still;
            return;
        }

        if (file.Movie is not null)
        {
            dto.Title = file.Movie.CreateTitle();
            dto.Backdrop = file.Movie.Backdrop;
        }
    }

    internal static bool IsEncodeInFlight(DateTime? reservedAt, bool hasReservedChild) =>
        reservedAt is not null || hasReservedChild;

    /// <summary>
    /// The artwork for a queued encode — a movie's backdrop or an episode's still,
    /// the same image the encoder's progress events carry once the job is running,
    /// so the card does not change picture when it starts.
    /// </summary>
    private static string? ResolveArtwork(
        VideoEncodeJob j,
        Dictionary<int, Movie> movieById,
        Dictionary<int, Episode> episodeById
    )
    {
        int intId = j.Id.ToInt();
        if (intId == 0)
            return null;

        if (movieById.TryGetValue(intId, out Movie? movie))
            return movie.Backdrop;

        return episodeById.TryGetValue(intId, out Episode? episode) ? episode.Still : null;
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

        foreach (string queue in EncoderQueueFamily)
            await QueueRunner.Current.Pause(queue);

        SuspendRunningEncodes();
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

        ResumeRunningEncodes();

        foreach (string queue in EncoderQueueFamily)
            await QueueRunner.Current.Resume(queue);
        return Ok(
            new StatusResponseDto<string> { Message = "Encoder queue resumed", Status = "success" }
        );
    }

    /// <summary>
    /// Every queue an encode occupies, because pausing is a promise about the
    /// machine and not about one table.
    /// <para>Pausing only <c>encoder</c> stopped the coordinators and left the
    /// queues their children run on untouched — and the children are what spawn
    /// ffmpeg. The dashboard reported the queue paused while two ffmpeg
    /// processes carried on at a third of the CPU, which is the opposite of what
    /// the button says.</para>
    /// </summary>
    private static readonly string[] EncoderQueueFamily =
    [
        QueueNames.Encoder,
        QueueNames.EncoderGpu,
        QueueNames.EncoderCpu,
    ];

    /// <summary>
    /// Suspends every ffmpeg the encoder currently has running.
    /// <para>Stopping the workers only stops the NEXT job being reserved. An
    /// encode already inside ffmpeg carried on, and a single file can be an
    /// hour of it — so "paused" meant the machine stayed at full tilt and the
    /// operator watched a task complete two hundred seconds after they asked
    /// for quiet. Pause has to reach the process, which is what the per-task
    /// pause on this controller has always done; this is the same mechanism
    /// applied to everything at once.</para>
    /// <para>Suspended, not killed. The process keeps its place and its partial
    /// output, so resuming continues the encode rather than starting it
    /// again.</para>
    /// </summary>
    private void SuspendRunningEncodes()
    {
        foreach (int jobId in processRegistry.ActiveJobIds)
        foreach (int processId in processRegistry.GetProcessIds(jobId))
            processThrottle.Suspend(processId);
    }

    /// <summary>
    /// Wakes what <see cref="SuspendRunningEncodes"/> put to sleep. Runs before
    /// the workers restart so a resumed encode is never racing a newly reserved
    /// one for the same hardware.
    /// </summary>
    private void ResumeRunningEncodes()
    {
        foreach (int jobId in processRegistry.ActiveJobIds)
        foreach (int processId in processRegistry.GetProcessIds(jobId))
            processThrottle.Resume(processId);
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
        QueueRunner? runner = QueueRunner.Current;
        bool paused = runner is not null && EncoderQueueFamily.All(queue => runner.IsPaused(queue));
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

    [HttpDelete]
    [Route("queue/incomplete/{id:int}")]
    public async Task<IActionResult> DeleteIncompleteEncode(int id)
    {
        IncompleteEncode? row = await mediaContext.IncompleteEncodes.FindAsync(id);
        if (row is null)
            return NotFoundResponse("Incomplete encode record not found");

        mediaContext.IncompleteEncodes.Remove(row);
        await mediaContext.SaveChangesAsync();

        return Ok(
            new StatusResponseDto<string>
            {
                Message = "Incomplete encode record removed",
                Status = "success",
            }
        );
    }

    [HttpDelete]
    [Route("queue/incomplete")]
    public async Task<IActionResult> DeleteAllIncompleteEncodes()
    {
        int removedCount = await mediaContext.IncompleteEncodes.ExecuteDeleteAsync();

        return Ok(
            new StatusResponseDto<int>
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

/// <summary>
/// A maintenance row described as itself, alongside the media folder it works on.
/// The folder is kept separate from the DTO because it is not part of the
/// contract — it is the key the title and the still are looked up by.
/// </summary>
internal sealed record MaintenanceJob(QueueJobDto Dto, string HostFolder);

public class QueueJobDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("payload_id")]
    public string PayloadId { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The item's own artwork — a movie backdrop or an episode still. Carried on
    /// the queue row, not only on progress events, so a card can show the title
    /// it is about from the moment the job is queued rather than waiting for the
    /// first frame of encoding to arrive.
    /// </summary>
    [JsonProperty("backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("input_file")]
    public string InputFile { get; set; } = string.Empty;

    [JsonProperty("profile")]
    public string? Profile { get; set; }

    /// <summary>
    /// What this encode is going to produce, from the preset it will run with.
    /// Null on maintenance rows, and on an encode whose preset no longer
    /// resolves — a client shows the rest of the card either way.
    /// </summary>
    [JsonProperty("plan")]
    public QueueJobPlanDto? Plan { get; set; }

    [JsonProperty("priority")]
    public int Priority { get; set; }

    /// <summary>
    /// What kind of work this row is, when it is not an encode: <c>preview</c>
    /// for a scrub-sheet rebuild, <c>subtitles</c> for an OCR backfill. Null on a
    /// real encode, which is what every existing client already assumes it is
    /// reading — so an older build ignores this and keeps working.
    ///
    /// <para>These share the encoder queue on purpose, so they must not be shown
    /// as encodes: they have no media id, no artwork and no progress, and pushing
    /// them through the encode shape produced cards for titles that did not
    /// exist. Filtering them out instead went too far the other way — the server
    /// spends hours on this work and the panel claimed nothing was running.</para>
    /// </summary>
    [JsonProperty("kind")]
    public string? Kind { get; set; }
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
