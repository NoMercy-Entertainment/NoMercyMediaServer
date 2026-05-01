using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.Services;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Storage;
using NoMercy.Helpers;
using NoMercy.Helpers.Extensions;
using NoMercy.Storage;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Drivers")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/drivers", Order = 11)]
public class DriversController(DriverRepository driverRepository, IStorageFactory storageFactory)
    : BaseController
{
    // -----------------------------------------------------------------------
    // GET /api/v1/dashboard/drivers
    // List all named driver instances.
    // -----------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view drivers");

        List<Driver> drivers = await driverRepository.GetAllDriversAsync();
        List<DriverDto> result = drivers.Select(MapToDto).ToList();

        return Ok(result);
    }

    // -----------------------------------------------------------------------
    // GET /api/v1/dashboard/drivers/types
    // Returns driver type metadata (same data as /folders/drivers).
    // -----------------------------------------------------------------------

    [HttpGet]
    [Route("types")]
    public IActionResult GetTypes()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view driver types");

        return Ok(DriverTypeMetadata.All);
    }

    // -----------------------------------------------------------------------
    // GET /api/v1/dashboard/drivers/{id}
    // -----------------------------------------------------------------------

    [HttpGet]
    [Route("{id:ulid}")]
    public async Task<IActionResult> Show(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view drivers");

        Driver? driver = await driverRepository.GetDriverByIdAsync(id);
        if (driver is null)
            return NotFoundResponse("Driver not found");

        return Ok(MapToDto(driver));
    }

    // -----------------------------------------------------------------------
    // POST /api/v1/dashboard/drivers
    // Create a named driver instance.
    // -----------------------------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDriverRequestDto request)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to create drivers");

        string normalizedType = (request.Type ?? string.Empty).Trim().ToLowerInvariant();

        if (!DriverTypeMetadata.AllowedTypes.Contains(normalizedType))
            return BadRequestResponse(
                $"Invalid type '{request.Type}'. Allowed values: {string.Join(", ", DriverTypeMetadata.AllowedTypes)}."
            );

        string? validationError = DriverTypeMetadata.ValidateConfig(normalizedType, request.Config);
        if (validationError is not null)
            return BadRequestResponse(validationError);

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequestResponse("name is required.");

        bool nameExists = await driverRepository.NameExistsAsync(request.Name);
        if (nameExists)
            return ConflictResponse($"A driver named '{request.Name}' already exists.");

        Ulid newId = Ulid.NewUlid();

        JObject? configToStore = request.Config;

        if (request.Credentials is not null)
        {
            string credRef = $"driver:{newId}";
            CredentialManager.SetCredentials(
                target: credRef,
                username: request.Credentials.AccessKey,
                password: request.Credentials.SecretKey,
                apiKey: string.Empty
            );

            // Inject credentialsRef into Config so the StorageFactory can resolve it.
            configToStore ??= new JObject();
            configToStore["credentialsRef"] = credRef;
        }

        Driver driver = new()
        {
            Id = newId,
            Name = request.Name.Trim(),
            Type = normalizedType,
            Config = configToStore is not null ? JsonConvert.SerializeObject(configToStore) : null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await driverRepository.CreateDriverAsync(driver);

        return StatusCode(
            StatusCodes.Status201Created,
            MapToDto(driver)
        );
    }

    // -----------------------------------------------------------------------
    // PUT /api/v1/dashboard/drivers/{id}
    // Update a named driver instance. Credentials omitted = leave unchanged.
    // -----------------------------------------------------------------------

    [HttpPut]
    [Route("{id:ulid}")]
    public async Task<IActionResult> Update(Ulid id, [FromBody] UpdateDriverRequestDto request)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to update drivers");

        Driver? driver = await driverRepository.GetDriverByIdAsync(id);
        if (driver is null)
            return NotFoundResponse("Driver not found");

        if (request.Name is not null)
        {
            string trimmedName = request.Name.Trim();
            if (string.IsNullOrEmpty(trimmedName))
                return BadRequestResponse("name cannot be empty.");

            bool nameExists = await driverRepository.NameExistsAsync(trimmedName, excludeId: id);
            if (nameExists)
                return ConflictResponse($"A driver named '{trimmedName}' already exists.");

            driver.Name = trimmedName;
        }

        JObject? configToStore = request.Config;

        if (request.Credentials is not null)
        {
            string credRef = $"driver:{id}";
            CredentialManager.SetCredentials(
                target: credRef,
                username: request.Credentials.AccessKey,
                password: request.Credentials.SecretKey,
                apiKey: string.Empty
            );

            // Ensure credentialsRef is present in Config.
            configToStore ??= request.Config ?? ParseConfigJson(driver.Config);
            configToStore ??= new JObject();
            configToStore["credentialsRef"] = credRef;
        }

        if (configToStore is not null)
        {
            string? validationError = DriverTypeMetadata.ValidateConfig(driver.Type, configToStore);
            if (validationError is not null)
                return BadRequestResponse(validationError);

            driver.Config = JsonConvert.SerializeObject(configToStore);
        }

        driver.UpdatedAt = DateTimeOffset.UtcNow;

        await driverRepository.UpdateDriverAsync(driver);

        // Invalidate StorageFactory cache for all folders using this driver.
        List<Ulid> folderIds = driver.Folders.Select(f => f.Id).ToList();
        foreach (Ulid folderId in folderIds)
            storageFactory.Invalidate(folderId);

        return Ok(MapToDto(driver));
    }

    // -----------------------------------------------------------------------
    // DELETE /api/v1/dashboard/drivers/{id}
    // Refuses with 409 if any folder references this driver.
    // -----------------------------------------------------------------------

    [HttpDelete]
    [Route("{id:ulid}")]
    public async Task<IActionResult> Delete(Ulid id)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("You do not have permission to delete drivers");

        Driver? driver = await driverRepository.GetDriverByIdAsync(id);
        if (driver is null)
            return NotFoundResponse("Driver not found");

        int folderCount = await driverRepository.FolderCountAsync(id);
        if (folderCount > 0)
            return ConflictResponse(
                $"Cannot delete driver '{driver.Name}': {folderCount} folder(s) still reference it. Reassign or remove them first."
            );

        // Remove stored credentials if present.
        CredentialManager.RemoveCredentials($"driver:{id}");

        await driverRepository.DeleteDriverAsync(driver);

        return NoContent();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static DriverDto MapToDto(Driver driver)
    {
        bool hasCredentialsRef = false;
        if (!string.IsNullOrWhiteSpace(driver.Config))
        {
            try
            {
                JObject? parsed = JObject.Parse(driver.Config);
                string? credRef = parsed?["credentialsRef"]?.Value<string>();
                hasCredentialsRef = !string.IsNullOrWhiteSpace(credRef);
            }
            catch (JsonException) { }
        }

        JObject? configObj = ParseConfigJson(driver.Config);

        // Strip credentials ref from the public response — never expose the ref key.
        if (configObj is not null)
        {
            configObj.Remove("credentialsRef");
            if (!configObj.HasValues)
                configObj = null;
        }

        return new DriverDto
        {
            Id = driver.Id.ToString(),
            Name = driver.Name,
            Type = driver.Type,
            Config = configObj,
            CredentialsConfigured = hasCredentialsRef,
            FolderCount = driver.Folders.Count,
            CreatedAt = driver.CreatedAt,
            UpdatedAt = driver.UpdatedAt,
        };
    }

    private static JObject? ParseConfigJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JObject.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
