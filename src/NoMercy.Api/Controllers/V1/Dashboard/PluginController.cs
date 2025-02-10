
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.Dashboard.DTO;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Helpers;
using NoMercy.Networking;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Server Plugins")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/plugins", Order = 10)]
public class PluginController : BaseController
{
    [HttpGet]
    public IActionResult Index()
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to view plugins");

        // AniDBAnimeItem randomAnime = await AniDbRandomAnime.GetRandomAnime();

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Plugins loaded successfully"
        });
    }

    [HttpGet]
    [Route("credentials")]
    public IActionResult Credentials()
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to view credentials");

        UserPass? aniDb = CredentialManager.Credential("AniDb");

        if (aniDb == null)
            return NotFound(new StatusResponseDto<string>
            {
                Status = "error",
                Message = "No credentials found for AniDb"
            });

        return Ok(new AniDbCredentialsResponseDto
        {
            Key = "AniDb",
            Username = aniDb.Username,
            ApiKey = aniDb.ApiKey
        });
    }

    [HttpPost]
    [Route("credentials")]
    public IActionResult Credentials([FromBody] AniDbCredentialsRequestDto requestDto)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to set credentials");

        UserPass? aniDb = CredentialManager.Credential(requestDto.Key);
        CredentialManager.SetCredentials(requestDto.Key, requestDto.Username, requestDto.Password ?? aniDb?.Password ?? "",
            requestDto.ApiKey);

        return Ok(new StatusResponseDto<string>
        {
            Status = "ok",
            Message = "Credentials set successfully for {0}",
            Args = [requestDto.Key]
        });
    }
}
