using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;
using NoMercy.Helpers.Extensions;
using AnalysisMediaInfo = NoMercy.Encoder.Analysis.MediaInfo;

namespace NoMercy.Api.Controllers.V1.Encoder;

/// <summary>
/// Phase 2 primary controller — supersedes the legacy
/// /api/v1/dashboard/encoding/presets controller. The dashboard should migrate
/// to these routes. Legacy endpoints remain operational but carry
/// <c>[Obsolete]</c> markers.
/// </summary>
[ApiController]
[Tags("Encoder Profiles")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/encoder/profiles")]
public class EncoderProfilesController(
    IProfileValidator profileValidator,
    IMediaAnalyzer mediaAnalyzer,
    IProfileSignatureVerifier signatureVerifier,
    IHttpClientFactory httpClientFactory,
    MediaContext mediaContext,
    EncodingPresetRepository presetRepository
) : BaseController
{
    /// <summary>
    /// Returns a paginated list of encoding profiles, built-in rows first
    /// then user-created alphabetically. Optionally filtered by <paramref name="tag"/>.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] int pageSize = 100,
        [FromQuery] int pageIndex = 0,
        [FromQuery] string? tag = null
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view profiles");

        pageSize = Math.Clamp(pageSize, 1, 500);
        if (pageIndex < 0)
            pageIndex = 0;

        List<EncodingPreset> presets = await presetRepository.ListAsync(pageSize, pageIndex, tag);
        int total = await presetRepository.GetTotalCountAsync();

        return Ok(
            new
            {
                data = presets,
                meta = new
                {
                    total,
                    pageSize,
                    pageIndex,
                    totalPages = (int)Math.Ceiling((double)total / pageSize),
                },
            }
        );
    }

    /// <summary>
    /// Returns a single encoding profile by its <see cref="Ulid"/> id.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view profiles");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid profile id");

        EncodingPreset? preset = await presetRepository.GetByIdAsync(presetId);
        if (preset is null)
            return NotFoundResponse("Profile not found");

        return Ok(preset);
    }

    /// <summary>
    /// Creates a new user-owned encoding profile. <c>IsBuiltIn</c> is always
    /// stamped <c>false</c> regardless of what the caller sends.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEncoderProfileRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create profiles");

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequestResponse("name is required");
        if (string.IsNullOrWhiteSpace(request.ProfileJson))
            return BadRequestResponse("profile_json is required");

        EncodingPreset? existing = await presetRepository.GetByNameAsync(request.Name);
        if (existing is not null)
            return ConflictResponse($"A profile named '{request.Name}' already exists");

        EncodingPreset preset = new()
        {
            Name = request.Name,
            Description = request.Description,
            Author = request.Author,
            Tags = request.Tags,
            ProfileJson = request.ProfileJson,
            ParentPresetId = request.ParentPresetId,
            IsBuiltIn = false,
        };

        EncodingPreset saved = await presetRepository.CreateAsync(preset);
        return Ok(saved);
    }

    /// <summary>
    /// Returns the distinct set of tags in use across all profiles, sorted
    /// alphabetically. Tags are stored as comma-separated values on each row.
    /// </summary>
    [HttpGet("tags")]
    public async Task<IActionResult> Tags()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view profiles");

        IReadOnlyList<string> tags = await presetRepository.GetAllTagsAsync();
        return Ok(new { data = tags });
    }

    /// <summary>
    /// Resolves a profile by walking its parent chain via
    /// <see cref="IPresetResolver"/>, returning the fully-merged
    /// <see cref="EncodingProfile"/>. This is distinct from the new
    /// <c>IProfileResolver</c> — the preset resolver expands inheritance,
    /// the profile resolver walks the V3 parent-id graph.
    /// </summary>
    [HttpGet("{id}/resolve")]
    public async Task<IActionResult> Resolve(
        string id,
        [FromServices] IPresetResolver presetResolver
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view profiles");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid profile id");

        EncodingPreset? leaf = await presetRepository.GetByIdAsync(presetId);
        if (leaf is null)
            return NotFoundResponse("Profile not found");

        string? parentName = null;
        if (leaf.ParentPresetId is Ulid parentId)
        {
            EncodingPreset? parent = await presetRepository.GetByIdAsync(parentId);
            parentName = parent?.Name;
        }

        PresetResolveRequest resolveRequest = new(leaf.Name, leaf.ProfileJson, parentName);

        try
        {
            EncodingProfile resolved = presetResolver.Resolve(
                resolveRequest,
                new EncoderProfilesPresetLookup(presetRepository)
            );
            return Ok(resolved);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse($"Profile could not be resolved: {ex.Message}");
        }
    }

    /// <summary>
    /// Permanently deletes a user-created profile. Returns HTTP 422 with a
    /// structured <see cref="ValidationEnvelope"/> when called on a built-in
    /// profile — built-ins are seeded and cannot be deleted.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete profiles");

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
                    "Built-in profiles cannot be deleted — clone the profile to make an editable copy.",
                    $"POST /api/v1/encoder/profiles/{id}/clone to create an editable copy, then delete that."
                ),
            ]);
            return UnprocessableEntity(envelope);
        }

        try
        {
            bool removed = await presetRepository.DeleteAsync(presetId);
            if (!removed)
                return NotFoundResponse("Profile not found");

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ConflictResponse(ex.Message);
        }
    }

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

    /// <summary>
    /// Imports an encoding profile from either a raw JSON body or a remote HTTPS URL.
    ///
    /// <para>If the profile carries <c>PublisherKeyFingerprint</c> and <c>Signature</c>
    /// the Ed25519 signature is verified against the <c>trusted_publisher_keys</c> table
    /// before the profile is persisted.</para>
    ///
    /// <para>Unsigned profiles are accepted only when <c>?trust_unsigned=true</c> is
    /// supplied — this is an explicit opt-in so administrators cannot accidentally
    /// import tampered community profiles.</para>
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import(
        [FromBody] ImportProfileRequest request,
        [FromQuery] bool trust_unsigned = false,
        CancellationToken ct = default
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to import profiles");

        // --- 1. Resolve JSON source ------------------------------------------
        string profileJson;

        if (!string.IsNullOrWhiteSpace(request.ProfileJson))
        {
            profileJson = request.ProfileJson;
        }
        else if (!string.IsNullOrWhiteSpace(request.Url))
        {
            if (!request.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                ValidationEnvelope httpsError = ValidationEnvelope.FromRules([
                    new EncoderRule(
                        EncoderRuleId.ImportHttpNotHttps,
                        EncoderRuleSeverity.Error,
                        "url",
                        "Profile URLs must use HTTPS — plain HTTP is not permitted.",
                        "Replace the URL scheme with https:// before importing."
                    ),
                ]);
                return UnprocessableEntity(httpsError);
            }

            HttpClient client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            client.MaxResponseContentBufferSize = 256 * 1024;

            HttpResponseMessage response;
            try
            {
                using HttpRequestMessage httpReq = new(HttpMethod.Get, request.Url);
                response = await client.SendAsync(httpReq, ct);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                ValidationEnvelope fetchError = ValidationEnvelope.FromRules([
                    new EncoderRule(
                        EncoderRuleId.ImportHttpNotHttps,
                        EncoderRuleSeverity.Error,
                        "url",
                        $"Failed to fetch profile from URL: {ex.Message}",
                        "Verify the URL is reachable and returns valid JSON."
                    ),
                ]);
                return UnprocessableEntity(fetchError);
            }

            profileJson = await response.Content.ReadAsStringAsync(ct);
        }
        else
        {
            ValidationEnvelope missingInput = ValidationEnvelope.FromRules([
                new EncoderRule(
                    EncoderRuleId.ProfileNameMissing,
                    EncoderRuleSeverity.Error,
                    "profile_json",
                    "Either profile_json or url must be provided.",
                    "Supply profile_json with the raw JSON or url with an HTTPS URL pointing to the profile."
                ),
            ]);
            return UnprocessableEntity(missingInput);
        }

        // --- 2. Deserialise --------------------------------------------------
        EncodingProfile? profile;
        try
        {
            profile = JsonConvert.DeserializeObject<EncodingProfile>(profileJson);
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
            return UnprocessableEntity(parseError);
        }

        if (profile is null)
        {
            ValidationEnvelope nullError = ValidationEnvelope.FromRules([
                new EncoderRule(
                    EncoderRuleId.ProfileNameMissing,
                    EncoderRuleSeverity.Error,
                    "profile_json",
                    "Profile JSON deserialized to null — check the outer object is present.",
                    "Ensure the JSON root is an object, not null or an array."
                ),
            ]);
            return UnprocessableEntity(nullError);
        }

        // --- 3. Signature verification ---------------------------------------
        bool hasSigning =
            !string.IsNullOrWhiteSpace(profile.PublisherKeyFingerprint)
            && !string.IsNullOrWhiteSpace(profile.Signature);

        if (hasSigning)
        {
            EncoderRule? rejection = signatureVerifier.Verify(
                profileJson,
                profile.PublisherKeyFingerprint!,
                profile.Signature!,
                fingerprint =>
                    mediaContext
                        .TrustedPublisherKeys.AsNoTracking()
                        .FirstOrDefault(k => k.Fingerprint == fingerprint)
            );

            if (rejection is not null)
            {
                ValidationEnvelope sigError = ValidationEnvelope.FromRules([rejection]);
                return UnprocessableEntity(sigError);
            }
        }
        else
        {
            if (!trust_unsigned)
            {
                ValidationEnvelope unsignedError = ValidationEnvelope.FromRules([
                    new EncoderRule(
                        EncoderRuleId.ImportUnsignedRequiresFlag,
                        EncoderRuleSeverity.Error,
                        "trust_unsigned",
                        "This profile has no publisher signature. Pass ?trust_unsigned=true to import it anyway.",
                        "Add ?trust_unsigned=true to the request URL to accept unsigned profiles."
                    ),
                ]);
                return UnprocessableEntity(unsignedError);
            }
        }

        // --- 4. Persist ------------------------------------------------------
        Ulid newId = Ulid.NewUlid();
        EncodingProfile importedProfile = profile with { Id = newId, IsBuiltin = false };

        string savedJson = JsonConvert.SerializeObject(importedProfile);

        EncodingPreset preset = new()
        {
            Id = newId,
            Name = importedProfile.Name,
            Description = importedProfile.Description,
            ProfileJson = savedJson,
            IsBuiltIn = false,
        };

        EncodingPreset saved = await presetRepository.CreateAsync(preset);

        return CreatedAtAction(
            nameof(Import),
            new { id = saved.Id.ToString() },
            new
            {
                id = saved.Id,
                name = saved.Name,
                profile = importedProfile,
            }
        );
    }

    /// <summary>
    /// Exports a profile as a JSON file download. The exported JSON includes all
    /// Phase 2.1 fields (<c>PublisherKeyFingerprint</c>, <c>Signature</c>) if
    /// present on the stored profile. Signing is the publisher's responsibility
    /// — this endpoint does not add a signature.
    /// </summary>
    [HttpGet("{id}/export")]
    public async Task<IActionResult> Export(string id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to export profiles");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid profile id");

        EncodingPreset? preset = await presetRepository.GetByIdAsync(presetId);
        if (preset is null)
            return NotFoundResponse("Profile not found");

        string fileName = preset.Name.Replace(" ", "_").Replace("/", "-") + ".json";

        Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";

        return Content(preset.ProfileJson, "application/json");
    }
}

public record CreateEncoderProfileRequest(
    [property: JsonProperty("name")] string Name,
    [property: JsonProperty("profile_json")] string ProfileJson,
    [property: JsonProperty("description")] string? Description = null,
    [property: JsonProperty("author")] string? Author = null,
    [property: JsonProperty("tags")] string? Tags = null,
    [property: JsonProperty("parent_preset_id")] Ulid? ParentPresetId = null
);

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

public record ImportProfileRequest(
    [property: JsonProperty("profile_json")] string? ProfileJson,
    [property: JsonProperty("url")] string? Url
);

/// <summary>
/// Adapter that lets <see cref="IPresetResolver"/> walk the parent chain by
/// hitting the database once per ancestor. Synchronous lookup — the resolver
/// is pure and doesn't await, so the adapter blocks on async repository
/// calls.
/// </summary>
internal sealed class EncoderProfilesPresetLookup(EncodingPresetRepository repository)
    : IPresetLookup
{
    public PresetResolveRequest? FindByName(string name)
    {
        EncodingPreset? preset = repository.GetByNameAsync(name).GetAwaiter().GetResult();
        if (preset is null)
            return null;

        string? parentName = preset.ParentPresetId is Ulid parentId
            ? repository.GetByIdAsync(parentId).GetAwaiter().GetResult()?.Name
            : null;

        return new(preset.Name, preset.ProfileJson, parentName);
    }
}
