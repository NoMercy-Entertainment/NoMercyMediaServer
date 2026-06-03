using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Database;
using NoMercy.Database.Activity;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercyQueue;
using Serilog.Events;
using Configuration = NoMercy.Database.Models.Common.Configuration;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

[ApiController]
[Tags("Dashboard Configuration")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/configuration", Order = 10)]
public class ConfigurationController(
    MediaContext mediaContext,
    AppDbContext appContext,
    QueueRunner queueRunner,
    IActivityLogger activityLogger
) : BaseController
{
    [HttpGet]
    public IActionResult Index()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view configuration");

        return Ok(
            new ConfigDto
            {
                Data = new()
                {
                    InternalServerPort = Config.InternalServerPort,
                    ExternalServerPort = Config.ExternalServerPort,
                    LibraryWorkers = Config.LibraryWorkers.Value,
                    ImportWorkers = Config.ImportWorkers.Value,
                    ExtrasWorkers = Config.ExtrasWorkers.Value,
                    EncoderWorkers = Config.EncoderWorkers.Value,
                    CronWorkers = Config.CronWorkers.Value,
                    ImageWorkers = Config.ImageWorkers.Value,
                    FileWorkers = Config.FileWorkers.Value,
                    MusicWorkers = Config.MusicWorkers.Value,
                    ServerName = DeviceName(),
                    Swagger = Config.Swagger,
                    AllowAdultContent = Config.ShowAdultContent,
                },
            }
        );
    }

    [NonAction]
    private string DeviceName()
    {
        Configuration? device = appContext.Configuration.FirstOrDefault(device =>
            device.Key == "serverName"
        );
        return device?.Value ?? Environment.MachineName;
    }

    /// <summary>
    /// Belt-and-suspenders persist for worker counts. Writes the value to the
    /// Configuration table directly AND tells the QueueRunner to resize live
    /// workers. The previous flow only called QueueRunner.SetWorkerCount,
    /// which short-circuits with a no-op when the new count equals the
    /// current count — leaving the DB stale and the value resetting to the
    /// default on next boot. Two writes both have to land or the persistence
    /// silently rots.
    /// </summary>
    [NonAction]
    private async Task PersistWorkerCount(string queueName, int count, Guid userId)
    {
        string key = $"{queueName}Runners";
        await appContext
            .Configuration.Upsert(
                new Configuration
                {
                    Key = key,
                    Value = count.ToString(),
                    ModifiedBy = userId,
                }
            )
            .On(c => c.Key)
            .WhenMatched((_, n) => new() { Value = n.Value, ModifiedBy = n.ModifiedBy })
            .RunAsync();

        await queueRunner.SetWorkerCount(queueName, count, userId);
    }

    [HttpPost]
    public IActionResult Store()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to store configuration");

        return Ok(new PlaceholderResponse { Data = [] });
    }

    [HttpPatch]
    public async Task<IActionResult> Update([FromBody] ConfigDtoData request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update configuration");

        Guid userId = User.UserId();
        List<(string key, object? oldVal, object? newVal)> changes = [];

        if (request.InternalServerPort != 0)
        {
            int oldPort = Config.InternalServerPort;
            Config.InternalServerPort = request.InternalServerPort;
            await appContext
                .Configuration.Upsert(
                    new()
                    {
                        Key = "internalPort",
                        Value = request.InternalServerPort.ToString(),
                        ModifiedBy = userId,
                    }
                )
                .On(e => e.Key)
                .WhenMatched((o, n) => new() { Value = n.Value, ModifiedBy = n.ModifiedBy })
                .RunAsync();
            changes.Add(("internalPort", oldPort, request.InternalServerPort));
        }

        if (request.ExternalServerPort != 0)
        {
            int oldPort = Config.ExternalServerPort;
            Config.ExternalServerPort = request.ExternalServerPort;
            await appContext
                .Configuration.Upsert(
                    new()
                    {
                        Key = "externalPort",
                        Value = request.ExternalServerPort.ToString(),
                        ModifiedBy = userId,
                    }
                )
                .On(e => e.Key)
                .WhenMatched((o, n) => new() { Value = n.Value, ModifiedBy = n.ModifiedBy })
                .RunAsync();
            changes.Add(("externalPort", oldPort, request.ExternalServerPort));
        }

        if (request.LibraryWorkers is not null)
        {
            int oldCount = Config.LibraryWorkers.Value;
            int newCount = (int)request.LibraryWorkers;
            Config.LibraryWorkers = new(Config.LibraryWorkers.Key, newCount);
            await PersistWorkerCount(Config.LibraryWorkers.Key, newCount, userId);
            changes.Add((Config.LibraryWorkers.Key, oldCount, newCount));
        }

        if (request.ImportWorkers is not null)
        {
            int oldCount = Config.ImportWorkers.Value;
            int newCount = (int)request.ImportWorkers;
            Config.ImportWorkers = new(Config.ImportWorkers.Key, newCount);
            await PersistWorkerCount(Config.ImportWorkers.Key, newCount, userId);
            changes.Add((Config.ImportWorkers.Key, oldCount, newCount));
        }

        if (request.ExtrasWorkers is not null)
        {
            int oldCount = Config.ExtrasWorkers.Value;
            int newCount = (int)request.ExtrasWorkers;
            Config.ExtrasWorkers = new(Config.ExtrasWorkers.Key, newCount);
            await PersistWorkerCount(Config.ExtrasWorkers.Key, newCount, userId);
            changes.Add((Config.ExtrasWorkers.Key, oldCount, newCount));
        }

        if (request.EncoderWorkers is not null)
        {
            int oldCount = Config.EncoderWorkers.Value;
            int newCount = (int)request.EncoderWorkers;
            Config.EncoderWorkers = new(Config.EncoderWorkers.Key, newCount);
            await PersistWorkerCount(Config.EncoderWorkers.Key, newCount, userId);
            changes.Add((Config.EncoderWorkers.Key, oldCount, newCount));
        }

        if (request.CronWorkers is not null)
        {
            int oldCount = Config.CronWorkers.Value;
            int newCount = (int)request.CronWorkers;
            Config.CronWorkers = new(Config.CronWorkers.Key, newCount);
            await PersistWorkerCount(Config.CronWorkers.Key, newCount, userId);
            changes.Add((Config.CronWorkers.Key, oldCount, newCount));
        }

        if (request.ImageWorkers is not null)
        {
            int oldCount = Config.ImageWorkers.Value;
            int newCount = (int)request.ImageWorkers;
            Config.ImageWorkers = new(Config.ImageWorkers.Key, newCount);
            await PersistWorkerCount(Config.ImageWorkers.Key, newCount, userId);
            changes.Add((Config.ImageWorkers.Key, oldCount, newCount));
        }

        if (request.FileWorkers is not null)
        {
            int oldCount = Config.FileWorkers.Value;
            int newCount = (int)request.FileWorkers;
            Config.FileWorkers = new(Config.FileWorkers.Key, newCount);
            await PersistWorkerCount(Config.FileWorkers.Key, newCount, userId);
            changes.Add((Config.FileWorkers.Key, oldCount, newCount));
        }

        if (request.MusicWorkers is not null)
        {
            int oldCount = Config.MusicWorkers.Value;
            int newCount = (int)request.MusicWorkers;
            Config.MusicWorkers = new(Config.MusicWorkers.Key, newCount);
            await PersistWorkerCount(Config.MusicWorkers.Key, newCount, userId);
            changes.Add((Config.MusicWorkers.Key, oldCount, newCount));
        }

        if (request.Swagger is not null)
        {
            bool oldSwagger = Config.Swagger;
            Config.Swagger = (bool)request.Swagger;
            await appContext
                .Configuration.Upsert(
                    new()
                    {
                        Key = "swagger",
                        Value = Config.Swagger.ToString(),
                        ModifiedBy = User.UserId(),
                    }
                )
                .On(e => e.Key)
                .WhenMatched(
                    (o, n) => new() { Value = Config.Swagger.ToString(), ModifiedBy = n.ModifiedBy }
                )
                .RunAsync();
            changes.Add(("swagger", oldSwagger, (bool)request.Swagger));
        }

        if (request.AllowAdultContent is not null)
        {
            bool oldAllowAdult = Config.ShowAdultContent;
            Config.AllowAdultContent = request.AllowAdultContent;
            await appContext
                .Configuration.Upsert(
                    new()
                    {
                        Key = "allowAdultContent",
                        Value = Config.ShowAdultContent.ToString(),
                        ModifiedBy = userId,
                    }
                )
                .On(e => e.Key)
                .WhenMatched((o, n) => new() { Value = n.Value, ModifiedBy = n.ModifiedBy })
                .RunAsync();
            changes.Add(("allowAdultContent", oldAllowAdult, (bool)request.AllowAdultContent));
        }

        if (request.ServerName is not null)
        {
            string oldName = DeviceName();
            await appContext
                .Configuration.Upsert(
                    new()
                    {
                        Key = "serverName",
                        Value = request.ServerName,
                        ModifiedBy = User.UserId(),
                    }
                )
                .On(e => e.Key)
                .WhenMatched(
                    (o, n) => new() { Value = request.ServerName, ModifiedBy = n.ModifiedBy }
                )
                .RunAsync();
            changes.Add(("serverName", oldName, request.ServerName));
        }

        foreach ((string key, object? oldVal, object? newVal) in changes)
        {
            try
            {
                await activityLogger.LogConfigurationAsync(
                    "config.server_changed",
                    userId,
                    Ulid.Empty,
                    configKey: key,
                    oldValue: oldVal,
                    newValue: newVal
                );
            }
            catch (Exception ex)
            {
                Logger.Setup($"Failed to log config change: {ex.Message}", LogEventLevel.Warning);
            }
        }

        return Ok(
            new StatusResponseDto<string>
            {
                Message = "Configuration updated successfully",
                Status = "success",
                Args = [],
            }
        );
    }

    [HttpGet]
    [Route("languages")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Languages()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view languages");

        List<Language> languages = await mediaContext.Languages.ToListAsync();

        return Ok(
            languages
                .Select(language => new LanguageDto
                {
                    Id = language.Id,
                    Iso6391 = language.Iso6391,
                    EnglishName = language.EnglishName,
                    Name = language.Name,
                })
                .ToList()
        );
    }

    [HttpGet]
    [Route("countries")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Countries()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view countries");

        List<Country> countries = await mediaContext.Countries.ToListAsync();

        return Ok(
            countries
                .Select(country => new CountryDto
                {
                    Name = country.EnglishName,
                    Code = country.Iso31661,
                })
                .ToList()
        );
    }
}
