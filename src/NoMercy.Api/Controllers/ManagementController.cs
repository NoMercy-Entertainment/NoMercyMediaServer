using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Management;
using NoMercy.Api.Middleware;
using NoMercy.Database;
using NoMercy.Helpers.Monitoring;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Newtonsoft.Json;
using NoMercy.Networking.Discovery;
using NoMercy.Plugins.Abstractions;
using NoMercy.Queue;
using NoMercy.NmSystem;
using NoMercy.NmSystem.FileSystem;
using NoMercy.Setup;
using Microsoft.Extensions.Hosting;
using Configuration = NoMercy.Database.Models.Common.Configuration;

namespace NoMercy.Api.Controllers;

[ApiController]
[Route("manage")]
[AllowAnonymous]
[LocalhostOnly]
[Tags("Management")]
public class ManagementController(
    IHostApplicationLifetime appLifetime,
    MediaContext mediaContext,
    QueueRunner queueRunner,
    IPluginManager pluginManager,
    AppProcessManager appProcessManager,
    SetupState setupState,
    INetworkDiscovery networkDiscovery) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(ManagementStatusDto), StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        Configuration? serverNameConfig = mediaContext.Configuration
            .FirstOrDefault(c => c.Key == "serverName");
        string serverName = serverNameConfig?.Value ?? Environment.MachineName;

        return Ok(new ManagementStatusDto
        {
            Status = Config.Started ? "running" : "starting",
            ServerName = serverName,
            Version = Software.GetReleaseVersion(),
            Platform = Info.Platform,
            Architecture = Info.Architecture,
            Os = $"{Info.Platform} {Info.OsVersion}",
            UptimeSeconds = (long)(DateTime.UtcNow - Info.StartTime).TotalSeconds,
            StartTime = Info.StartTime,
            IsDev = Config.IsDev,
            AutoStart = AutoStartupManager.IsEnabled(),
            UpdateAvailable = Config.UpdateAvailable,
            LatestVersion = Config.LatestVersion,
            SetupPhase = setupState.CurrentPhase.ToString(),
            InternalAddress = networkDiscovery.InternalAddress,
            ExternalAddress = networkDiscovery.ExternalAddress,
            AppStatus = new AppProcessStatusDto
            {
                Running = appProcessManager.IsRunning,
                Pid = appProcessManager.ProcessId
            }
        });
    }

    [HttpGet("logs")]
    [ProducesResponseType(typeof(List<LogEntry>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogs([FromQuery] int tail = 100,
        [FromQuery] string? types = null,
        [FromQuery] string? levels = null)
    {
        string[]? typeFilter = types?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string[]? levelFilter = levels?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        List<LogEntry> logs = await Logger.GetLogs(tail, entry =>
        {
            bool typeMatch = typeFilter is null || typeFilter.Length == 0 ||
                             typeFilter.Any(t => string.Equals(t, entry.Type, StringComparison.OrdinalIgnoreCase));
            bool levelMatch = levelFilter is null || levelFilter.Length == 0 ||
                              levelFilter.Contains(entry.Level.ToString(), StringComparer.OrdinalIgnoreCase);

            return typeMatch && levelMatch;
        });

        return Ok(logs);
    }

    [HttpGet("logs/stream")]
    public async Task StreamLogs([FromQuery] int backfill = 50, CancellationToken cancellationToken = default)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        await Response.StartAsync(cancellationToken);

        // Send backfill of recent log entries
        List<LogEntry> recentLogs = await Logger.GetLogs(backfill);
        foreach (LogEntry entry in recentLogs)
        {
            string json = JsonConvert.SerializeObject(entry);
            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
        }

        await Response.Body.FlushAsync(cancellationToken);

        // Subscribe to live log events
        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration ctr = cancellationToken.Register(() => tcs.TrySetResult());

        void OnLogEmitted(LogEntry entry)
        {
            try
            {
                string json = JsonConvert.SerializeObject(entry);
                byte[] data = System.Text.Encoding.UTF8.GetBytes($"data: {json}\n\n");
                Response.Body.WriteAsync(data, 0, data.Length, cancellationToken)
                    .ContinueWith(t => Response.Body.FlushAsync(cancellationToken), cancellationToken);
            }
            catch
            {
                tcs.TrySetResult();
            }
        }

        Logger.LogEmitted += OnLogEmitted;

        try
        {
            await tcs.Task;
        }
        finally
        {
            Logger.LogEmitted -= OnLogEmitted;
            ctr.Dispose();
        }
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        appLifetime.StopApplication();
        return Ok(new { status = "ok", message = "Server is shutting down" });
    }

    [HttpPost("restart")]
    public IActionResult Restart()
    {
        return Ok(new { status = "ok", message = "Restart is not yet implemented" });
    }

    [HttpPost("update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult ApplyUpdate()
    {
        string tempPath = AppFiles.ServerTempExePath;

        if (!System.IO.File.Exists(tempPath))
            return NotFound(new { status = "error", message = "No update has been downloaded" });

        try
        {
            string currentPath = AppFiles.ServerExePath;

            if (System.IO.File.Exists(currentPath))
                System.IO.File.Delete(currentPath);

            System.IO.File.Move(tempPath, currentPath);

            FilePermissions.SetExecutionPermissions(currentPath).GetAwaiter().GetResult();

            Logger.Setup("Server update applied. Restart required to use the new version.");

            return Ok(new { status = "ok", message = "Update applied. Restart the server to use the new version." });
        }
        catch (Exception e)
        {
            Logger.Setup($"Failed to apply update: {e.Message}", Serilog.Events.LogEventLevel.Error);
            return StatusCode(500, new { status = "error", message = $"Failed to apply update: {e.Message}" });
        }
    }

    [HttpGet("autostart")]
    [ProducesResponseType(typeof(AutoStartDto), StatusCodes.Status200OK)]
    public IActionResult GetAutoStart()
    {
        return Ok(new AutoStartDto
        {
            Enabled = AutoStartupManager.IsEnabled()
        });
    }

    [HttpPost("autostart")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult SetAutoStart([FromBody] AutoStartDto request)
    {
        if (request.Enabled)
            AutoStartupManager.Initialize();
        else
            AutoStartupManager.Remove();

        return Ok(new AutoStartDto
        {
            Enabled = AutoStartupManager.IsEnabled()
        });
    }

    [HttpGet("config")]
    [ProducesResponseType(typeof(ManagementConfigDto), StatusCodes.Status200OK)]
    public IActionResult GetConfig()
    {
        Configuration? serverNameConfig = mediaContext.Configuration
            .FirstOrDefault(c => c.Key == "serverName");

        return Ok(new ManagementConfigDto
        {
            InternalPort = Config.InternalServerPort,
            ExternalPort = Config.ExternalServerPort,
            ServerName = serverNameConfig?.Value ?? Environment.MachineName,
            LibraryWorkers = Config.LibraryWorkers.Value,
            ImportWorkers = Config.ImportWorkers.Value,
            ExtrasWorkers = Config.ExtrasWorkers.Value,
            EncoderWorkers = Config.EncoderWorkers.Value,
            CronWorkers = Config.CronWorkers.Value,
            ImageWorkers = Config.ImageWorkers.Value,
            FileWorkers = Config.FileWorkers.Value,
            MusicWorkers = Config.MusicWorkers.Value,
            Swagger = Config.Swagger
        });
    }

    [HttpPut("config")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateConfig([FromBody] ManagementConfigUpdateDto request)
    {
        if (request.LibraryWorkers is not null)
        {
            Config.LibraryWorkers = new(Config.LibraryWorkers.Key, (int)request.LibraryWorkers);
            await queueRunner.SetWorkerCount(Config.LibraryWorkers.Key, (int)request.LibraryWorkers, null);
        }

        if (request.ImportWorkers is not null)
        {
            Config.ImportWorkers = new(Config.ImportWorkers.Key, (int)request.ImportWorkers);
            await queueRunner.SetWorkerCount(Config.ImportWorkers.Key, (int)request.ImportWorkers, null);
        }

        if (request.ExtrasWorkers is not null)
        {
            Config.ExtrasWorkers = new(Config.ExtrasWorkers.Key, (int)request.ExtrasWorkers);
            await queueRunner.SetWorkerCount(Config.ExtrasWorkers.Key, (int)request.ExtrasWorkers, null);
        }

        if (request.EncoderWorkers is not null)
        {
            Config.EncoderWorkers = new(Config.EncoderWorkers.Key, (int)request.EncoderWorkers);
            await queueRunner.SetWorkerCount(Config.EncoderWorkers.Key, (int)request.EncoderWorkers, null);
        }

        if (request.CronWorkers is not null)
        {
            Config.CronWorkers = new(Config.CronWorkers.Key, (int)request.CronWorkers);
            await queueRunner.SetWorkerCount(Config.CronWorkers.Key, (int)request.CronWorkers, null);
        }

        if (request.ImageWorkers is not null)
        {
            Config.ImageWorkers = new(Config.ImageWorkers.Key, (int)request.ImageWorkers);
            await queueRunner.SetWorkerCount(Config.ImageWorkers.Key, (int)request.ImageWorkers, null);
        }

        if (request.FileWorkers is not null)
        {
            Config.FileWorkers = new(Config.FileWorkers.Key, (int)request.FileWorkers);
            await queueRunner.SetWorkerCount(Config.FileWorkers.Key, (int)request.FileWorkers, null);
        }

        if (request.MusicWorkers is not null)
        {
            Config.MusicWorkers = new(Config.MusicWorkers.Key, (int)request.MusicWorkers);
            await queueRunner.SetWorkerCount(Config.MusicWorkers.Key, (int)request.MusicWorkers, null);
        }

        if (request.ServerName is not null)
        {
            await mediaContext.Configuration.Upsert(new()
                {
                    Key = "serverName",
                    Value = request.ServerName
                })
                .On(e => e.Key)
                .WhenMatched((_, n) => new()
                {
                    Value = n.Value
                })
                .RunAsync();
        }

        return Ok(new { status = "ok", message = "Configuration updated" });
    }

    [HttpGet("plugins")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetPlugins()
    {
        IReadOnlyList<PluginInfo> plugins = pluginManager.GetInstalledPlugins();

        return Ok(plugins.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            description = p.Description,
            version = p.Version.ToString(),
            status = p.Status.ToString().ToLowerInvariant(),
            author = p.Author,
            project_url = p.ProjectUrl
        }));
    }

    [HttpGet("queue")]
    [ProducesResponseType(typeof(ManagementQueueStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueueStatus()
    {
        await using QueueContext queueContext = new();

        int pendingJobs = await queueContext.QueueJobs.CountAsync();
        int failedJobs = await queueContext.FailedJobs.CountAsync();

        IReadOnlyDictionary<string, Thread> activeThreads = queueRunner.GetActiveWorkerThreads();

        Dictionary<string, ManagementWorkerStatusDto> workers = new();
        foreach (IGrouping<string, KeyValuePair<string, Thread>> group in activeThreads
                     .GroupBy(t => t.Key.Split('-')[0]))
        {
            workers[group.Key] = new()
            {
                ActiveThreads = group.Count()
            };
        }

        return Ok(new ManagementQueueStatusDto
        {
            Workers = workers,
            PendingJobs = pendingJobs,
            FailedJobs = failedJobs
        });
    }

    [HttpGet("resources")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetResources()
    {
        try
        {
            Resource? resource = ResourceMonitor.Monitor();
            List<ResourceMonitorDto> storage = StorageMonitor.Main();

            return Ok(new
            {
                cpu = resource.Cpu,
                gpu = resource.Gpu,
                memory = resource.Memory,
                storage
            });
        }
        catch (Exception e)
        {
            return StatusCode(500, new { status = "error", message = $"Resource monitor failed: {e.Message}" });
        }
    }

    [HttpGet("app/status")]
    [ProducesResponseType(typeof(AppProcessStatusDto), StatusCodes.Status200OK)]
    public IActionResult GetAppStatus()
    {
        return Ok(new AppProcessStatusDto
        {
            Running = appProcessManager.IsRunning,
            Pid = appProcessManager.ProcessId
        });
    }

    [HttpPost("app/start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult StartApp()
    {
        if (appProcessManager.IsRunning)
            return Conflict(new { status = "already_running", message = "App is already running" });

        bool started = appProcessManager.Start();

        if (!started)
            return StatusCode(500, new { status = "error", message = "Failed to start app — binary not found" });

        return Ok(new { status = "ok", message = "App started" });
    }

    [HttpPost("app/stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult StopApp()
    {
        bool stopped = appProcessManager.Stop();

        return Ok(new
        {
            status = "ok",
            message = stopped ? "App stopped" : "App was not running"
        });
    }
}
