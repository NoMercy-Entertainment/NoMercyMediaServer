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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.Services;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Storage;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.Api.Controllers.V1.Dashboard.Admin;

[ApiController]
[Tags("Dashboard Drivers")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/drivers", Order = 11)]
public class DriversController(IDriverRepository driverRepository, IStorageFactory storageFactory)
    : BaseController
{
    // -----------------------------------------------------------------------
    // GET /api/v1/dashboard/drivers
    // List all named driver instances.
    // -----------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!AuthPolicy.IsModerator(User))
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
        if (!AuthPolicy.IsModerator(User))
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
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to view drivers");

        Driver? driver = await driverRepository.GetDriverByIdAsync(id);
        if (driver is null)
            return NotFoundResponse("Driver not found");

        return Ok(MapToDto(driver));
    }

    // -----------------------------------------------------------------------
    // GET /api/v1/dashboard/drivers/system-local
    // Returns the stable id of the built-in system local driver.
    // -----------------------------------------------------------------------

    [HttpGet]
    [Route("system-local")]
    public IActionResult GetSystemLocalId()
    {
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to view driver info");

        return Ok(new { id = Driver.SystemLocalDriverId.ToString() });
    }

    // -----------------------------------------------------------------------
    // POST /api/v1/dashboard/drivers
    // Create a named driver instance.
    // -----------------------------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDriverRequestDto request)
    {
        if (!AuthPolicy.IsOwner(User))
            return UnauthorizedResponse("You do not have permission to create drivers");

        string normalizedType = (request.Type ?? string.Empty).Trim().ToLowerInvariant();

        if (!DriverTypeMetadata.AllUserCreatable.Contains(normalizedType))
            return BadRequestResponse(
                $"Invalid type '{request.Type}'. Allowed values: {string.Join(", ", DriverTypeMetadata.AllUserCreatable)}."
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

        if (request.Credentials is not null && HasMeaningfulCredentials(request.Credentials))
        {
            string credRef = $"driver:{newId}";
            CredentialManager.SetCredentials(
                target: credRef,
                username: request.Credentials.AccessKey,
                password: request.Credentials.SecretKey,
                apiKey: string.Empty
            );
            Logger.App(
                $"[DriversController] Stored credentials for new {normalizedType} driver "
                    + $"(id={newId}, accessKey len={request.Credentials.AccessKey.Length}, "
                    + $"secret len={request.Credentials.SecretKey.Length})",
                LogEventLevel.Information
            );

            // Inject credentialsRef into Config so the StorageFactory can resolve it.
            configToStore ??= new JObject();
            configToStore["credentialsRef"] = credRef;
        }
        else if (request.Credentials is not null)
        {
            Logger.App(
                $"[DriversController] Ignoring blank credentials block on create for {normalizedType} "
                    + $"(id={newId}); driver will be created without stored credentials.",
                LogEventLevel.Warning
            );
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

        return StatusCode(StatusCodes.Status201Created, MapToDto(driver));
    }

    // -----------------------------------------------------------------------
    // PUT /api/v1/dashboard/drivers/{id}
    // Update a named driver instance. Credentials omitted = leave unchanged.
    // -----------------------------------------------------------------------

    [HttpPut]
    [Route("{id:ulid}")]
    public async Task<IActionResult> Update(Ulid id, [FromBody] UpdateDriverRequestDto request)
    {
        if (!AuthPolicy.IsOwner(User))
            return UnauthorizedResponse("You do not have permission to update drivers");

        Driver? driver = await driverRepository.GetDriverByIdAsync(id);
        if (driver is null)
            return NotFoundResponse("Driver not found");

        // The built-in system-local driver anchors the entire passthrough
        // mode (empty rootPath) and must remain immutable. Users can create
        // their own named local drivers alongside it, but this row is off
        // limits — even a rename would surprise other code paths that look
        // it up by id.
        if (id == Driver.SystemLocalDriverId)
            return ConflictResponse("Cannot edit the built-in system local driver.");

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

        // Allow type change. UI lets the user switch backend in the form;
        // server must honour it or we silently validate against the old
        // type and reject configs that are valid for the new one.
        if (request.Type is not null)
        {
            string normalizedType = request.Type.Trim().ToLowerInvariant();
            if (!DriverTypeMetadata.AllUserCreatable.Contains(normalizedType))
                return BadRequestResponse(
                    $"Invalid type '{request.Type}'. Allowed values: {string.Join(", ", DriverTypeMetadata.AllUserCreatable)}."
                );
            driver.Type = normalizedType;
        }

        JObject? configToStore = request.Config;

        // Diagnostic: surface what the dashboard actually sent. Without this,
        // a UI that submits {credentials: null} vs {credentials: {access_key:"",
        // secret_key:""}} vs flat top-level fields all looked identical from
        // the operator's seat.
        Logger.App(
            $"[DriversController] Update {id} ({driver.Type}) — credentials block: "
                + (
                    request.Credentials is null
                        ? "absent"
                        : $"present (access_key len={request.Credentials.AccessKey.Length}, "
                            + $"secret_key len={request.Credentials.SecretKey.Length})"
                ),
            LogEventLevel.Information
        );

        if (request.Credentials is not null && HasMeaningfulCredentials(request.Credentials))
        {
            string credRef = $"driver:{id}";
            CredentialManager.SetCredentials(
                target: credRef,
                username: request.Credentials.AccessKey,
                password: request.Credentials.SecretKey,
                apiKey: string.Empty
            );
            Logger.App(
                $"[DriversController] Updated credentials for driver {id} ({driver.Type}) "
                    + $"(accessKey len={request.Credentials.AccessKey.Length}, "
                    + $"secret len={request.Credentials.SecretKey.Length})",
                LogEventLevel.Information
            );

            // Ensure credentialsRef is present in Config.
            configToStore ??= request.Config ?? ParseConfigJson(driver.Config);
            configToStore ??= new JObject();
            configToStore["credentialsRef"] = credRef;
        }
        else if (request.Credentials is not null)
        {
            // The dashboard form re-submits an empty credentials block when
            // the user touches anything else — without this guard, every
            // unrelated edit (renaming the driver, toggling a config flag)
            // wiped out the previously-stored access key + secret. Preserve
            // existing credentials when the incoming block is blank.
            Logger.App(
                $"[DriversController] Ignoring blank credentials block on update for driver {id} "
                    + $"({driver.Type}); preserving previously-stored credentials.",
                LogEventLevel.Information
            );
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
    // PUT /api/v1/dashboard/drivers/{id}/credentials
    // Direct credential write — bypasses the full Update payload so a stuck
    // form (or a curl/script user) can land non-empty creds without having
    // to reconstruct config + name + type. Body: {access_key, secret_key}.
    // -----------------------------------------------------------------------

    [HttpPut]
    [Route("{id:ulid}/credentials")]
    public async Task<IActionResult> UpdateCredentials(
        Ulid id,
        [FromBody] DriverCredentialsDto request
    )
    {
        if (!AuthPolicy.IsOwner(User))
            return UnauthorizedResponse("You do not have permission to update driver credentials");

        Driver? driver = await driverRepository.GetDriverByIdAsync(id);
        if (driver is null)
            return NotFoundResponse("Driver not found");

        if (!HasMeaningfulCredentials(request))
            return BadRequestResponse(
                "access_key and secret_key are both required and must be non-empty."
            );

        string credRef = $"driver:{id}";
        CredentialManager.SetCredentials(
            target: credRef,
            username: request.AccessKey,
            password: request.SecretKey,
            apiKey: string.Empty
        );

        // Ensure credentialsRef is present in Config so the StorageFactory resolves it.
        JObject? configObj = ParseConfigJson(driver.Config) ?? new();
        configObj["credentialsRef"] = credRef;
        driver.Config = JsonConvert.SerializeObject(configObj);
        driver.UpdatedAt = DateTimeOffset.UtcNow;
        await driverRepository.UpdateDriverAsync(driver);

        // Invalidate any cached IStorage instances built without credentials.
        List<Ulid> folderIds = driver.Folders.Select(f => f.Id).ToList();
        foreach (Ulid folderId in folderIds)
            storageFactory.Invalidate(folderId);

        Logger.App(
            $"[DriversController] Direct credential write for driver {id} ({driver.Type}) "
                + $"(accessKey len={request.AccessKey.Length}, secret len={request.SecretKey.Length}); "
                + $"invalidated {folderIds.Count} cached folder(s).",
            LogEventLevel.Information
        );

        return Ok(MapToDto(driver));
    }

    // -----------------------------------------------------------------------
    // DELETE /api/v1/dashboard/drivers/{id}
    // Refuses with 409 if any folder references this driver or it is system.
    // -----------------------------------------------------------------------

    [HttpDelete]
    [Route("{id:ulid}")]
    public async Task<IActionResult> Delete(Ulid id)
    {
        if (!AuthPolicy.IsOwner(User))
            return UnauthorizedResponse("You do not have permission to delete drivers");

        if (id == Driver.SystemLocalDriverId)
            return ConflictResponse("Cannot delete system driver");

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
        // Resolve the credentials_configured flag against the secrets store —
        // not against the mere presence of a credentialsRef key in Config.
        // A wiped or blank entry leaves the ref in place but the values gone,
        // and previously surfaced as "credentials_configured: true" in the
        // dashboard while the AWS SDK refused with "access key has length 0."
        bool hasCredentialsConfigured = false;
        if (!string.IsNullOrWhiteSpace(driver.Config))
        {
            try
            {
                JObject? parsed = JObject.Parse(driver.Config);
                string? credRef = parsed["credentialsRef"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(credRef))
                {
                    UserPass? stored = CredentialManager.Credential(credRef);
                    hasCredentialsConfigured =
                        stored is not null
                        && !string.IsNullOrEmpty(stored.Username)
                        && !string.IsNullOrEmpty(stored.Password);
                }
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
            CredentialsConfigured = hasCredentialsConfigured,
            IsSystem = driver.Id == Driver.SystemLocalDriverId,
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

    /// <summary>
    /// True when the credentials block carries actual values to write. The
    /// dashboard form submits an empty access/secret block whenever any
    /// other field changes — without this guard a rename or config tweak
    /// would silently overwrite the previously-stored credentials with
    /// empty strings, surfacing later as "Credential access key has length 0"
    /// when the driver is used.
    /// </summary>
    private static bool HasMeaningfulCredentials(DriverCredentialsDto credentials) =>
        !string.IsNullOrWhiteSpace(credentials.AccessKey)
        && !string.IsNullOrWhiteSpace(credentials.SecretKey);
}
