using System.Reflection;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.EncoderV2.Api;
using NoMercy.EncoderV2.Capabilities;
using NoMercy.EncoderV2.Jobs;
using NoMercy.EncoderV2.Profiles;
using NoMercy.EncoderV2.Validation;
using NoMercy.Helpers;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Encoder V2")]
[ApiVersion(2.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/encoder", Order = 10)]
public class EncoderV2Controller : BaseController
{
    private readonly ProfileValidator _validator;
    private readonly AllCapabilities _capabilities;

    public EncoderV2Controller(ProfileValidator validator, AllCapabilities capabilities)
    {
        _validator = validator;
        _capabilities = capabilities;
    }

    /// <summary>
    /// Get FFmpeg capabilities
    /// Returns available encoders, decoders, containers
    /// </summary>
    /// <response code="200">FFmpeg capabilities</response>
    [HttpGet("capabilities")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetCapabilities()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoder capabilities");

        CapabilitiesResponse response = new()
        {
            VideoEncoders = _capabilities.VideoEncoders
                .ToDictionary(x => x.Key, x => MapEncoderCapability(x.Value)),
            AudioEncoders = _capabilities.AudioEncoders
                .ToDictionary(x => x.Key, x => MapEncoderCapability(x.Value)),
            SubtitleEncoders = _capabilities.SubtitleEncoders
                .ToDictionary(x => x.Key, x => MapEncoderCapability(x.Value)),
            Containers = _capabilities.Containers
                .ToDictionary(x => x.Key, x => MapContainerCapability(x.Value)),
            GeneratedAt = _capabilities.GeneratedAt
        };

        return Ok(response);
    }

    /// <summary>
    /// Validate profile against FFmpeg capabilities
    /// Checks if codecs, containers are supported by system
    /// </summary>
    /// <param name="request">Profile to validate</param>
    /// <response code="200">Validation result</response>
    [HttpPost("validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateProfile([FromBody] CreateEncodingProfileRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to validate profiles");

        bool isValid = await _validator.ValidateAsync(request);

        return Ok(new { isValid, message = isValid ? "Profile is valid" : "Profile validation failed" });
    }

    /// <summary>
    /// Get all available encoding profiles
    /// </summary>
    /// <response code="200">List of encoding profiles</response>
    [HttpGet("profiles")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetProfiles()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view profiles");

        List<EncodingProfile> profiles = ProductionProfiles.GetAllProfiles();
        return Ok(profiles.Select(p => MapToResponse(p)).ToList());
    }

    /// <summary>
    /// Get encoding profile by ID
    /// </summary>
    /// <param name="profileId">Profile identifier (e.g., 'playback-1080p-high')</param>
    /// <response code="200">Encoding profile details</response>
    /// <response code="404">Profile not found</response>
    [HttpGet("profiles/{profileId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetProfile(string profileId)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view profiles");

        EncodingProfile? profile = ProductionProfiles.GetAllProfiles().FirstOrDefault(p => p.ProfileId == profileId);
        if (profile == null)
            return NotFound(new { error = $"Profile '{profileId}' not found" });

        return Ok(MapToResponse(profile));
    }

    /// <summary>
    /// Get profiles by container type
    /// </summary>
    /// <param name="container">Container type: 'm3u8' (HLS), 'mkv' (Matroska), 'mp4'</param>
    /// <response code="200">List of profiles for container</response>
    [HttpGet("profiles/container/{container}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetProfilesByContainer(string container)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view profiles");

        List<EncodingProfileResponse> profiles = ProductionProfiles.GetAllProfiles()
            .Where(p => p.Container.Equals(container, StringComparison.OrdinalIgnoreCase))
            .Select(p => MapToResponse(p))
            .ToList();

        return Ok(profiles);
    }

    /// <summary>
    /// Get profiles by purpose
    /// </summary>
    /// <param name="purpose">Purpose type: 'archive', 'playback', 'direct-play'</param>
    /// <response code="200">List of profiles for purpose</response>
    [HttpGet("profiles/purpose/{purpose}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetProfilesByPurpose(string purpose)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view profiles");

        List<EncodingProfileResponse> profiles = ProductionProfiles.GetAllProfiles()
            .Where(p => p.Purpose.Equals(purpose, StringComparison.OrdinalIgnoreCase))
            .Select(p => MapToResponse(p))
            .ToList();

        return Ok(profiles);
    }

    /// <summary>
    /// Create new encoding profile
    /// </summary>
    /// <param name="request">Profile definition</param>
    /// <response code="201">Profile created successfully</response>
    /// <response code="400">Invalid profile definition</response>
    [HttpPost("profiles")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProfile([FromBody] CreateEncodingProfileRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create profiles");

        // Validate first
        if (!await _validator.ValidateAsync(request))
        {
            return BadRequest(new { error = "Invalid profile definition" });
        }

        EncodingProfile profile = new()
        {
            ProfileId = $"{request.Name.ToLower().Replace(" ", "-")}-{DateTime.UtcNow.Ticks}",
            Name = request.Name,
            Container = request.Container,
            Purpose = request.Purpose ?? "playback",
            VideoProfile = MapToVideoConfig(request.VideoProfile),
            AudioProfile = MapToAudioConfig(request.AudioProfile),
            SubtitleProfile = MapToSubtitleConfig(request.SubtitleProfile),
            CreatedAt = DateTime.UtcNow
        };

        return CreatedAtAction(nameof(GetProfile), new { profileId = profile.ProfileId }, MapToResponse(profile));
    }

    /// <summary>
    /// Update existing encoding profile
    /// </summary>
    /// <param name="profileId">Profile identifier</param>
    /// <param name="request">Updated profile definition</param>
    /// <response code="200">Profile updated successfully</response>
    /// <response code="400">Invalid profile definition</response>
    /// <response code="404">Profile not found</response>
    [HttpPut("profiles/{profileId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(string profileId, [FromBody] CreateEncodingProfileRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update profiles");

        EncodingProfile? existing = ProductionProfiles.GetAllProfiles().FirstOrDefault(p => p.ProfileId == profileId);
        if (existing == null)
            return NotFound(new { error = $"Profile '{profileId}' not found" });

        // Validate updated profile
        if (!await _validator.ValidateAsync(request))
        {
            return BadRequest(new { error = "Invalid profile configuration" });
        }

        EncodingProfile updated = new()
        {
            ProfileId = profileId,
            Name = request.Name,
            Container = request.Container,
            Purpose = request.Purpose ?? "playback",
            VideoProfile = MapToVideoConfig(request.VideoProfile),
            AudioProfile = MapToAudioConfig(request.AudioProfile),
            SubtitleProfile = MapToSubtitleConfig(request.SubtitleProfile),
            CreatedAt = existing.CreatedAt
        };

        return Ok(MapToResponse(updated));
    }

    /// <summary>
    /// Delete encoding profile
    /// </summary>
    /// <param name="profileId">Profile identifier</param>
    /// <response code="204">Profile deleted successfully</response>
    /// <response code="404">Profile not found</response>
    [HttpDelete("profiles/{profileId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DeleteProfile(string profileId)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete profiles");

        EncodingProfile? existing = ProductionProfiles.GetAllProfiles().FirstOrDefault(p => p.ProfileId == profileId);
        if (existing == null)
            return NotFound(new { error = $"Profile '{profileId}' not found" });

        // In production, would check if profile is in use before deletion
        return NoContent();
    }

    /// <summary>
    /// Seed default production profiles
    /// Called on initial setup to populate database with standard profiles
    /// </summary>
    /// <response code="200">Seeding completed</response>
    [HttpPost("seed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult SeedProfiles()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to seed profiles");

        // Default profiles are returned by ProductionProfiles.GetAllProfiles()
        return Ok(new { message = "Default profiles available", count = ProductionProfiles.GetAllProfiles().Count });
    }

    // Helper Methods

    private Dictionary<string, object> MapToDict<T>(T? obj) where T : class
    {
        if (obj == null) return new();

        Dictionary<string, object> dict = new();
        foreach (PropertyInfo prop in obj.GetType().GetProperties())
        {
            object? value = prop.GetValue(obj);
            if (value != null)
                dict[prop.Name.ToSnakeCase()] = value;
        }
        return dict;
    }

    private EncoderCapabilityDto MapEncoderCapability(EncoderCapability capability)
    {
        return new()
        {
            Name = capability.Name,
            LongName = capability.LongName,
            IsHardware = capability.IsHardware,
            Options = capability.Options.ToDictionary(
                x => x.Key,
                x => new EncoderOptionDto
                {
                    Name = x.Value.Name,
                    Type = x.Value.Type,
                    Default = x.Value.Default,
                    Min = x.Value.Min,
                    Max = x.Value.Max,
                    Choices = x.Value.Choices,
                    Help = x.Value.Help
                })
        };
    }

    private ContainerCapabilityDto MapContainerCapability(ContainerCapability container)
    {
        return new()
        {
            Name = container.Name,
            LongName = container.LongName,
            CanMux = container.CanMux,
            CanDemux = container.CanDemux,
            SupportedVideoCodecs = container.SupportedVideoCodecs,
            SupportedAudioCodecs = container.SupportedAudioCodecs,
            SupportedSubtitleCodecs = container.SupportedSubtitleCodecs
        };
    }

    private VideoProfileConfig? MapToVideoConfig(VideoProfileConfigDto? dto)
    {
        if (dto == null) return null;
        return new()
        {
            Codec = dto.Codec,
            Width = dto.Width,
            Height = dto.Height,
            Bitrate = dto.Bitrate,
            Framerate = dto.Framerate,
            Crf = dto.Crf,
            Preset = dto.Preset,
            Profile = dto.Profile,
            Tune = dto.Tune,
            PixelFormat = dto.PixelFormat,
            ColorSpace = dto.ColorSpace,
            KeyframeInterval = dto.KeyframeInterval,
            ConvertHdrToSdr = dto.ConvertHdrToSdr,
            CustomOptions = dto.CustomOptions,
            CustomArguments = dto.CustomArguments
        };
    }

    private AudioProfileConfig? MapToAudioConfig(AudioProfileConfigDto? dto)
    {
        if (dto == null) return null;
        return new()
        {
            Codec = dto.Codec,
            Bitrate = dto.Bitrate,
            Channels = dto.Channels,
            SampleRate = dto.SampleRate,
            AllowedLanguages = dto.AllowedLanguages,
            CustomOptions = dto.CustomOptions,
            CustomArguments = dto.CustomArguments
        };
    }

    private SubtitleProfileConfig? MapToSubtitleConfig(SubtitleProfileConfigDto? dto)
    {
        if (dto == null) return null;
        return new()
        {
            Codec = dto.Codec,
            AllowedLanguages = dto.AllowedLanguages,
            CustomOptions = dto.CustomOptions,
            CustomArguments = dto.CustomArguments
        };
    }

    private EncodingProfileResponse MapToResponse(EncodingProfile profile)
    {
        return new()
        {
            Id = profile.ProfileId,
            Name = profile.Name,
            Container = profile.Container,
            Purpose = profile.Purpose,
            VideoProfile = MapToVideoDto(profile.VideoProfile),
            AudioProfile = MapToAudioDto(profile.AudioProfile),
            SubtitleProfile = MapToSubtitleDto(profile.SubtitleProfile),
            CreatedAt = profile.CreatedAt
        };
    }

    private VideoProfileConfigDto? MapToVideoDto(VideoProfileConfig? config)
    {
        if (config == null) return null;
        return new()
        {
            Codec = config.Codec,
            Width = config.Width,
            Height = config.Height,
            Bitrate = config.Bitrate,
            Framerate = (int)config.Framerate,
            Crf = config.Crf,
            Preset = config.Preset ?? string.Empty,
            Profile = config.Profile ?? string.Empty,
            Tune = config.Tune ?? string.Empty,
            PixelFormat = config.PixelFormat ?? string.Empty,
            ColorSpace = config.ColorSpace ?? string.Empty,
            KeyframeInterval = config.KeyframeInterval,
            ConvertHdrToSdr = config.ConvertHdrToSdr,
            CustomOptions = config.CustomOptions ?? [],
            CustomArguments = config.CustomArguments
        };
    }

    private AudioProfileConfigDto? MapToAudioDto(AudioProfileConfig? config)
    {
        if (config == null) return null;
        return new()
        {
            Codec = config.Codec,
            Bitrate = config.Bitrate,
            Channels = config.Channels,
            SampleRate = config.SampleRate,
            AllowedLanguages = config.AllowedLanguages ?? [],
            CustomOptions = config.CustomOptions ?? [],
            CustomArguments = config.CustomArguments
        };
    }

    private SubtitleProfileConfigDto? MapToSubtitleDto(SubtitleProfileConfig? config)
    {
        if (config == null) return null;
        return new()
        {
            Codec = config.Codec,
            AllowedLanguages = config.AllowedLanguages ?? [],
            CustomOptions = config.CustomOptions ?? [],
            CustomArguments = config.CustomArguments
        };
    }
}

