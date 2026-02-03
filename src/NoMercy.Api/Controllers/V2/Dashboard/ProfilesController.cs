using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.EncoderV2.Repositories;
using NoMercy.Helpers;

namespace NoMercy.Api.Controllers.V2.Dashboard;

/// <summary>
/// Controller for managing EncoderV2 encoding profiles
/// Provides CRUD operations for encoding profiles with video, audio, and subtitle configurations
/// </summary>
[ApiController]
[Tags("EncoderV2 Profiles")]
[ApiVersion(2.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/encoder/profiles", Order = 10)]
public class ProfilesController(IProfileRepository profileRepository) : BaseController
{
    /// <summary>
    /// Get all encoding profiles
    /// </summary>
    /// <returns>List of all encoding profiles</returns>
    [HttpGet]
    [ProducesResponseType(typeof(StatusResponseDto<List<EncoderProfile>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Index()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoder profiles");

        List<EncoderProfile> profiles = await profileRepository.GetAllProfilesAsync();

        return Ok(new StatusResponseDto<List<EncoderProfile>>
        {
            Status = "ok",
            Data = profiles,
            Message = "Successfully retrieved encoder profiles."
        });
    }

    /// <summary>
    /// Get a specific encoding profile by ID
    /// </summary>
    /// <param name="id">Profile ID (ULID)</param>
    /// <returns>The encoding profile</returns>
    [HttpGet]
    [Route("{id:ulid}")]
    [ProducesResponseType(typeof(StatusResponseDto<EncoderProfile>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Show(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoder profiles");

        EncoderProfile? profile = await profileRepository.GetProfileAsync(id);

        if (profile is null)
            return NotFoundResponse("Encoder profile not found");

        return Ok(new StatusResponseDto<EncoderProfile>
        {
            Status = "ok",
            Data = profile,
            Message = "Successfully retrieved encoder profile."
        });
    }

    /// <summary>
    /// Create a new encoding profile
    /// </summary>
    /// <param name="request">Profile creation request</param>
    /// <returns>The created profile</returns>
    [HttpPost]
    [ProducesResponseType(typeof(StatusResponseDto<EncoderProfile>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Store([FromBody] CreateProfileRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create encoder profiles");

        try
        {
            EncoderProfile profile = new()
            {
                Name = request.Name,
                Container = request.Container,
                Param = request.Param,
                VideoProfiles = request.VideoProfiles ?? [],
                AudioProfiles = request.AudioProfiles ?? [],
                SubtitleProfiles = request.SubtitleProfiles ?? []
            };

            EncoderProfile created = await profileRepository.CreateProfileAsync(profile);

            return Ok(new StatusResponseDto<EncoderProfile>
            {
                Status = "ok",
                Data = created,
                Message = "Successfully created encoder profile."
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntityResponse($"Failed to create encoder profile: {e.Message}");
        }
    }

    /// <summary>
    /// Update an existing encoding profile
    /// </summary>
    /// <param name="id">Profile ID (ULID)</param>
    /// <param name="request">Profile update request</param>
    /// <returns>The updated profile</returns>
    [HttpPatch]
    [Route("{id:ulid}")]
    [ProducesResponseType(typeof(StatusResponseDto<EncoderProfile>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Ulid id, [FromBody] UpdateProfileRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update encoder profiles");

        EncoderProfile? profile = await profileRepository.GetProfileAsync(id);

        if (profile is null)
            return NotFoundResponse("Encoder profile not found");

        try
        {
            if (request.Name is not null)
                profile.Name = request.Name;

            if (request.Container is not null)
                profile.Container = request.Container;

            if (request.Param is not null)
                profile.Param = request.Param;

            if (request.VideoProfiles is not null)
                profile.VideoProfiles = request.VideoProfiles;

            if (request.AudioProfiles is not null)
                profile.AudioProfiles = request.AudioProfiles;

            if (request.SubtitleProfiles is not null)
                profile.SubtitleProfiles = request.SubtitleProfiles;

            EncoderProfile updated = await profileRepository.UpdateProfileAsync(profile);

            return Ok(new StatusResponseDto<EncoderProfile>
            {
                Status = "ok",
                Data = updated,
                Message = "Successfully updated encoder profile.",
                Args = [profile.Name]
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntityResponse($"Failed to update encoder profile: {e.Message}");
        }
    }

    /// <summary>
    /// Delete an encoding profile
    /// </summary>
    /// <param name="id">Profile ID (ULID)</param>
    /// <returns>Success message</returns>
    [HttpDelete]
    [Route("{id:ulid}")]
    [ProducesResponseType(typeof(StatusResponseDto<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Destroy(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete encoder profiles");

        EncoderProfile? profile = await profileRepository.GetProfileAsync(id);

        if (profile is null)
            return NotFoundResponse("Encoder profile not found");

        try
        {
            await profileRepository.DeleteProfileAsync(id);

            return Ok(new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "Successfully deleted encoder profile.",
                Args = [profile.Name]
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntityResponse($"Failed to delete encoder profile: {e.Message}");
        }
    }

    /// <summary>
    /// Get the default encoding profile
    /// </summary>
    /// <returns>The default encoding profile if one exists</returns>
    [HttpGet]
    [Route("default")]
    [ProducesResponseType(typeof(StatusResponseDto<EncoderProfile>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDefault()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoder profiles");

        EncoderProfile? profile = await profileRepository.GetDefaultProfileAsync();

        if (profile is null)
            return NotFoundResponse("No default encoder profile found");

        return Ok(new StatusResponseDto<EncoderProfile>
        {
            Status = "ok",
            Data = profile,
            Message = "Successfully retrieved default encoder profile."
        });
    }

    /// <summary>
    /// Duplicate an existing encoding profile
    /// </summary>
    /// <param name="id">Profile ID to duplicate</param>
    /// <param name="request">Optional new name for the duplicated profile</param>
    /// <returns>The duplicated profile</returns>
    [HttpPost]
    [Route("{id:ulid}/duplicate")]
    [ProducesResponseType(typeof(StatusResponseDto<EncoderProfile>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Duplicate(Ulid id, [FromBody] DuplicateProfileRequest? request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create encoder profiles");

        EncoderProfile? sourceProfile = await profileRepository.GetProfileAsync(id);

        if (sourceProfile is null)
            return NotFoundResponse("Source encoder profile not found");

        try
        {
            EncoderProfile newProfile = new()
            {
                Name = request?.Name ?? $"{sourceProfile.Name} (Copy)",
                Container = sourceProfile.Container,
                Param = sourceProfile.Param,
                VideoProfiles = sourceProfile.VideoProfiles,
                AudioProfiles = sourceProfile.AudioProfiles,
                SubtitleProfiles = sourceProfile.SubtitleProfiles
            };

            EncoderProfile created = await profileRepository.CreateProfileAsync(newProfile);

            return Ok(new StatusResponseDto<EncoderProfile>
            {
                Status = "ok",
                Data = created,
                Message = "Successfully duplicated encoder profile.",
                Args = [sourceProfile.Name, created.Name]
            });
        }
        catch (Exception e)
        {
            return UnprocessableEntityResponse($"Failed to duplicate encoder profile: {e.Message}");
        }
    }
}

/// <summary>
/// Request DTO for creating a new encoding profile
/// </summary>
public class CreateProfileRequest
{
    /// <summary>
    /// Name of the profile (required)
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Container format (hls, mp4, mkv)
    /// </summary>
    public string? Container { get; set; }

    /// <summary>
    /// Additional parameters as JSON string
    /// </summary>
    public string? Param { get; set; }

    /// <summary>
    /// Video encoding configurations
    /// </summary>
    public IVideoProfile[]? VideoProfiles { get; set; }

    /// <summary>
    /// Audio encoding configurations
    /// </summary>
    public IAudioProfile[]? AudioProfiles { get; set; }

    /// <summary>
    /// Subtitle handling configurations
    /// </summary>
    public ISubtitleProfile[]? SubtitleProfiles { get; set; }
}

/// <summary>
/// Request DTO for updating an existing encoding profile
/// </summary>
public class UpdateProfileRequest
{
    /// <summary>
    /// New name for the profile
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// New container format
    /// </summary>
    public string? Container { get; set; }

    /// <summary>
    /// New additional parameters
    /// </summary>
    public string? Param { get; set; }

    /// <summary>
    /// New video encoding configurations
    /// </summary>
    public IVideoProfile[]? VideoProfiles { get; set; }

    /// <summary>
    /// New audio encoding configurations
    /// </summary>
    public IAudioProfile[]? AudioProfiles { get; set; }

    /// <summary>
    /// New subtitle handling configurations
    /// </summary>
    public ISubtitleProfile[]? SubtitleProfiles { get; set; }
}

/// <summary>
/// Request DTO for duplicating a profile
/// </summary>
public class DuplicateProfileRequest
{
    /// <summary>
    /// Optional new name for the duplicated profile
    /// </summary>
    public string? Name { get; set; }
}
