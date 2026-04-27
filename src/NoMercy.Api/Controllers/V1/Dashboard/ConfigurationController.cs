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

namespace NoMercy.Api.Controllers.V1.Dashboard;

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
            Config.LibraryWorkers = new(Config.LibraryWorkers.Key, (int)request.LibraryWorkers);
            await queueRunner.SetWorkerCount(
                Config.LibraryWorkers.Key,
                (int)request.LibraryWorkers,
                userId
            );
            changes.Add((Config.LibraryWorkers.Key, oldCount, (int)request.LibraryWorkers));
        }

        if (request.ImportWorkers is not null)
        {
            int oldCount = Config.ImportWorkers.Value;
            Config.ImportWorkers = new(Config.ImportWorkers.Key, (int)request.ImportWorkers);
            await queueRunner.SetWorkerCount(
                Config.ImportWorkers.Key,
                (int)request.ImportWorkers,
                userId
            );
            changes.Add((Config.ImportWorkers.Key, oldCount, (int)request.ImportWorkers));
        }

        if (request.ExtrasWorkers is not null)
        {
            int oldCount = Config.ExtrasWorkers.Value;
            Config.ExtrasWorkers = new(Config.ExtrasWorkers.Key, (int)request.ExtrasWorkers);
            await queueRunner.SetWorkerCount(
                Config.ExtrasWorkers.Key,
                (int)request.ExtrasWorkers,
                userId
            );
            changes.Add((Config.ExtrasWorkers.Key, oldCount, (int)request.ExtrasWorkers));
        }

        if (request.EncoderWorkers is not null)
        {
            int oldCount = Config.EncoderWorkers.Value;
            Config.EncoderWorkers = new(Config.EncoderWorkers.Key, (int)request.EncoderWorkers);
            await queueRunner.SetWorkerCount(
                Config.EncoderWorkers.Key,
                (int)request.EncoderWorkers,
                userId
            );
            changes.Add((Config.EncoderWorkers.Key, oldCount, (int)request.EncoderWorkers));
        }

        if (request.CronWorkers is not null)
        {
            int oldCount = Config.CronWorkers.Value;
            Config.CronWorkers = new(Config.CronWorkers.Key, (int)request.CronWorkers);
            await queueRunner.SetWorkerCount(
                Config.CronWorkers.Key,
                (int)request.CronWorkers,
                userId
            );
            changes.Add((Config.CronWorkers.Key, oldCount, (int)request.CronWorkers));
        }

        if (request.ImageWorkers is not null)
        {
            int oldCount = Config.ImageWorkers.Value;
            Config.ImageWorkers = new(Config.ImageWorkers.Key, (int)request.ImageWorkers);
            await queueRunner.SetWorkerCount(
                Config.ImageWorkers.Key,
                (int)request.ImageWorkers,
                userId
            );
            changes.Add((Config.ImageWorkers.Key, oldCount, (int)request.ImageWorkers));
        }

        if (request.FileWorkers is not null)
        {
            int oldCount = Config.FileWorkers.Value;
            Config.FileWorkers = new(Config.FileWorkers.Key, (int)request.FileWorkers);
            await queueRunner.SetWorkerCount(
                Config.FileWorkers.Key,
                (int)request.FileWorkers,
                userId
            );
            changes.Add((Config.FileWorkers.Key, oldCount, (int)request.FileWorkers));
        }

        if (request.MusicWorkers is not null)
        {
            int oldCount = Config.MusicWorkers.Value;
            Config.MusicWorkers = new(Config.MusicWorkers.Key, (int)request.MusicWorkers);
            await queueRunner.SetWorkerCount(
                Config.MusicWorkers.Key,
                (int)request.MusicWorkers,
                userId
            );
            changes.Add((Config.MusicWorkers.Key, oldCount, (int)request.MusicWorkers));
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
