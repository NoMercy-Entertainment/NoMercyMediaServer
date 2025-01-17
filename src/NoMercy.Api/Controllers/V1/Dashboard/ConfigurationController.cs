using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.V1.Dashboard.DTO;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Queue;
using Configuration = NoMercy.Database.Models.Configuration;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Configuration")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/configuration", Order = 10)]
public class ConfigurationController : BaseController
{
    [HttpGet]
    public IActionResult Index()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view configuration");

        return Ok(new ConfigDto
        {
            Data = new()
            {
                InternalServerPort = Config.InternalServerPort,
                ExternalServerPort = Config.ExternalServerPort,
                QueueWorkers = Config.QueueWorkers.Value,
                EncoderWorkers = Config.EncoderWorkers.Value,
                CronWorkers = Config.CronWorkers.Value,
                DataWorkers = Config.DataWorkers.Value,
                ImageWorkers = Config.ImageWorkers.Value,
                RequestWorkers = Config.RequestWorkers.Value,
                ServerName = DeviceName(),
                Swagger = Config.Swagger,
            }
        });
    }
    
    [NonAction]
    private static string DeviceName()
    {
        MediaContext mediaContext = new();
        Configuration? device = mediaContext.Configuration.FirstOrDefault(device => device.Key == "serverName");
        return device?.Value ?? Environment.MachineName;
    }

    [HttpPost]
    public IActionResult Store()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to store configuration");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }

    [HttpPatch]
    public async Task<IActionResult> Update([FromBody] ConfigDtoData request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update configuration");

        Guid userId = User.UserId();
        await using MediaContext mediaContext = new();
        
        if (request.InternalServerPort != 0)
        {
            Config.InternalServerPort = request.InternalServerPort;
            await mediaContext.Configuration.Upsert(new()
                {
                    Key = "InternalServerPort",
                    Value = request.InternalServerPort.ToString(),
                    ModifiedBy = userId
                })
                .On(e => e.Key)
                .WhenMatched((o, n) =>new()
                {
                    Id = o.Id,
                    Value = n.Value,
                    ModifiedBy = n.ModifiedBy,
                    UpdatedAt = n.UpdatedAt
                })
                .RunAsync();
        }
        
        if (request.ExternalServerPort != 0)
        {
            Config.ExternalServerPort = request.ExternalServerPort;
            await mediaContext.Configuration.Upsert(new()
                {
                    Key = "ExternalServerPort",
                    Value = request.ExternalServerPort.ToString(),
                    ModifiedBy = userId
                })
                .On(e => e.Key)
                .WhenMatched((o, n) =>new()
                {
                    Id = o.Id,
                    Value = n.Value,
                    ModifiedBy = n.ModifiedBy,
                    UpdatedAt = n.UpdatedAt
                })
                .RunAsync();
        }
        
        if (request.QueueWorkers is not null)
        {
            Config.QueueWorkers = new(Config.QueueWorkers.Key, (int)request.QueueWorkers);
            await QueueRunner.SetWorkerCount(Config.QueueWorkers.Key, (int)request.QueueWorkers, userId);
        }
        if (request.EncoderWorkers is not null)
        {
            Config.EncoderWorkers = new(Config.EncoderWorkers.Key, (int)request.EncoderWorkers);
            await QueueRunner.SetWorkerCount(Config.EncoderWorkers.Key, (int)request.EncoderWorkers, userId);
        }
        if (request.CronWorkers is not null)
        {
            Config.CronWorkers = new(Config.CronWorkers.Key, (int)request.CronWorkers);
            await QueueRunner.SetWorkerCount(Config.CronWorkers.Key, (int)request.CronWorkers, userId);
        }
        if (request.DataWorkers is not null)
        {
            Config.DataWorkers = new(Config.DataWorkers.Key, (int)request.DataWorkers);
            await QueueRunner.SetWorkerCount(Config.DataWorkers.Key, (int)request.DataWorkers, userId);
        }
        if (request.ImageWorkers is not null)
        {
            Config.ImageWorkers = new(Config.ImageWorkers.Key, (int)request.ImageWorkers);
            await QueueRunner.SetWorkerCount(Config.ImageWorkers.Key, (int)request.ImageWorkers, userId);
        }
        if (request.RequestWorkers is not null)
        {
            Config.RequestWorkers = new(Config.RequestWorkers.Key, (int)request.RequestWorkers);
            await QueueRunner.SetWorkerCount(Config.RequestWorkers.Key, (int)request.RequestWorkers, userId);
        }

        if (request.Swagger is not null)
        {
            Config.Swagger = (bool)request.Swagger;
            await mediaContext.Configuration.Upsert(new()
                {
                    Key = "swagger",
                    Value = Config.Swagger.ToString(),
                    ModifiedBy = User.UserId()
                })
                .On(e => e.Key)
                .WhenMatched((o, n) =>new()
                {
                    Id = o.Id,
                    Value = Config.Swagger.ToString(),
                    ModifiedBy = n.ModifiedBy,
                    UpdatedAt = n.UpdatedAt
                })
                .RunAsync();
        }
        
        if (request.ServerName is not null)
        {
            await mediaContext.Configuration.Upsert(new()
                {
                Key = "serverName",
                Value = request.ServerName,
                ModifiedBy = User.UserId()
            })
            .On(e => e.Key)
            .WhenMatched((o, n) =>new()
                {
                Id = o.Id,
                Value = request.ServerName,
                ModifiedBy = n.ModifiedBy,
                UpdatedAt = n.UpdatedAt
            })
            .RunAsync();
        }
        
        return Ok(new StatusResponseDto<string>
        {
            Message = "Configuration updated successfully",
            Status = "success",
            Args = [],
        });
    }

    [HttpGet]
    [Route("languages")]
    public async Task<IActionResult> Languages()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view languages");

        await using MediaContext context = new();
        List<Language> languages = await context.Languages
            .ToListAsync();

        return Ok(languages.Select(language => new LanguageDto
        {
            Id = language.Id,
            Iso6391 = language.Iso6391,
            EnglishName = language.EnglishName,
            Name = language.Name
        }).ToList());
    }

    [HttpGet]
    [Route("countries")]
    public async Task<IActionResult> Countries()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view countries");

        await using MediaContext context = new();
        List<Country> countries = await context.Countries
            .ToListAsync();

        return Ok(countries.Select(country => new CountryDto
        {
            Name = country.EnglishName,
            Code = country.Iso31661
        }).ToList());
    }
    
}