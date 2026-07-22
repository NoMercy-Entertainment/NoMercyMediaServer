// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.Services;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Storage;
using NoMercy.NmSystem.Auth;
using NoMercy.Storage;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

[ApiController]
[Tags(tags: "Dashboard Drivers")]
[ApiVersion(version: 1.0)]
[Authorize]
[Route(template: "api/v{version:apiVersion}/dashboard/drivers", Order = 11)]
public class DriversController(
    IDriverRepository driverRepository,
    IStorageFactory storageFactory,
    ILogger<DriversController> logger
) : BaseController
{
    // -----------------------------------------------------------------------
    // GET /api/v1/dashboard/drivers
    // List all named driver instances.
    // -----------------------------------------------------------------------

    [HttpGet]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Index()
    {
        List<Driver> drivers = await driverRepository.GetAllDriversAsync();
        List<DriverDto> result = drivers.Select(selector: MapToDto).ToList();

        return Ok(value: result);
    }

    // -----------------------------------------------------------------------
    // GET /api/v1/dashboard/drivers/types
    // Returns driver type metadata (same data as /folders/drivers).
    // -----------------------------------------------------------------------

    [HttpGet]
    [Route(template: "types")]
    [Authorize(Policy = "Moderator")]
    public IActionResult GetTypes()
    {
        return Ok(value: DriverTypeMetadata.All);
    }

    // -----------------------------------------------------------------------
    // GET /api/v1/dashboard/drivers/{id}
    // -----------------------------------------------------------------------

    [HttpGet]
    [Route(template: "{id:ulid}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Show(Ulid id)
    {
        Driver? driver = await driverRepository.GetDriverByIdAsync(id: id);
        if (driver is null)
            return NotFoundResponse(detail: "Driver not found");

        return Ok(value: MapToDto(driver: driver));
    }

    // -----------------------------------------------------------------------
    // GET /api/v1/dashboard/drivers/system-local
    // Returns the stable id of the built-in system local driver.
    // -----------------------------------------------------------------------

    [HttpGet]
    [Route(template: "system-local")]
    [Authorize(Policy = "Moderator")]
    public IActionResult GetSystemLocalId()
    {
        return Ok(value: new { id = Driver.SystemLocalDriverId.ToString() });
    }

    // -----------------------------------------------------------------------
    // POST /api/v1/dashboard/drivers
    // Create a named driver instance.
    // -----------------------------------------------------------------------

    [HttpPost]
    [Authorize(Policy = "Owner")]
    public async Task<IActionResult> Create([FromBody] CreateDriverRequestDto request)
    {
        string normalizedType = (request.Type ?? string.Empty).Trim().ToLowerInvariant();

        if (!DriverTypeMetadata.AllUserCreatable.Contains(value: normalizedType))
            return BadRequestResponse(
                detail: $"Invalid type '{request.Type}'. Allowed values: {string.Join(separator: ", ", value: DriverTypeMetadata.AllUserCreatable)}."
            );

        string? validationError = DriverTypeMetadata.ValidateConfig(driverType: normalizedType, config: request.Config);
        if (validationError is not null)
            return BadRequestResponse(detail: validationError);

        if (string.IsNullOrWhiteSpace(value: request.Name))
            return BadRequestResponse(detail: "name is required.");

        bool nameExists = await driverRepository.NameExistsAsync(name: request.Name);
        if (nameExists)
            return ConflictResponse(detail: $"A driver named '{request.Name}' already exists.");

        Ulid newId = Ulid.NewUlid();

        JObject? configToStore = request.Config;

        if (request.Credentials is not null && HasMeaningfulCredentials(credentials: request.Credentials))
        {
            string credRef = $"driver:{newId}";
            CredentialManager.SetCredentials(
                target: credRef,
                username: request.Credentials.AccessKey,
                password: request.Credentials.SecretKey,
                apiKey: string.Empty
            );
            logger.LogInformation(
                message: "[DriversController] Stored credentials for new {NormalizedType} driver (id={NewId}, accessKey len={Length}, secret len={Length2})", args: [normalizedType, newId, request.Credentials.AccessKey.Length, request.Credentials.SecretKey.Length]
            );

            // Inject credentialsRef into Config so the StorageFactory can resolve it.
            configToStore ??= new();
            configToStore[propertyName: "credentialsRef"] = credRef;
        }
        else if (request.Credentials is not null)
        {
            logger.LogWarning(
                message: "[DriversController] Ignoring blank credentials block on create for {NormalizedType} (id={NewId}); driver will be created without stored credentials.", args: [normalizedType, newId]
            );
        }

        Driver driver = new()
        {
            Id = newId,
            Name = request.Name.Trim(),
            Type = normalizedType,
            Config = configToStore is not null ? JsonConvert.SerializeObject(value: configToStore) : null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await driverRepository.CreateDriverAsync(driver: driver);

        return StatusCode(statusCode: StatusCodes.Status201Created, value: MapToDto(driver: driver));
    }

    // -----------------------------------------------------------------------
    // PUT /api/v1/dashboard/drivers/{id}
    // Update a named driver instance. Credentials omitted = leave unchanged.
    // -----------------------------------------------------------------------

    [HttpPut]
    [Route(template: "{id:ulid}")]
    [Authorize(Policy = "Owner")]
    public async Task<IActionResult> Update(Ulid id, [FromBody] UpdateDriverRequestDto request)
    {
        Driver? driver = await driverRepository.GetDriverByIdAsync(id: id);
        if (driver is null)
            return NotFoundResponse(detail: "Driver not found");

        // The built-in system-local driver anchors the entire passthrough
        // mode (empty rootPath) and must remain immutable. Users can create
        // their own named local drivers alongside it, but this row is off
        // limits — even a rename would surprise other code paths that look
        // it up by id.
        if (id == Driver.SystemLocalDriverId)
            return ConflictResponse(detail: "Cannot edit the built-in system local driver.");

        if (request.Name is not null)
        {
            string trimmedName = request.Name.Trim();
            if (string.IsNullOrEmpty(value: trimmedName))
                return BadRequestResponse(detail: "name cannot be empty.");

            bool nameExists = await driverRepository.NameExistsAsync(name: trimmedName, excludeId: id);
            if (nameExists)
                return ConflictResponse(detail: $"A driver named '{trimmedName}' already exists.");

            driver.Name = trimmedName;
        }

        // Allow type change. UI lets the user switch backend in the form;
        // server must honour it or we silently validate against the old
        // type and reject configs that are valid for the new one.
        if (request.Type is not null)
        {
            string normalizedType = request.Type.Trim().ToLowerInvariant();
            if (!DriverTypeMetadata.AllUserCreatable.Contains(value: normalizedType))
                return BadRequestResponse(
                    detail: $"Invalid type '{request.Type}'. Allowed values: {string.Join(separator: ", ", value: DriverTypeMetadata.AllUserCreatable)}."
                );
            driver.Type = normalizedType;
        }

        JObject? configToStore = request.Config;

        // Diagnostic: surface what the dashboard actually sent. Without this,
        // a UI that submits {credentials: null} vs {credentials: {access_key:"",
        // secret_key:""}} vs flat top-level fields all looked identical from
        // the operator's seat.
        logger.LogInformation(
            message: "[DriversController] Update {Id} ({Type}) — credentials block: {Length}", args:
            [id, driver.Type, (
                    request.Credentials is null
                        ? "absent"
                        : $"present (access_key len={request.Credentials.AccessKey.Length}, "
                          + $"secret_key len={request.Credentials.SecretKey.Length})"
                )
            ]
        );

        if (request.Credentials is not null && HasMeaningfulCredentials(credentials: request.Credentials))
        {
            string credRef = $"driver:{id}";
            CredentialManager.SetCredentials(
                target: credRef,
                username: request.Credentials.AccessKey,
                password: request.Credentials.SecretKey,
                apiKey: string.Empty
            );
            logger.LogInformation(
                message: "[DriversController] Updated credentials for driver {Id} ({Type}) (accessKey len={Length}, secret len={Length2})", args: [id, driver.Type, request.Credentials.AccessKey.Length, request.Credentials.SecretKey.Length]
            );

            // Ensure credentialsRef is present in Config.
            configToStore ??= request.Config ?? ParseConfigJson(json: driver.Config);
            configToStore ??= new();
            configToStore[propertyName: "credentialsRef"] = credRef;
        }
        else if (request.Credentials is not null)
        {
            // The dashboard form re-submits an empty credentials block when
            // the user touches anything else — without this guard, every
            // unrelated edit (renaming the driver, toggling a config flag)
            // wiped out the previously-stored access key + secret. Preserve
            // existing credentials when the incoming block is blank.
            logger.LogInformation(
                message: "[DriversController] Ignoring blank credentials block on update for driver {Id} ({Type}); preserving previously-stored credentials.", args: [id, driver.Type]
            );
        }

        if (configToStore is not null)
        {
            string? validationError = DriverTypeMetadata.ValidateConfig(driverType: driver.Type, config: configToStore);
            if (validationError is not null)
                return BadRequestResponse(detail: validationError);

            driver.Config = JsonConvert.SerializeObject(value: configToStore);
        }

        driver.UpdatedAt = DateTimeOffset.UtcNow;

        await driverRepository.UpdateDriverAsync(driver: driver);

        // Invalidate StorageFactory cache for all folders using this driver.
        List<Ulid> folderIds = driver.Folders.Select(selector: f => f.Id).ToList();
        foreach (Ulid folderId in folderIds)
            storageFactory.Invalidate(folderId: folderId);

        return Ok(value: MapToDto(driver: driver));
    }

    // -----------------------------------------------------------------------
    // PUT /api/v1/dashboard/drivers/{id}/credentials
    // Direct credential write — bypasses the full Update payload so a stuck
    // form (or a curl/script user) can land non-empty creds without having
    // to reconstruct config + name + type. Body: {access_key, secret_key}.
    // -----------------------------------------------------------------------

    [HttpPut]
    [Route(template: "{id:ulid}/credentials")]
    [Authorize(Policy = "Owner")]
    public async Task<IActionResult> UpdateCredentials(
        Ulid id,
        [FromBody] DriverCredentialsDto request
    )
    {
        Driver? driver = await driverRepository.GetDriverByIdAsync(id: id);
        if (driver is null)
            return NotFoundResponse(detail: "Driver not found");

        if (!HasMeaningfulCredentials(credentials: request))
            return BadRequestResponse(
                detail: "access_key and secret_key are both required and must be non-empty."
            );

        string credRef = $"driver:{id}";
        CredentialManager.SetCredentials(
            target: credRef,
            username: request.AccessKey,
            password: request.SecretKey,
            apiKey: string.Empty
        );

        // Ensure credentialsRef is present in Config so the StorageFactory resolves it.
        JObject? configObj = ParseConfigJson(json: driver.Config) ?? new();
        configObj[propertyName: "credentialsRef"] = credRef;
        driver.Config = JsonConvert.SerializeObject(value: configObj);
        driver.UpdatedAt = DateTimeOffset.UtcNow;
        await driverRepository.UpdateDriverAsync(driver: driver);

        // Invalidate any cached IStorage instances built without credentials.
        List<Ulid> folderIds = driver.Folders.Select(selector: f => f.Id).ToList();
        foreach (Ulid folderId in folderIds)
            storageFactory.Invalidate(folderId: folderId);

        logger.LogInformation(
            message: "[DriversController] Direct credential write for driver {Id} ({Type}) (accessKey len={Length}, secret len={Length2}); invalidated {Count} cached folder(s).", args: [id, driver.Type, request.AccessKey.Length, request.SecretKey.Length, folderIds.Count]
        );

        return Ok(value: MapToDto(driver: driver));
    }

    // -----------------------------------------------------------------------
    // DELETE /api/v1/dashboard/drivers/{id}
    // Refuses with 409 if any folder references this driver or it is system.
    // -----------------------------------------------------------------------

    [HttpDelete]
    [Route(template: "{id:ulid}")]
    [Authorize(Policy = "Owner")]
    public async Task<IActionResult> Delete(Ulid id)
    {
        if (id == Driver.SystemLocalDriverId)
            return ConflictResponse(detail: "Cannot delete system driver");

        Driver? driver = await driverRepository.GetDriverByIdAsync(id: id);
        if (driver is null)
            return NotFoundResponse(detail: "Driver not found");

        int libraryFolderCount = await driverRepository.LibraryFolderCountAsync(driverId: id);
        if (libraryFolderCount > 0)
            return ConflictResponse(
                detail: $"Cannot delete driver '{driver.Name}': {libraryFolderCount} folder(s) are still in use by a library. Remove them from their libraries first."
            );

        // Remove stored credentials if present.
        CredentialManager.RemoveCredentials(target: $"driver:{id}");

        await driverRepository.DeleteDriverAsync(driver: driver);

        return NoContent();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static DriverDto MapToDto(Driver driver)
    {
        // Resolve the credentials_configured flag against the secrets store —
        // not against the mere presence of a credentialsRef key in Config.
        // A wiped or blank entry leaves the ref in place but the values gone,
        // and previously surfaced as "credentials_configured: true" in the
        // dashboard while the AWS SDK refused with "access key has length 0."
        bool hasCredentialsConfigured = false;
        if (!string.IsNullOrWhiteSpace(value: driver.Config))
        {
            try
            {
                JObject? parsed = JObject.Parse(json: driver.Config);
                string? credRef = parsed[propertyName: "credentialsRef"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(value: credRef))
                {
                    UserPass? stored = CredentialManager.Credential(target: credRef);
                    hasCredentialsConfigured =
                        stored is not null
                        && !string.IsNullOrEmpty(value: stored.Username)
                        && !string.IsNullOrEmpty(value: stored.Password);
                }
            }
            catch (JsonException) { }
        }

        JObject? configObj = ParseConfigJson(json: driver.Config);

        // Strip credentials ref from the public response — never expose the ref key.
        if (configObj is not null)
        {
            configObj.Remove(propertyName: "credentialsRef");
            if (!configObj.HasValues)
                configObj = null;
        }

        return new()
        {
            Id = driver.Id.ToString(),
            Name = driver.Name,
            Type = driver.Type,
            Config = configObj,
            CredentialsConfigured = hasCredentialsConfigured,
            IsSystem = driver.Id == Driver.SystemLocalDriverId,
            FolderCount = driver.Folders.Count,
            CreatedAt = driver.CreatedAt,
            UpdatedAt = driver.UpdatedAt,
        };
    }

    private static JObject? ParseConfigJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(value: json))
            return null;

        try
        {
            return JObject.Parse(json: json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// True when the credentials block carries actual values to write. The
    /// dashboard form submits an empty access/secret block whenever any
    /// other field changes — without this guard a rename or config tweak
    /// would silently overwrite the previously-stored credentials with
    /// empty strings, surfacing later as "Credential access key has length 0"
    /// when the driver is used.
    /// </summary>
    private static bool HasMeaningfulCredentials(DriverCredentialsDto credentials) =>
        !string.IsNullOrWhiteSpace(value: credentials.AccessKey)
        && !string.IsNullOrWhiteSpace(value: credentials.SecretKey);
}
