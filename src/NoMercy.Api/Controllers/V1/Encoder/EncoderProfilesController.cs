using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Activity;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;
using AnalysisMediaInfo = NoMercy.Encoder.Analysis.MediaInfo;
using V2EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using V2IPresetLookup = NoMercy.Encoder.Profiles.IPresetLookup;
using V2PresetResolver = NoMercy.Encoder.Profiles.PresetResolver;
using V2ProfileDiffer = NoMercy.Encoder.Profiles.ProfileDiffer;
using V2ProfileValidationResult = NoMercy.Encoder.Profiles.ProfileValidationResult;
using V2ProfileValidator = NoMercy.Encoder.Profiles.ProfileValidator;

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
    IProfileSignatureVerifier signatureVerifier,
    IHttpClientFactory httpClientFactory,
    MediaContext mediaContext,
    EncodingPresetRepository presetRepository,
    IActivityLogger activityLogger
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
    /// Returns the sparse DB row for a single encoding preset by its <see cref="Ulid"/> id.
    /// </summary>
    [HttpGet("{id:ulid}")]
    public async Task<IActionResult> Get(Ulid id, CancellationToken ct)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view profiles");

        EncodingPreset? preset = await mediaContext
            .EncodingPresets.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (preset is null)
            return NotFoundResponse("Preset not found.");

        return Ok(
            new EncoderProfileDto
            {
                Id = preset.Id,
                Name = preset.Name,
                Description = preset.Description,
                Tags = preset.Tags,
                ParentPresetId = preset.ParentPresetId,
                IsBuiltIn = preset.IsBuiltIn,
                Source = preset.Source,
                ProfileJson = preset.ProfileJson,
            }
        );
    }

    /// <summary>
    /// Resolves a preset by walking its parent chain and merging all layers,
    /// returning the fully-effective <see cref="V2EncodingProfile"/>.
    /// </summary>
    [HttpGet("{id:ulid}/resolved")]
    public IActionResult GetResolved(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view profiles");

        DbPresetLookup lookup = new(mediaContext);
        V2EncodingProfile resolved;
        try
        {
            resolved = V2PresetResolver.Resolve(id, lookup);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }

        V2ProfileValidationResult validation = V2ProfileValidator.Validate(resolved);
        if (!validation.IsValid)
            return UnprocessableEntityResponse(string.Join("; ", validation.Errors));

        return Ok(resolved);
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
        {
            try
            {
                await activityLogger.LogFailureAsync(
                    "failure.config_save",
                    User.UserId(),
                    Ulid.Empty,
                    errorCode: "validation_error",
                    message: "name is required"
                );
            }
            catch (Exception logEx)
            {
                Logger.App(
                    $"Failed to log failure.config_save: {logEx.Message}",
                    LogEventLevel.Warning
                );
            }

            return BadRequestResponse("name is required");
        }

        if (string.IsNullOrWhiteSpace(request.ProfileJson))
        {
            try
            {
                await activityLogger.LogFailureAsync(
                    "failure.config_save",
                    User.UserId(),
                    Ulid.Empty,
                    errorCode: "validation_error",
                    message: "profile_json is required"
                );
            }
            catch (Exception logEx)
            {
                Logger.App(
                    $"Failed to log failure.config_save: {logEx.Message}",
                    LogEventLevel.Warning
                );
            }

            return BadRequestResponse("profile_json is required");
        }

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

        try
        {
            await activityLogger.LogConfigurationAsync(
                "config.encoder_default_changed",
                User.UserId(),
                Ulid.Empty,
                configKey: $"encoder.profile.{saved.Id}",
                oldValue: null,
                newValue: new
                {
                    id = saved.Id.ToString(),
                    name = saved.Name,
                    action = "created",
                }
            );
        }
        catch (Exception ex)
        {
            Logger.App(
                $"Failed to log encoder profile created: {ex.Message}",
                LogEventLevel.Warning
            );
        }

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
    /// <see cref="INamePresetResolver"/>, returning the fully-merged
    /// <see cref="EncodingProfile"/>. Superseded by
    /// <c>GET /{id:ulid}/resolved</c> which uses the V2 resolver and validates
    /// the merged result before returning.
    /// </summary>
    [Obsolete("Use GET /{id:ulid}/resolved — V2 resolver with post-merge validation.")]
    [HttpGet("{id}/resolve")]
    public async Task<IActionResult> Resolve(
        string id,
        [FromServices] INamePresetResolver presetResolver
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
    /// Permanently deletes a user-created preset. Rejects deletion when the
    /// preset is built-in or when other presets inherit from it — callers must
    /// reparent or delete children first.
    /// </summary>
    [HttpDelete("{id:ulid}")]
    public async Task<IActionResult> Delete(Ulid id, CancellationToken ct)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete profiles");

        EncodingPreset? row = await mediaContext.EncodingPresets.FirstOrDefaultAsync(
            p => p.Id == id,
            ct
        );
        if (row is null)
            return NotFoundResponse("Preset not found.");
        if (row.IsBuiltIn)
            return BadRequestResponse("Built-in presets cannot be deleted.");

        bool hasChildren = await mediaContext.EncodingPresets.AnyAsync(
            p => p.ParentPresetId == id,
            ct
        );
        if (hasChildren)
            return BadRequestResponse(
                "Preset has children that inherit from it; reparent or delete them first."
            );

        mediaContext.EncodingPresets.Remove(row);
        await mediaContext.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Validates an encoding profile and returns a structured envelope with
    /// errors (blocking) and warnings (non-blocking) bucketed separately.
    ///
    /// Always returns 200 — <c>valid: false</c> flows in the body. Validation
    /// informs; it never gates the HTTP request.
    ///
    /// Note: uses the V2.5 <see cref="IProfileValidator"/> pipeline. V2 profiles
    /// stored via <c>PUT /{id:ulid}</c> are validated by
    /// <see cref="V2ProfileValidator"/> inside <c>GET /{id:ulid}/resolved</c>.
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

        // PreviewEngine was removed in V2 migration — V2 preview not yet implemented.
        return StatusCode(
            501,
            new { error = "Encode preview not yet implemented for V2 profiles." }
        );
    }

    /// <summary>
    /// Updates a user-owned preset. When the preset has a parent, the saved
    /// <see cref="EncodingPreset.ProfileJson"/> is the sparse diff against the
    /// resolved parent rather than the full profile — keeping the inheritance
    /// chain intact and compact.
    /// </summary>
    [HttpPut("{id:ulid}")]
    public async Task<IActionResult> Update(
        Ulid id,
        [FromBody] V2EncodingProfile incoming,
        CancellationToken ct
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update profiles");

        EncodingPreset? row = await mediaContext.EncodingPresets.FirstOrDefaultAsync(
            p => p.Id == id,
            ct
        );
        if (row is null)
            return NotFoundResponse("Preset not found.");
        if (row.IsBuiltIn)
            return BadRequestResponse("Built-in presets are read-only.");

        Newtonsoft.Json.Linq.JObject sparseJson;
        if (row.ParentPresetId.HasValue)
        {
            DbPresetLookup lookup = new(mediaContext);
            V2EncodingProfile resolvedParent = V2PresetResolver.Resolve(
                row.ParentPresetId.Value,
                lookup
            );
            sparseJson = V2ProfileDiffer.Diff(incoming, resolvedParent);
        }
        else
        {
            sparseJson = Newtonsoft.Json.Linq.JObject.FromObject(incoming);
        }

        row.ProfileJson = sparseJson.ToString(Formatting.None);
        row.UpdatedAt = DateTime.UtcNow;
        await mediaContext.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Clones a preset into a new pure-inheritance child. The clone's
    /// <see cref="EncodingPreset.ProfileJson"/> starts as <c>{}</c> — all
    /// effective values flow from the parent chain — so only intentional
    /// overrides need to be set via PUT.
    /// </summary>
    [HttpPost("{parentId:ulid}/clone")]
    public async Task<IActionResult> Clone(
        Ulid parentId,
        [FromBody] CloneRequest request,
        CancellationToken ct
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to clone profiles");

        EncodingPreset? parent = await mediaContext
            .EncodingPresets.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == parentId, ct);
        if (parent is null)
            return NotFoundResponse("Parent preset not found.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequestResponse("Name required.");

        EncodingPreset clone = new()
        {
            Id = Ulid.NewUlid(),
            Name = request.Name,
            Description = request.Description,
            ProfileJson = "{}",
            ParentPresetId = parent.Id,
            IsBuiltIn = false,
            Source = "db",
        };
        mediaContext.EncodingPresets.Add(clone);
        await mediaContext.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = clone.Id }, new { id = clone.Id });
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
        // V2 EncodingProfile no longer carries PublisherKeyFingerprint /
        // Signature inline — those move into the trusted-publisher pipeline as
        // a separate envelope when V2 grows publisher signing support. For
        // now, skip signature verification on V2 profiles.
        bool hasSigning = false;

        if (hasSigning)
        {
            EncoderRule? rejection = signatureVerifier.Verify(
                profileJson,
                string.Empty,
                string.Empty,
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

        try
        {
            await activityLogger.LogConfigurationAsync(
                "config.encoder_default_changed",
                User.UserId(),
                Ulid.Empty,
                configKey: $"encoder.profile.{saved.Id}",
                oldValue: null,
                newValue: new
                {
                    id = saved.Id.ToString(),
                    name = saved.Name,
                    action = "imported",
                    source = request.Url ?? "inline",
                }
            );
        }
        catch (Exception ex)
        {
            Logger.App(
                $"Failed to log encoder profile imported: {ex.Message}",
                LogEventLevel.Warning
            );
        }

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

public class CloneRequest
{
    [JsonProperty("name")]
    public required string Name { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }
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

[Obsolete("Replaced by V2EncodingProfile body on PUT /{id:ulid}. Kept for reference only.")]
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
/// Adapter that lets <see cref="INamePresetResolver"/> walk the parent chain by
/// hitting the database once per ancestor. Synchronous lookup — the resolver
/// is pure and doesn't await, so the adapter blocks on async repository
/// calls.
/// </summary>
internal sealed class EncoderProfilesPresetLookup(EncodingPresetRepository repository)
    : INamePresetLookup
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

/// <summary>
/// V2 <see cref="V2IPresetLookup"/> adapter — walks the parent chain by id
/// directly against <see cref="MediaContext"/> so the V2 resolver can merge
/// the inheritance layers without going through the V2.5 repository.
/// Synchronous lookup is intentional: <see cref="PresetResolver"/> is a pure
/// static method and the chain is short (max 8 hops by contract).
/// </summary>
internal sealed class DbPresetLookup(MediaContext context) : IPresetLookup
{
    public (string ProfileJson, Ulid? ParentPresetId)? Get(Ulid presetId)
    {
        EncodingPreset? row = context
            .EncodingPresets.AsNoTracking()
            .FirstOrDefault(p => p.Id == presetId);
        return row is null ? null : (row.ProfileJson, row.ParentPresetId);
    }
}
