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

using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Management;
using NoMercy.Api.Middleware;
using NoMercy.Database;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Monitoring;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Status;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Plugins.Abstractions;
using NoMercy.Setup.Server;
using NoMercy.Storage;
using NoMercyQueue;
using Configuration = NoMercy.Database.Models.Common.Configuration;

using Microsoft.Extensions.Logging;
namespace NoMercy.Api.Controllers;

[ApiController]
[Route(template: "manage")]
[AllowAnonymous]
[LocalhostOnly]
[Tags(tags: "Management")]
public class ManagementController(
    ILogger<ManagementController> logger,
    ResourceMonitor resourceMonitor,
    IHostApplicationLifetime appLifetime,
    AppDbContext appContext,
    QueueRunner queueRunner,
    IPluginManager pluginManager,
    AppProcessManager appProcessManager,
    SetupState setupState,
    INetworkDiscovery networkDiscovery,
    ISessionManager sessionManager,
    IStorageDriver storageDriver,
    IStorage storage,
    IDbContextFactory<QueueContext> queueContextFactory,
    IBootStatus bootStatus,
    IUpdateStatus updateStatus,
    RuntimeServerSettings runtimeSettings
) : BaseController
{
    [HttpGet(template: "status")]
    [ProducesResponseType(type: typeof(ManagementStatusDto), statusCode: StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        Configuration? serverNameConfig = appContext.Configuration.FirstOrDefault(predicate: c =>
            c.Key == "serverName"
        );
        string serverName = serverNameConfig?.Value ?? Environment.MachineName;

        return Ok(
            value: new ManagementStatusDto
            {
                Status = bootStatus.IsStarted ? "running" : "starting",
                ServerName = serverName,
                Version = Software.GetReleaseVersion(),
                Platform = Info.Platform,
                Architecture = Info.Architecture,
                Os = $"{Info.Platform} {Info.OsVersion}",
                UptimeSeconds = (long)(DateTime.UtcNow - Info.StartTime).TotalSeconds,
                StartTime = Info.StartTime,
                IsDev = Config.IsDev,
                AutoStart = AutoStartupManager.IsEnabled(),
                IsDocker = Screen.IsDocker,
                UpdateAvailable = updateStatus.UpdateAvailable,
                RestartNeeded = updateStatus.RestartNeeded,
                LatestVersion = updateStatus.LatestVersion,
                SetupPhase = setupState.CurrentPhase.ToString(),
                InternalAddress = networkDiscovery.InternalAddress,
                ExternalAddress = networkDiscovery.ExternalAddress,
                AppStatus = new()
                {
                    Running = appProcessManager.IsRunning,
                    Pid = appProcessManager.ProcessId,
                },
            }
        );
    }

    [HttpGet(template: "logs")]
    [ProducesResponseType(type: typeof(List<LogEntry>), statusCode: StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int tail = 100,
        [FromQuery] string? types = null,
        [FromQuery] string? levels = null
    )
    {
        string[]? typeFilter = types?.Split(
            separator: ',',
            options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        string[]? levelFilter = levels?.Split(
            separator: ',',
            options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        List<LogEntry> logs = await Logger.GetLogs(
            limit: tail,
            filter: entry =>
            {
                bool typeMatch =
                    typeFilter is null
                    || typeFilter.Length == 0
                    || typeFilter.Any(predicate: t =>
                        string.Equals(a: t, b: entry.Type, comparisonType: StringComparison.OrdinalIgnoreCase)
                    );
                bool levelMatch =
                    levelFilter is null
                    || levelFilter.Length == 0
                    || levelFilter.Contains(
                        value: entry.Level.ToString(),
                        comparer: StringComparer.OrdinalIgnoreCase
                    );

                return typeMatch && levelMatch;
            }
        );

        return Ok(value: logs);
    }

    [HttpGet(template: "logs/stream")]
    public async Task StreamLogs(
        [FromQuery] int backfill = 50,
        CancellationToken cancellationToken = default
    )
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        await Response.StartAsync(cancellationToken: cancellationToken);

        // Bounded channel: drops oldest if client falls behind
        Channel<LogEntry> channel = Channel.CreateBounded<LogEntry>(
            options: new BoundedChannelOptions(capacity: 500)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            }
        );

        void OnLogEmitted(LogEntry entry) => channel.Writer.TryWrite(item: entry);

        // Subscribe before backfill so no events are lost during backfill writes
        Logger.LogEmitted += OnLogEmitted;

        try
        {
            // Send backfill of recent log entries
            List<LogEntry> recentLogs = await Logger.GetLogs(limit: backfill);
            foreach (LogEntry entry in recentLogs)
            {
                string json = JsonConvert.SerializeObject(value: entry);
                await Response.WriteAsync(text: $"data: {json}\n\n", cancellationToken: cancellationToken);
            }

            await Response.Body.FlushAsync(cancellationToken: cancellationToken);

            // Consume live events from the channel
            await foreach (LogEntry entry in channel.Reader.ReadAllAsync(cancellationToken: cancellationToken))
            {
                string json = JsonConvert.SerializeObject(value: entry);
                await Response.WriteAsync(text: $"data: {json}\n\n", cancellationToken: cancellationToken);
                await Response.Body.FlushAsync(cancellationToken: cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        finally
        {
            Logger.LogEmitted -= OnLogEmitted;
            channel.Writer.TryComplete();
        }
    }

    [HttpGet(template: "activity")]
    [ProducesResponseType(type: typeof(ManagementActivityDto), statusCode: StatusCodes.Status200OK)]
    public IActionResult GetActivity()
    {
        int activeStreams = sessionManager.ActiveSessionCount;

        IReadOnlyDictionary<string, Thread> activeThreads = queueRunner.GetActiveWorkerThreads();
        int activeEncodes = activeThreads.Count(predicate: t =>
            t.Key.StartsWith(value: "encoder", comparisonType: StringComparison.OrdinalIgnoreCase)
        );

        // All encode jobs in V3 are split/resumable, so killing mid-encode is safe.
        // Streams are never "safe to interrupt" — stopping one ends playback for that user.
        bool canInterruptSafely = activeStreams == 0;

        return Ok(
            value: new ManagementActivityDto
            {
                ActiveStreams = activeStreams,
                ActiveEncodes = activeEncodes,
                CanInterruptSafely = canInterruptSafely,
            }
        );
    }

    [HttpPost(template: "stop")]
    public IActionResult Stop()
    {
        appLifetime.StopApplication();
        return Ok(value: new { status = "ok", message = "Server is shutting down" });
    }

    [HttpPost(template: "restart")]
    public IActionResult Restart()
    {
        appLifetime.StopApplication();
        return Ok(value: new { status = "ok", message = "Server is restarting" });
    }

    [HttpPost(template: "update")]
    [ProducesResponseType(statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DownloadUpdate()
    {
        try
        {
            string tempPath = AppFiles.ServerTempExePath;

            if (storageDriver.FileExists(path: tempPath))
            {
                logger.LogInformation(message: "Update already staged, skipping download.");
                return Ok(
                    value: new
                    {
                        status = "ok",
                        message = "Update already staged.",
                        path = tempPath,
                    }
                );
            }

            string? onDiskVersion = Software.GetFileVersion(driver: storageDriver, exePath: AppFiles.ServerExePath);
            string runningVersion = Software.GetReleaseVersion();
            if (
                onDiskVersion is not null
                && Version.TryParse(input: onDiskVersion, result: out Version? diskVer)
                && Version.TryParse(input: runningVersion, result: out Version? runVer)
                && diskVer > runVer
            )
            {
                logger.LogInformation(message: "Binary on disk is already {OnDiskVersion} (running {RunningVersion}), restart will apply the update.", args: [onDiskVersion, runningVersion]);
                return Ok(
                    value: new
                    {
                        status = "ok",
                        message = $"Binary on disk is already {onDiskVersion}, restart needed.",
                    }
                );
            }

            logger.LogInformation(message: "Downloading server update on demand...");
            ServerUpdateResult result = await new Binaries(
                driver: storageDriver,
                storage: storage
            ).DownloadServerUpdate();

            switch (result)
            {
                case ServerUpdateResult.AlreadyUpToDate:
                    return Ok(value: new { status = "ok", message = "Server is already up to date." });

                case ServerUpdateResult.UseInstaller:
                    return Ok(
                        value: new
                        {
                            status = "ok",
                            message = "This is an installer deployment. Use the installer to update.",
                            use_installer = true,
                            latest_version = updateStatus.LatestVersion,
                        }
                    );

                case ServerUpdateResult.RestartNeeded:
                    return Ok(
                        value: new
                        {
                            status = "ok",
                            message = "Binary on disk is already the latest version, restart needed to apply.",
                        }
                    );

                case ServerUpdateResult.NoAssetFound:
                    return InternalServerErrorResponse(
                        detail: "No suitable update asset found for the current platform."
                    );

                case ServerUpdateResult.Downloaded:
                    if (!storageDriver.FileExists(path: tempPath))
                    {
                        logger.LogError(message: "Server update staged file missing at {TempPath} after successful download", args: tempPath);
                        return InternalServerErrorResponse(
                            detail: "Download completed but staged file not found. This may be caused by antivirus software quarantining the file."
                        );
                    }

                    long fileSize = storageDriver.GetFileSize(path: tempPath);
                    logger.LogInformation(message: "Server update staged at {TempPath} ({FileSize} bytes)", args: [tempPath, fileSize]);
                    return Ok(
                        value: new
                        {
                            status = "ok",
                            message = "Update downloaded and staged.",
                            path = tempPath,
                            size = fileSize,
                        }
                    );

                default:
                    return InternalServerErrorResponse(detail: "Unexpected update result.");
            }
        }
        catch (Exception e)
        {
            logger.LogError(message: "Failed to download update: {Message}", args: e.Message);
            return InternalServerErrorResponse(detail: "Failed to download update");
        }
    }

    [HttpGet(template: "autostart")]
    [ProducesResponseType(type: typeof(AutoStartDto), statusCode: StatusCodes.Status200OK)]
    public IActionResult GetAutoStart()
    {
        return Ok(value: new AutoStartDto { Enabled = AutoStartupManager.IsEnabled() });
    }

    [HttpPost(template: "autostart")]
    [ProducesResponseType(statusCode: StatusCodes.Status200OK)]
    public IActionResult SetAutoStart([FromBody] AutoStartDto request)
    {
        if (request.Enabled)
            AutoStartupManager.Initialize();
        else
            AutoStartupManager.Remove();

        return Ok(value: new AutoStartDto { Enabled = AutoStartupManager.IsEnabled() });
    }

    [HttpGet(template: "config")]
    [ProducesResponseType(type: typeof(ManagementConfigDto), statusCode: StatusCodes.Status200OK)]
    public IActionResult GetConfig()
    {
        Configuration? serverNameConfig = appContext.Configuration.FirstOrDefault(predicate: c =>
            c.Key == "serverName"
        );

        return Ok(
            value: new ManagementConfigDto
            {
                InternalPort = runtimeSettings.InternalServerPort,
                ExternalPort = runtimeSettings.ExternalServerPort,
                ServerName = serverNameConfig?.Value ?? Environment.MachineName,
                LibraryWorkers = runtimeSettings.LibraryWorkers.Value,
                ImportWorkers = runtimeSettings.ImportWorkers.Value,
                ExtrasWorkers = runtimeSettings.ExtrasWorkers.Value,
                EncoderWorkers = runtimeSettings.EncoderWorkers.Value,
                CronWorkers = runtimeSettings.CronWorkers.Value,
                ImageWorkers = runtimeSettings.ImageWorkers.Value,
                FileWorkers = runtimeSettings.FileWorkers.Value,
                MusicWorkers = runtimeSettings.MusicWorkers.Value,
                Swagger = runtimeSettings.Swagger,
            }
        );
    }

    [HttpPut(template: "config")]
    [ProducesResponseType(statusCode: StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateConfig([FromBody] ManagementConfigUpdateDto request)
    {
        if (request.LibraryWorkers is not null)
        {
            runtimeSettings.LibraryWorkers = new(
                key: runtimeSettings.LibraryWorkers.Key,
                value: (int)request.LibraryWorkers
            );
            await queueRunner.SetWorkerCount(
                name: runtimeSettings.LibraryWorkers.Key,
                max: (int)request.LibraryWorkers,
                userId: null
            );
        }

        if (request.ImportWorkers is not null)
        {
            runtimeSettings.ImportWorkers = new(
                key: runtimeSettings.ImportWorkers.Key,
                value: (int)request.ImportWorkers
            );
            await queueRunner.SetWorkerCount(
                name: runtimeSettings.ImportWorkers.Key,
                max: (int)request.ImportWorkers,
                userId: null
            );
        }

        if (request.ExtrasWorkers is not null)
        {
            runtimeSettings.ExtrasWorkers = new(
                key: runtimeSettings.ExtrasWorkers.Key,
                value: (int)request.ExtrasWorkers
            );
            await queueRunner.SetWorkerCount(
                name: runtimeSettings.ExtrasWorkers.Key,
                max: (int)request.ExtrasWorkers,
                userId: null
            );
        }

        if (request.EncoderWorkers is not null)
        {
            runtimeSettings.EncoderWorkers = new(
                key: runtimeSettings.EncoderWorkers.Key,
                value: (int)request.EncoderWorkers
            );
            await queueRunner.SetWorkerCount(
                name: runtimeSettings.EncoderWorkers.Key,
                max: (int)request.EncoderWorkers,
                userId: null
            );
        }

        if (request.CronWorkers is not null)
        {
            runtimeSettings.CronWorkers = new(
                key: runtimeSettings.CronWorkers.Key,
                value: (int)request.CronWorkers
            );
            await queueRunner.SetWorkerCount(
                name: runtimeSettings.CronWorkers.Key,
                max: (int)request.CronWorkers,
                userId: null
            );
        }

        if (request.ImageWorkers is not null)
        {
            runtimeSettings.ImageWorkers = new(
                key: runtimeSettings.ImageWorkers.Key,
                value: (int)request.ImageWorkers
            );
            await queueRunner.SetWorkerCount(
                name: runtimeSettings.ImageWorkers.Key,
                max: (int)request.ImageWorkers,
                userId: null
            );
        }

        if (request.FileWorkers is not null)
        {
            runtimeSettings.FileWorkers = new(
                key: runtimeSettings.FileWorkers.Key,
                value: (int)request.FileWorkers
            );
            await queueRunner.SetWorkerCount(
                name: runtimeSettings.FileWorkers.Key,
                max: (int)request.FileWorkers,
                userId: null
            );
        }

        if (request.MusicWorkers is not null)
        {
            runtimeSettings.MusicWorkers = new(
                key: runtimeSettings.MusicWorkers.Key,
                value: (int)request.MusicWorkers
            );
            await queueRunner.SetWorkerCount(
                name: runtimeSettings.MusicWorkers.Key,
                max: (int)request.MusicWorkers,
                userId: null
            );
        }

        if (request.ServerName is not null)
        {
            Configuration? existing = await appContext.Configuration.FirstOrDefaultAsync(predicate: c =>
                c.Key == "serverName"
            );

            if (existing is not null)
            {
                existing.Value = request.ServerName;
            }
            else
            {
                appContext.Configuration.Add(
                    entity: new() { Key = "serverName", Value = request.ServerName }
                );
            }

            await appContext.SaveChangesAsync();
        }

        return Ok(value: new { status = "ok", message = "Configuration updated" });
    }

    [HttpGet(template: "plugins")]
    [ProducesResponseType(statusCode: StatusCodes.Status200OK)]
    public IActionResult GetPlugins()
    {
        IReadOnlyList<PluginInfo> plugins = pluginManager.GetInstalledPlugins();

        return Ok(
            value: plugins.Select(selector: p => new
            {
                id = p.Id,
                name = p.Name,
                description = p.Description,
                version = p.Version.ToString(),
                status = p.Status.ToString().ToLowerInvariant(),
                author = p.Author,
                project_url = p.ProjectUrl,
            })
        );
    }

    [HttpGet(template: "queue")]
    [ProducesResponseType(type: typeof(ManagementQueueStatusDto), statusCode: StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueueStatus()
    {
        await using QueueContext queueContext = await queueContextFactory.CreateDbContextAsync();

        int pendingJobs = await queueContext.QueueJobs.CountAsync();
        int failedJobs = await queueContext.FailedJobs.CountAsync();

        IReadOnlyDictionary<string, Thread> activeThreads = queueRunner.GetActiveWorkerThreads();

        Dictionary<string, ManagementWorkerStatusDto> workers = new();
        foreach (
            IGrouping<string, KeyValuePair<string, Thread>> group in activeThreads.GroupBy(keySelector: t =>
                t.Key.Split(separator: '-')[0]
            )
        )
        {
            workers[key: group.Key] = new() { ActiveThreads = group.Count() };
        }

        return Ok(
            value: new ManagementQueueStatusDto
            {
                Workers = workers,
                PendingJobs = pendingJobs,
                FailedJobs = failedJobs,
            }
        );
    }

    [HttpGet(template: "resources")]
    [ProducesResponseType(statusCode: StatusCodes.Status200OK)]
    public IActionResult GetResources()
    {
        try
        {
            Resource? resource = resourceMonitor.Monitor();
            List<ResourceMonitorDto> storage = StorageMonitor.Main();

            return Ok(
                value: new
                {
                    cpu = resource.Cpu,
                    gpu = resource.Gpu,
                    memory = resource.Memory,
                    storage,
                }
            );
        }
        catch (Exception)
        {
            return InternalServerErrorResponse(detail: "Resource monitor failed");
        }
    }

    [HttpGet(template: "app/status")]
    [ProducesResponseType(type: typeof(AppProcessStatusDto), statusCode: StatusCodes.Status200OK)]
    public IActionResult GetAppStatus()
    {
        return Ok(
            value: new AppProcessStatusDto
            {
                Running = appProcessManager.IsRunning,
                Pid = appProcessManager.ProcessId,
            }
        );
    }

    [HttpPost(template: "app/start")]
    [ProducesResponseType(statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status409Conflict)]
    [ProducesResponseType(statusCode: StatusCodes.Status500InternalServerError)]
    public IActionResult StartApp()
    {
        if (appProcessManager.IsRunning)
            return ConflictResponse(detail: "App is already running");

        bool started = appProcessManager.Start();

        if (!started)
            return InternalServerErrorResponse(detail: "Failed to start app — binary not found");

        return Ok(value: new { status = "ok", message = "App started" });
    }

    [HttpPost(template: "app/stop")]
    [ProducesResponseType(statusCode: StatusCodes.Status200OK)]
    public IActionResult StopApp()
    {
        bool stopped = appProcessManager.Stop();

        return Ok(value: new { status = "ok", message = stopped ? "App stopped" : "App was not running" });
    }
}
