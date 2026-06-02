using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.Services;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Libraries;
using NoMercy.Helpers.Extensions;
using NoMercy.Storage;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

[ApiController]
[Tags("Dashboard Folders")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/folders", Order = 10)]
public class FolderDriverController(
    IFolderRepository folderRepository,
    IDriverRepository driverRepository,
    IStorageFactory storageFactory
) : BaseController
{
    // -----------------------------------------------------------------------
    // GET /api/v1/dashboard/folders/drivers
    // Returns all available driver TYPE metadata (for the create-driver dropdown).
    // -----------------------------------------------------------------------

    [HttpGet]
    [Route("drivers")]
    public IActionResult GetDriverTypes()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view driver types");

        return Ok(DriverTypeMetadata.All);
    }

    // -----------------------------------------------------------------------
    // GET /api/v1/dashboard/folders/{id}/driver
    // Returns which driver instance (if any) is attached to this folder.
    // -----------------------------------------------------------------------

    [HttpGet]
    [Route("{id:ulid}/driver")]
    public async Task<IActionResult> GetDriver(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view folder driver");

        Folder? folder = await folderRepository.GetFolderByIdAsync(id);
        if (folder is null)
            return NotFoundResponse("Folder not found");

        FolderDriverInfoDto info = new()
        {
            DriverId = folder.DriverId.ToString(),
            DriverName = folder.Driver?.Name,
            DriverType = folder.Driver?.Type,
            Path = folder.Path,
        };

        return Ok(info);
    }

    // -----------------------------------------------------------------------
    // PUT /api/v1/dashboard/folders/{id}/driver
    // Reassigns the driver and/or sub-path for a folder.
    // Body: { "driver_id": "<ulid>", "path": "<sub-path>" }
    //   driver_id — required; every folder must have a driver.
    //   path      — optional. null = leave existing path unchanged. ""
    //               (empty string) means the driver root itself.
    // -----------------------------------------------------------------------

    [HttpPut]
    [Route("{id:ulid}/driver")]
    public async Task<IActionResult> AssignDriver(Ulid id, [FromBody] FolderDriverAssignDto request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update folder driver");

        if (string.IsNullOrWhiteSpace(request.DriverId))
            return BadRequestResponse("driver_id is required. Every folder must have a driver.");

        if (!Ulid.TryParse(request.DriverId, out Ulid driverId))
            return BadRequestResponse("driver_id is not a valid ULID.");

        Folder? folder = await folderRepository.GetFolderByIdAsync(id);
        if (folder is null)
            return NotFoundResponse("Folder not found");

        bool exists = await driverRepository.DriverExistsAsync(driverId);
        if (!exists)
            return NotFoundResponse($"Driver '{request.DriverId}' not found.");

        folder.DriverId = driverId;
        if (request.Path is not null)
            folder.Path = request.Path;

        await folderRepository.UpdateFolderAsync(folder);

        storageFactory.Invalidate(id);

        FolderDriverInfoDto info = new()
        {
            DriverId = folder.DriverId.ToString(),
            DriverName = folder.Driver?.Name,
            DriverType = folder.Driver?.Type,
            Path = folder.Path,
        };

        return Ok(info);
    }
}
