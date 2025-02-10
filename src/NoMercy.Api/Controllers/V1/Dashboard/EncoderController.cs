using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.Dashboard.DTO;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Encoder.Format.Container;
using NoMercy.Encoder.Format.Rules;
using NoMercy.Encoder.Format.Video;
using NoMercy.Networking;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Server Encoder Profiles")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/encoderprofiles", Order = 10)]
public class EncoderController(EncoderRepository encoderRepository) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoder profiles");

        await using MediaContext mediaContext = new();
        List<EncoderProfile> encoderProfiles = await encoderRepository.GetEncoderProfilesAsync();

        return Ok(encoderProfiles);
    }

    [HttpPost]
    public async Task<IActionResult> Create()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create encoder profiles");

        try
        {
            await using MediaContext mediaContext = new();
            int encoderProfiles = await encoderRepository.GetEncoderProfileCountAsync();

            EncoderProfile profile = new()
            {
                Id = Ulid.NewUlid(),
                Name = $"Profile {encoderProfiles}",
                Container = "mp4",
                Param = JsonConvert.SerializeObject(new ParamsDto
                {
                    Width = 1920,
                    Crf = 23,
                    Preset = "medium",
                    Profile = "main",
                    Codec = "libx264",
                    Audio = "aac"
                })
            };

            await encoderRepository.AddEncoderProfileAsync(profile);

            return Ok(new StatusResponseDto<EncoderProfile>
            {
                Status = "ok",
                Data = profile,
                Message = "Successfully created a new encoder profile.",
                Args = []
            });
        }
        catch (Exception e)
        {
            return Ok(new StatusResponseDto<EncoderProfile>()
            {
                Status = "error",
                Message = "Something went wrong creating a new library: {0}",
                Args = [e.Message]
            });
        }
    }

    [HttpDelete]
    [Route("{id:ulid}")]
    public async Task<IActionResult> Destroy(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to remove encoder profiles");

        EncoderProfile? profile = await encoderRepository.GetEncoderProfileByIdAsync(id);

        if (profile == null)
            return NotFound(new StatusResponseDto<string>
            {
                Status = "error",
                Data = "Encoder profile not found"
            });

        await encoderRepository.DeleteEncoderProfileAsync(profile);

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Data = "Profile removed"
        });
    }

    [HttpGet]
    [Route("containers")]
    public IActionResult Containers()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to remove encoder profiles");

        ContainerDto[] containers = BaseContainer.AvailableContainers
            .Select(container => new ContainerDto(container)).ToArray();

        return Ok(new DataResponseDto<ContainerDto[]>
        {
            Data = containers
        });
    }

    [HttpGet]
    [Route("framesizes")]
    public IActionResult FrameSizes()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to remove encoder profiles");

        Classes.VideoQualityDto[] frameSizes = BaseVideo.AvailableVideoSizes;

        return Ok(new DataResponseDto<Classes.VideoQualityDto[]>
        {
            Data = frameSizes.ToArray()
        });
    }
}