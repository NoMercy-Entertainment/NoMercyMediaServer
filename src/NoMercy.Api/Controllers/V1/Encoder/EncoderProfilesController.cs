using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;
using NoMercy.Helpers.Extensions;
using AnalysisMediaInfo = NoMercy.Encoder.Analysis.MediaInfo;

namespace NoMercy.Api.Controllers.V1.Encoder;

[ApiController]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/encoder/profiles")]
public class EncoderProfilesController(
    IProfileValidator profileValidator,
    IMediaAnalyzer mediaAnalyzer,
    EncodingPresetRepository presetRepository
) : BaseController
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
    /// Returns a per-stream action plan and source-level warnings for the
    /// given profile applied to the given source file.
    ///
    /// <para>Always returns 200 when the source is accessible — warnings surface in the
    /// body as <c>source_warnings</c> chips, never as HTTP error codes.</para>
    /// <para>Returns 404 when the source file cannot be read (via
    /// <see cref="RuntimeErrors.SourceNotAccessible"/>).</para>
    /// </summary>
    [HttpPost("{id}/preview")]
    public async Task<IActionResult> Preview(
        string id,
        [FromBody] PreviewEncoderProfileRequest request,
        CancellationToken ct
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to preview encodes");

        if (string.IsNullOrWhiteSpace(request.ProfileJson))
        {
            PreviewResponse emptyResponse = new(
                ProfileId: id,
                SourceVideoFileId: request.SourcePath ?? string.Empty,
                SourceAnalysis: new SourceAnalysisDto(
                    string.Empty,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    false,
                    null,
                    null,
                    null
                ),
                PerStreamPlan: new PerStreamPlan([], [], []),
                SourceWarnings:
                [
                    new EncoderRule(
                        EncoderRuleId.ProfileNameMissing,
                        EncoderRuleSeverity.Error,
                        "profile_json",
                        "profile_json is required.",
                        "Supply the full profile JSON in the profile_json field."
                    ),
                ],
                EstimatedFps: 0,
                EstimatedDurationSeconds: 0,
                EncoderHandle: "auto"
            );
            return Ok(emptyResponse);
        }

        EncodingProfile? profile;
        try
        {
            profile = JsonConvert.DeserializeObject<EncodingProfile>(request.ProfileJson);
        }
        catch (JsonException ex)
        {
            PreviewResponse parseErrorResponse = new(
                ProfileId: id,
                SourceVideoFileId: request.SourcePath ?? string.Empty,
                SourceAnalysis: new SourceAnalysisDto(
                    string.Empty,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    false,
                    null,
                    null,
                    null
                ),
                PerStreamPlan: new PerStreamPlan([], [], []),
                SourceWarnings:
                [
                    new EncoderRule(
                        EncoderRuleId.ProfileNameMissing,
                        EncoderRuleSeverity.Error,
                        "profile_json",
                        $"Profile JSON is malformed: {ex.Message}",
                        "Fix the JSON syntax error and resubmit."
                    ),
                ],
                EstimatedFps: 0,
                EstimatedDurationSeconds: 0,
                EncoderHandle: "auto"
            );
            return Ok(parseErrorResponse);
        }

        if (profile is null)
        {
            PreviewResponse nullResponse = new(
                ProfileId: id,
                SourceVideoFileId: request.SourcePath ?? string.Empty,
                SourceAnalysis: new SourceAnalysisDto(
                    string.Empty,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    false,
                    null,
                    null,
                    null
                ),
                PerStreamPlan: new PerStreamPlan([], [], []),
                SourceWarnings:
                [
                    new EncoderRule(
                        EncoderRuleId.ProfileNameMissing,
                        EncoderRuleSeverity.Error,
                        "profile_json",
                        "Profile JSON deserialized to null — check the outer object is present.",
                        "Ensure the JSON root is an object, not null or an array."
                    ),
                ],
                EstimatedFps: 0,
                EstimatedDurationSeconds: 0,
                EncoderHandle: "auto"
            );
            return Ok(nullResponse);
        }

        string sourcePath = request.SourcePath ?? string.Empty;

        AnalysisMediaInfo mediaInfo;
        try
        {
            mediaInfo = await mediaAnalyzer.AnalyzeAsync(sourcePath, ct);
        }
        catch
        {
            throw RuntimeErrors.SourceNotAccessible(sourcePath);
        }

        PreviewResult result = PreviewEngine.Analyze(profile, mediaInfo);

        PreviewResponse response = new(
            ProfileId: id,
            SourceVideoFileId: sourcePath,
            SourceAnalysis: result.SourceAnalysis,
            PerStreamPlan: result.Plan,
            SourceWarnings: result.SourceWarnings,
            EstimatedFps: result.EstimatedFps,
            EstimatedDurationSeconds: result.EstimatedDurationSeconds,
            EncoderHandle: result.EncoderHandle
        );

        return Ok(response);
    }

    /// <summary>
    /// Rejects any attempt to modify a built-in preset, returning HTTP 422
    /// with a structured <see cref="ValidationEnvelope"/> that includes the
    /// stable rule ID <c>profile.builtin_readonly</c> and a fix hint
    /// pointing at the clone endpoint.
    ///
    /// Wire this guard into any future PUT / PATCH endpoint on this controller
    /// by calling <c>await GuardBuiltinAsync(id)</c> before mutating the row.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdateEncoderProfileRequest request
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update profiles");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid profile id");

        EncodingPreset? existing = await presetRepository.GetByIdAsync(presetId);
        if (existing is null)
            return NotFoundResponse("Profile not found");

        if (existing.IsBuiltIn)
        {
            ValidationEnvelope envelope = ValidationEnvelope.FromRules([
                new EncoderRule(
                    EncoderRuleId.ProfileBuiltinReadonly,
                    EncoderRuleSeverity.Error,
                    "id",
                    "Built-in presets are read-only — clone the preset to edit it.",
                    $"POST /api/v1/encoder/profiles/{id}/clone to make an editable copy."
                ),
            ]);
            return UnprocessableEntity(envelope);
        }

        // Forward mutations through the repository so the UpdatedAt stamp
        // and change-tracking are handled consistently.
        EncodingPreset? updated = await presetRepository.UpdateAsync(
            presetId,
            preset =>
            {
                if (request.Name is not null)
                    preset.Name = request.Name;
                if (request.Description is not null)
                    preset.Description = request.Description;
                if (request.ProfileJson is not null)
                    preset.ProfileJson = request.ProfileJson;
            }
        );

        return Ok(updated);
    }

    /// <summary>
    /// Clones a preset (built-in or user-created) into a new editable preset.
    /// The clone gets a fresh <c>Ulid</c>, <c>IsBuiltIn = false</c>, and a
    /// <c>ParentId</c> pointing at the source so the UI can show provenance.
    /// </summary>
    [HttpPost("{id}/clone")]
    public async Task<IActionResult> Clone(string id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to clone profiles");

        if (!Ulid.TryParse(id, out Ulid sourceId))
            return BadRequestResponse("Invalid profile id");

        EncodingPreset? source = await presetRepository.GetByIdAsync(sourceId);
        if (source is null)
            return NotFoundResponse("Profile not found");

        // Build the clone: deserialize the source profile JSON, stamp a new
        // deterministic-free Ulid, clear IsBuiltin, set ParentId.
        EncodingProfile? sourceProfile;
        try
        {
            sourceProfile = JsonConvert.DeserializeObject<EncodingProfile>(source.ProfileJson);
        }
        catch (JsonException ex)
        {
            return BadRequestResponse($"Source profile JSON is malformed: {ex.Message}");
        }

        if (sourceProfile is null)
            return BadRequestResponse("Source profile JSON deserialized to null");

        Ulid newId = Ulid.NewUlid();
        EncodingProfile clonedProfile = sourceProfile with
        {
            Id = newId,
            IsBuiltin = false,
            ParentId = sourceProfile.Id,
            Name = source.Name + " (copy)",
        };

        string clonedJson = JsonConvert.SerializeObject(clonedProfile);

        EncodingPreset cloneRow = new()
        {
            Id = newId,
            Name = clonedProfile.Name,
            Description = source.Description,
            Author = source.Author,
            Tags = source.Tags,
            ProfileJson = clonedJson,
            ParentPresetId = source.Id,
            IsBuiltIn = false,
        };

        EncodingPreset saved = await presetRepository.CreateAsync(cloneRow);

        return CreatedAtAction(
            nameof(Clone),
            new { id = saved.Id.ToString() },
            new
            {
                id = saved.Id,
                name = saved.Name,
                profile = clonedProfile,
            }
        );
    }
}

public record ValidateEncoderProfileRequest(
    [property: JsonProperty("profile_json")] string ProfileJson
);

public record PreviewEncoderProfileRequest(
    [property: JsonProperty("profile_json")] string ProfileJson,
    [property: JsonProperty("source_path")] string? SourcePath
);

public record UpdateEncoderProfileRequest(
    [property: JsonProperty("name")] string? Name = null,
    [property: JsonProperty("description")] string? Description = null,
    [property: JsonProperty("profile_json")] string? ProfileJson = null
);
