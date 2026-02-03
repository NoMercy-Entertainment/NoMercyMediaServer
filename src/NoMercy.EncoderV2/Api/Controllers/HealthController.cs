using Microsoft.AspNetCore.Mvc;

namespace NoMercy.EncoderV2.Api.Controllers;

[ApiController]
[Route("api/encoderv2/health")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Creates a new instance of <see cref="HealthController"/>.
    /// </summary>
    public HealthController()
    {
    }

    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
