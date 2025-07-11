using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Services;
using NoMercy.Helpers;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags("Media")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}")]
public class HomeController : BaseController
{
    private readonly HomeService _homeService;

    public HomeController(HomeService homeService)
    {
        _homeService = homeService;
    }

    [HttpGet("home")]
    public async Task<IActionResult> ContinueWatching()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view continue watching");

        Render result = await _homeService.GetHomeData(User.UserId(), Language(), Country());

        return Ok(result);
    }

    [HttpPost("home/card")]
    public async Task<IActionResult> HomeCard([FromBody] CardRequestDto request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view home card");

        Render result = await _homeService.GetHomeCard(User.UserId(), Language(), request.ReplaceId);

        return Ok(result);
    }

    [HttpGet("home/tv")]
    public async Task<IActionResult> HomeTv()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view home tv");

        Render result = await _homeService.GetHomeTvContent(User.UserId(), Language(), Country());

        return Ok(result);
    }

    [HttpPost("home/continue")]
    public async Task<IActionResult> HomeContinue([FromBody] CardRequestDto request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view continue watching");

        Render result =
            await _homeService.GetHomeContinueContent(User.UserId(), Language(), Country(), request.ReplaceId);

        return Ok(result);
    }

    [HttpGet]
    [Route("screensaver")]
    public async Task<IActionResult> Screensaver()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view screensaver");

        ScreensaverDto result = await _homeService.GetScreensaverContent(User.UserId());

        return Ok(result);
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("/status")]
    public IActionResult Status()
    {
        return Ok(new
        {
            Status = "ok",
            Version = "1.0",
            Message = "NoMercy MediaServer API is running",
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpGet]
    [Route("permissions")]
    public IActionResult Permissions()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have access to this server");

        return Ok(new
        {
            owner = User.IsOwner(),
            manager = User.IsModerator(),
            allowed = User.IsAllowed()
        });
    }
}