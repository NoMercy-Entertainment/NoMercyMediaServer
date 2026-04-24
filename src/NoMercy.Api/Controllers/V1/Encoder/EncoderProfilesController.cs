using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.Controllers.V1.Encoder;

[ApiController]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/encoder/profiles")]
public class EncoderProfilesController(IProfileValidator profileValidator) : BaseController
{
    /// <summary>
    /// Validates an encoding profile and returns a structured envelope with
    /// errors (blocking) and warnings (non-blocking) bucketed separately.
    ///
    /// Always returns 200 — <c>valid: false</c> flows in the body. Validation
    /// informs; it never gates the HTTP request.
    /// </summary>
    [HttpPost("validate")]
    public IActionResult Validate([FromBody] ValidateEncoderProfileRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to validate profiles");

        if (string.IsNullOrWhiteSpace(request.ProfileJson))
        {
            ValidationEnvelope empty = ValidationEnvelope.FromRules([
                new EncoderRule(
                    EncoderRuleId.ProfileNameMissing,
                    EncoderRuleSeverity.Error,
                    "profile_json",
                    "profile_json is required",
                    "Supply the full profile JSON in the profile_json field."
                ),
            ]);
            return Ok(empty);
        }

        EncodingProfile? profile;
        try
        {
            profile = JsonConvert.DeserializeObject<EncodingProfile>(request.ProfileJson);
        }
        catch (JsonException ex)
        {
            ValidationEnvelope parseError = ValidationEnvelope.FromRules([
                new EncoderRule(
                    EncoderRuleId.ProfileNameMissing,
                    EncoderRuleSeverity.Error,
                    "profile_json",
                    $"Profile JSON is malformed: {ex.Message}",
                    "Fix the JSON syntax error and resubmit."
                ),
            ]);
            return Ok(parseError);
        }

        if (profile is null)
        {
            ValidationEnvelope nullError = ValidationEnvelope.FromRules([
                new EncoderRule(
                    EncoderRuleId.ProfileNameMissing,
                    EncoderRuleSeverity.Error,
                    "profile_json",
                    "Profile JSON deserialized to null — check the outer object is present",
                    "Ensure the JSON root is an object, not null or an array."
                ),
            ]);
            return Ok(nullError);
        }

        ValidationEnvelope envelope = profileValidator.ValidateAsEnvelope(profile);
        return Ok(envelope);
    }

    /// <summary>
    /// Placeholder for Phase 1.8 — per-stream plan + encode estimates.
    /// Returns 501 until that phase lands.
    /// </summary>
    [HttpPost("{id}/preview")]
    public IActionResult Preview(string id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to preview encodes");

        EncoderErrorShape shape = new(
            "not_implemented",
            "Preview is not yet implemented.",
            "Phase 1.8 will land the per-stream plan and encode estimates. Use POST /api/v1/dashboard/encoding/presets/preview in the interim.",
            new { phase = "1.8", profile_id = id }
        );

        return StatusCode(501, shape);
    }
}

public record ValidateEncoderProfileRequest(
    [property: JsonProperty("profile_json")] string ProfileJson
);
