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
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.Services;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;
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
[Tags(tags: "Encoder Profiles")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "Moderator")]
[Route(template: "api/v{version:apiVersion}/encoder/profiles")]
public class EncoderProfilesController(
    IProfileValidator profileValidator,
    IProfileSignatureVerifier signatureVerifier,
    MediaContext mediaContext,
    IEncodingPresetRepository presetRepository,
    EncoderProfileService encoderProfileService
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
        pageSize = Math.Clamp(value: pageSize, min: 1, max: 500);
        if (pageIndex < 0)
            pageIndex = 0;

        List<EncodingPreset> presets = await presetRepository.ListAsync(pageSize: pageSize, pageIndex: pageIndex, tagFilter: tag);
        int total = await presetRepository.GetTotalCountAsync();

        return Ok(
            value: new
            {
                data = presets,
                meta = new
                {
                    total,
                    pageSize,
                    pageIndex,
                    totalPages = (int)Math.Ceiling(a: (double)total / pageSize),
                },
            }
        );
    }

    /// <summary>
    /// Returns the sparse DB row for a single encoding preset by its <see cref="Ulid"/> id.
    /// </summary>
    [HttpGet(template: "{id:ulid}")]
    public async Task<IActionResult> Get(Ulid id, CancellationToken ct)
    {
        EncodingPreset? preset = await mediaContext
            .EncodingPresets.AsNoTracking()
            .FirstOrDefaultAsync(predicate: p => p.Id == id, cancellationToken: ct);
        if (preset is null)
            return NotFoundResponse(detail: "Preset not found.");

        return Ok(
            value: new EncoderProfileDto
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
    [HttpGet(template: "{id:ulid}/resolved")]
    public IActionResult GetResolved(Ulid id)
    {
        DbPresetLookup lookup = new(context: mediaContext);
        V2EncodingProfile resolved;
        try
        {
            resolved = V2PresetResolver.Resolve(presetId: id, lookup: lookup);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(detail: ex.Message);
        }

        V2ProfileValidationResult validation = V2ProfileValidator.Validate(profile: resolved);
        if (!validation.IsValid)
            return UnprocessableEntityResponse(detail: string.Join(separator: "; ", values: validation.Errors));

        return Ok(value: resolved);
    }

    /// <summary>
    /// Creates a new user-owned encoding profile. <c>IsBuiltIn</c> is always
    /// stamped <c>false</c> regardless of what the caller sends.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEncoderProfileRequest request)
    {
        EncoderProfileService.CreateResult result = await encoderProfileService.CreateAsync(
            name: request.Name,
            profileJson: request.ProfileJson,
            description: request.Description,
            author: request.Author,
            tags: request.Tags,
            parentPresetId: request.ParentPresetId,
            userId: User.UserId()
        );

        if (result.IsValidation)
            return BadRequestResponse(detail: result.ErrorMessage!);

        if (result.IsConflict)
            return ConflictResponse(detail: result.ErrorMessage!);

        return Ok(value: result.Saved);
    }

    /// <summary>
    /// Returns the distinct set of tags in use across all profiles, sorted
    /// alphabetically. Tags are stored as comma-separated values on each row.
    /// </summary>
    [HttpGet(template: "tags")]
    public async Task<IActionResult> Tags()
    {
        IReadOnlyList<string> tags = await presetRepository.GetAllTagsAsync();
        return Ok(value: new { data = tags });
    }

    /// <summary>
    /// Resolves a profile by walking its parent chain via
    /// <see cref="INamePresetResolver"/>, returning the fully-merged
    /// <see cref="EncodingProfile"/>. Superseded by
    /// <c>GET /{id:ulid}/resolved</c> which uses the V2 resolver and validates
    /// the merged result before returning.
    /// </summary>
    [Obsolete(message: "Use GET /{id:ulid}/resolved — V2 resolver with post-merge validation.")]
    [HttpGet(template: "{id}/resolve")]
    public async Task<IActionResult> Resolve(
        string id,
        [FromServices] INamePresetResolver presetResolver
    )
    {
        if (!Ulid.TryParse(base32: id, ulid: out Ulid presetId))
            return BadRequestResponse(detail: "Invalid profile id");

        EncodingPreset? leaf = await presetRepository.GetByIdAsync(id: presetId);
        if (leaf is null)
            return NotFoundResponse(detail: "Profile not found");

        string? parentName = null;
        if (leaf.ParentPresetId is Ulid parentId)
        {
            EncodingPreset? parent = await presetRepository.GetByIdAsync(id: parentId);
            parentName = parent?.Name;
        }

        PresetResolveRequest resolveRequest = new(Name: leaf.Name, ProfileJson: leaf.ProfileJson, ParentName: parentName);

        try
        {
            EncodingProfile resolved = presetResolver.Resolve(
                request: resolveRequest,
                lookup: new EncoderProfilesPresetLookup(repository: presetRepository)
            );
            return Ok(value: resolved);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(detail: $"Profile could not be resolved: {ex.Message}");
        }
    }

    /// <summary>
    /// Permanently deletes a user-created preset. Rejects deletion when the
    /// preset is built-in or when other presets inherit from it — callers must
    /// reparent or delete children first.
    /// </summary>
    [HttpDelete(template: "{id:ulid}")]
    public async Task<IActionResult> Delete(Ulid id, CancellationToken ct)
    {
        EncodingPreset? row = await mediaContext.EncodingPresets.FirstOrDefaultAsync(
            predicate: p => p.Id == id,
            cancellationToken: ct
        );
        if (row is null)
            return NotFoundResponse(detail: "Preset not found.");
        if (row.IsBuiltIn)
            return BadRequestResponse(detail: "Built-in presets cannot be deleted.");

        bool hasChildren = await mediaContext.EncodingPresets.AnyAsync(
            predicate: p => p.ParentPresetId == id,
            cancellationToken: ct
        );
        if (hasChildren)
            return BadRequestResponse(
                detail: "Preset has children that inherit from it; reparent or delete them first."
            );

        mediaContext.EncodingPresets.Remove(entity: row);
        await mediaContext.SaveChangesAsync(cancellationToken: ct);
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
    [HttpPost(template: "validate")]
    public IActionResult Validate([FromBody] ValidateEncoderProfileRequest request)
    {
        if (string.IsNullOrWhiteSpace(value: request.ProfileJson))
        {
            ValidationEnvelope empty = ValidationEnvelope.FromRules(rules:
            [
                new(
                    Id: EncoderRuleId.ProfileNameMissing,
                    Severity: EncoderRuleSeverity.Error,
                    Field: "profile_json",
                    Message: "profile_json is required",
                    Fix: "Supply the full profile JSON in the profile_json field."
                ),
            ]);
            return Ok(value: empty);
        }

        EncodingProfile? profile;
        try
        {
            profile = JsonConvert.DeserializeObject<EncodingProfile>(value: request.ProfileJson);
        }
        catch (JsonException ex)
        {
            ValidationEnvelope parseError = ValidationEnvelope.FromRules(rules:
            [
                new(
                    Id: EncoderRuleId.ProfileNameMissing,
                    Severity: EncoderRuleSeverity.Error,
                    Field: "profile_json",
                    Message: $"Profile JSON is malformed: {ex.Message}",
                    Fix: "Fix the JSON syntax error and resubmit."
                ),
            ]);
            return Ok(value: parseError);
        }

        if (profile is null)
        {
            ValidationEnvelope nullError = ValidationEnvelope.FromRules(rules:
            [
                new(
                    Id: EncoderRuleId.ProfileNameMissing,
                    Severity: EncoderRuleSeverity.Error,
                    Field: "profile_json",
                    Message: "Profile JSON deserialized to null — check the outer object is present",
                    Fix: "Ensure the JSON root is an object, not null or an array."
                ),
            ]);
            return Ok(value: nullError);
        }

        ValidationEnvelope envelope = profileValidator.ValidateAsEnvelope(profile: profile);
        return Ok(value: envelope);
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
    [HttpPost(template: "{id}/preview")]
    public async Task<IActionResult> Preview(
        string id,
        [FromBody] PreviewEncoderProfileRequest request,
        CancellationToken ct
    )
    {
        EncoderProfileService.PreviewParseResult parseResult =
            encoderProfileService.ParseProfileForPreview(
                id: id,
                profileJson: request.ProfileJson,
                sourcePath: request.SourcePath
            );

        if (parseResult.EarlyResponse is not null)
            return Ok(value: parseResult.EarlyResponse);

        // PreviewEngine was removed in V2 migration — V2 preview not yet implemented.
        return NotImplementedResponse(detail: "Encode preview not yet implemented for V2 profiles.");
    }

    /// <summary>
    /// Updates a user-owned preset. When the preset has a parent, the saved
    /// <see cref="EncodingPreset.ProfileJson"/> is the sparse diff against the
    /// resolved parent rather than the full profile — keeping the inheritance
    /// chain intact and compact.
    /// </summary>
    [HttpPut(template: "{id:ulid}")]
    public async Task<IActionResult> Update(
        Ulid id,
        [FromBody] V2EncodingProfile incoming,
        CancellationToken ct
    )
    {
        EncodingPreset? row = await mediaContext.EncodingPresets.FirstOrDefaultAsync(
            predicate: p => p.Id == id,
            cancellationToken: ct
        );
        if (row is null)
            return NotFoundResponse(detail: "Preset not found.");
        if (row.IsBuiltIn)
            return BadRequestResponse(detail: "Built-in presets are read-only.");

        Newtonsoft.Json.Linq.JObject sparseJson;
        if (row.ParentPresetId.HasValue)
        {
            DbPresetLookup lookup = new(context: mediaContext);
            V2EncodingProfile resolvedParent = V2PresetResolver.Resolve(
                presetId: row.ParentPresetId.Value,
                lookup: lookup
            );
            sparseJson = V2ProfileDiffer.Diff(child: incoming, resolvedParent: resolvedParent);
        }
        else
        {
            sparseJson = Newtonsoft.Json.Linq.JObject.FromObject(o: incoming);
        }

        row.ProfileJson = sparseJson.ToString(formatting: Formatting.None);
        row.UpdatedAt = DateTime.UtcNow;
        await mediaContext.SaveChangesAsync(cancellationToken: ct);

        return NoContent();
    }

    /// <summary>
    /// Clones a preset into a new pure-inheritance child. The clone's
    /// <see cref="EncodingPreset.ProfileJson"/> starts as <c>{}</c> — all
    /// effective values flow from the parent chain — so only intentional
    /// overrides need to be set via PUT.
    /// </summary>
    [HttpPost(template: "{parentId:ulid}/clone")]
    public async Task<IActionResult> Clone(
        Ulid parentId,
        [FromBody] CloneRequest request,
        CancellationToken ct
    )
    {
        EncodingPreset? parent = await mediaContext
            .EncodingPresets.AsNoTracking()
            .FirstOrDefaultAsync(predicate: p => p.Id == parentId, cancellationToken: ct);
        if (parent is null)
            return NotFoundResponse(detail: "Parent preset not found.");
        if (string.IsNullOrWhiteSpace(value: request.Name))
            return BadRequestResponse(detail: "Name required.");

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
        mediaContext.EncodingPresets.Add(entity: clone);
        await mediaContext.SaveChangesAsync(cancellationToken: ct);

        return CreatedAtAction(actionName: nameof(Get), routeValues: new { id = clone.Id }, value: new { id = clone.Id });
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
    [HttpPost(template: "import")]
    public async Task<IActionResult> Import(
        [FromBody] ImportProfileRequest request,
        [FromQuery] bool trust_unsigned = false,
        CancellationToken ct = default
    )
    {
        EncoderProfileService.ImportResult result = await encoderProfileService.ImportAsync(
            inlineProfileJson: request.ProfileJson,
            url: request.Url,
            trustUnsigned: trust_unsigned,
            signatureVerifier: signatureVerifier,
            userId: User.UserId(),
            ct: ct
        );

        if (result.ValidationError is not null)
            return UnprocessableEntity(error: result.ValidationError);

        return CreatedAtAction(
            actionName: nameof(Import),
            routeValues: new { id = result.Saved!.Id.ToString() },
            value: new
            {
                id = result.Saved.Id,
                name = result.Saved.Name,
                profile = result.ImportedProfile,
            }
        );
    }

    /// <summary>
    /// Exports a profile as a JSON file download. The exported JSON includes all
    /// Phase 2.1 fields (<c>PublisherKeyFingerprint</c>, <c>Signature</c>) if
    /// present on the stored profile. Signing is the publisher's responsibility
    /// — this endpoint does not add a signature.
    /// </summary>
    [HttpGet(template: "{id}/export")]
    public async Task<IActionResult> Export(string id)
    {
        if (!Ulid.TryParse(base32: id, ulid: out Ulid presetId))
            return BadRequestResponse(detail: "Invalid profile id");

        EncodingPreset? preset = await presetRepository.GetByIdAsync(id: presetId);
        if (preset is null)
            return NotFoundResponse(detail: "Profile not found");

        string fileName = preset.Name.Replace(oldValue: " ", newValue: "_").Replace(oldValue: "/", newValue: "-") + ".json";

        Response.Headers[key: "Content-Disposition"] = $"attachment; filename=\"{fileName}\"";

        return Content(content: preset.ProfileJson, contentType: "application/json");
    }
}

public class CloneRequest
{
    [JsonProperty(propertyName: "name")]
    public required string Name { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }
}

public record CreateEncoderProfileRequest(
    [property: JsonProperty(propertyName: "name")] string Name,
    [property: JsonProperty(propertyName: "profile_json")] string ProfileJson,
    [property: JsonProperty(propertyName: "description")] string? Description = null,
    [property: JsonProperty(propertyName: "author")] string? Author = null,
    [property: JsonProperty(propertyName: "tags")] string? Tags = null,
    [property: JsonProperty(propertyName: "parent_preset_id")] Ulid? ParentPresetId = null
);

public record ValidateEncoderProfileRequest(
    [property: JsonProperty(propertyName: "profile_json")] string ProfileJson
);

public record PreviewEncoderProfileRequest(
    [property: JsonProperty(propertyName: "profile_json")] string ProfileJson,
    [property: JsonProperty(propertyName: "source_path")] string? SourcePath
);

[Obsolete(message: "Replaced by V2EncodingProfile body on PUT /{id:ulid}. Kept for reference only.")]
public record UpdateEncoderProfileRequest(
    [property: JsonProperty(propertyName: "name")] string? Name = null,
    [property: JsonProperty(propertyName: "description")] string? Description = null,
    [property: JsonProperty(propertyName: "profile_json")] string? ProfileJson = null
);

public record ImportProfileRequest(
    [property: JsonProperty(propertyName: "profile_json")] string? ProfileJson,
    [property: JsonProperty(propertyName: "url")] string? Url
);

/// <summary>
/// Adapter that lets <see cref="INamePresetResolver"/> walk the parent chain by
/// hitting the database once per ancestor. Synchronous lookup — the resolver
/// is pure and doesn't await, so the adapter blocks on async repository
/// calls.
/// </summary>
internal sealed class EncoderProfilesPresetLookup(IEncodingPresetRepository repository)
    : INamePresetLookup
{
    public PresetResolveRequest? FindByName(string name)
    {
        EncodingPreset? preset = repository.GetByNameAsync(name: name).GetAwaiter().GetResult();
        if (preset is null)
            return null;

        string? parentName = preset.ParentPresetId is Ulid parentId
            ? repository.GetByIdAsync(id: parentId).GetAwaiter().GetResult()?.Name
            : null;

        return new(Name: preset.Name, ProfileJson: preset.ProfileJson, ParentName: parentName);
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
            .FirstOrDefault(predicate: p => p.Id == presetId);
        return row is null ? null : (row.ProfileJson, row.ParentPresetId);
    }
}
