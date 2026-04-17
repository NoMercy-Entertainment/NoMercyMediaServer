using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Profiles;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Encoding Presets")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/encoding/presets")]
public class EncodingPresetsController(
    EncodingPresetRepository presetRepository,
    IPresetResolver presetResolver,
    IHttpClientFactory httpClientFactory
) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int pageSize = 100,
        [FromQuery] int pageIndex = 0,
        [FromQuery] string? tag = null
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view presets");

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

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view presets");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid preset id");

        EncodingPreset? preset = await presetRepository.GetByIdAsync(presetId);
        if (preset is null)
            return NotFoundResponse("Preset not found");

        return Ok(preset);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePresetRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create presets");

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequestResponse("name is required");
        if (string.IsNullOrWhiteSpace(request.ProfileJson))
            return BadRequestResponse("profile_json is required");

        EncodingPreset? existing = await presetRepository.GetByNameAsync(request.Name);
        if (existing is not null)
            return ConflictResponse($"A preset named '{request.Name}' already exists");

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

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdatePresetRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update presets");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid preset id");

        try
        {
            EncodingPreset? updated = await presetRepository.UpdateAsync(
                presetId,
                preset =>
                {
                    if (request.Name is not null)
                        preset.Name = request.Name;
                    if (request.Description is not null)
                        preset.Description = request.Description;
                    if (request.Author is not null)
                        preset.Author = request.Author;
                    if (request.Tags is not null)
                        preset.Tags = request.Tags;
                    if (request.ProfileJson is not null)
                        preset.ProfileJson = request.ProfileJson;
                    if (request.ParentPresetId.HasValue)
                        preset.ParentPresetId = request.ParentPresetId.Value;
                }
            );

            if (updated is null)
                return NotFoundResponse("Preset not found");

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return ConflictResponse(ex.Message);
        }
    }

    [HttpGet("tags")]
    public async Task<IActionResult> ListAllTags()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view presets");

        IReadOnlyList<string> tags = await presetRepository.GetAllTagsAsync();
        return Ok(new { data = tags });
    }

    [HttpPost("{id}/clone")]
    public async Task<IActionResult> Clone(string id, [FromBody] ClonePresetRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to clone presets");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid preset id");

        EncodingPreset? source = await presetRepository.GetByIdAsync(presetId);
        if (source is null)
            return NotFoundResponse("Preset not found");

        string name = string.IsNullOrWhiteSpace(request.Name)
            ? await FindUnusedCloneNameAsync(source.Name)
            : request.Name;

        if (await presetRepository.GetByNameAsync(name) is not null)
            return ConflictResponse($"A preset named '{name}' already exists");

        EncodingPreset clone = new()
        {
            Name = name,
            Description = source.Description,
            Author = request.Author ?? source.Author,
            Tags = source.Tags,
            ProfileJson = source.ProfileJson,
            ParentPresetId = source.ParentPresetId,
            // Cloning a built-in produces an editable user preset. That's
            // the whole point — lets users tweak base presets without
            // touching the seeded rows.
            IsBuiltIn = false,
        };

        EncodingPreset saved = await presetRepository.CreateAsync(clone);
        return Ok(saved);
    }

    private async Task<string> FindUnusedCloneNameAsync(string sourceName)
    {
        string candidate = $"{sourceName} (copy)";
        int suffix = 1;
        while (await presetRepository.GetByNameAsync(candidate) is not null)
        {
            suffix++;
            candidate = $"{sourceName} (copy {suffix})";
        }
        return candidate;
    }

    [HttpGet("{id}/resolve")]
    public async Task<IActionResult> Resolve(string id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view presets");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid preset id");

        EncodingPreset? leaf = await presetRepository.GetByIdAsync(presetId);
        if (leaf is null)
            return NotFoundResponse("Preset not found");

        string? parentName = await ResolveParentNameAsync(leaf.ParentPresetId);
        PresetResolveRequest request = new(leaf.Name, leaf.ProfileJson, parentName);

        try
        {
            EncodingProfile resolved = presetResolver.Resolve(
                request,
                new RepositoryPresetLookup(presetRepository)
            );
            return Ok(resolved);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse($"Preset could not be resolved: {ex.Message}");
        }
    }

    [HttpGet("{id}/export")]
    public async Task<IActionResult> Export(string id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to export presets");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid preset id");

        EncodingPreset? preset = await presetRepository.GetByIdAsync(presetId);
        if (preset is null)
            return NotFoundResponse("Preset not found");

        PresetExport export = new(
            Name: preset.Name,
            Description: preset.Description,
            Author: preset.Author,
            Tags: preset.Tags,
            ProfileJson: preset.ProfileJson
        );

        return Ok(export);
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] PresetExport import)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to import presets");

        if (string.IsNullOrWhiteSpace(import.Name))
            return BadRequestResponse("name is required");
        if (string.IsNullOrWhiteSpace(import.ProfileJson))
            return BadRequestResponse("profile_json is required");

        // Collision rename: append "(imported N)" until we find an unused name.
        // Users can rename afterwards — we'd rather keep both copies than
        // silently overwrite an existing preset.
        string finalName = import.Name;
        int suffix = 1;
        while (await presetRepository.GetByNameAsync(finalName) is not null)
        {
            finalName = $"{import.Name} (imported {suffix++})";
        }

        EncodingPreset preset = new()
        {
            Name = finalName,
            Description = import.Description,
            Author = import.Author,
            Tags = import.Tags,
            ProfileJson = import.ProfileJson,
            IsBuiltIn = false,
        };

        EncodingPreset saved = await presetRepository.CreateAsync(preset);
        return Ok(saved);
    }

    [HttpPost("import-url")]
    public async Task<IActionResult> ImportFromUrl(
        [FromBody] ImportFromUrlRequest request,
        CancellationToken ct
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to import presets");

        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequestResponse("url is required");

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out Uri? parsed))
            return BadRequestResponse("url is not a valid absolute URL");

        // Only allow HTTPS for community presets — HTTP means a MITM attacker
        // can inject arbitrary encoding profiles onto the server. HTTPS shifts
        // trust to certificate validation, which is what we want.
        if (parsed.Scheme != Uri.UriSchemeHttps)
            return BadRequestResponse("Only https:// URLs are supported for preset imports");

        PresetExport? export;
        try
        {
            HttpClient client = httpClientFactory.CreateClient("preset-import");
            client.Timeout = TimeSpan.FromSeconds(15);
            string body = await client.GetStringAsync(parsed, ct);
            export = JsonConvert.DeserializeObject<PresetExport>(body);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BadRequestResponse($"Could not fetch or parse preset from URL: {ex.Message}");
        }

        if (export is null || string.IsNullOrWhiteSpace(export.Name))
            return BadRequestResponse("URL response did not contain a valid preset payload");

        return await Import(export);
    }

    private async Task<string?> ResolveParentNameAsync(Ulid? parentId)
    {
        if (parentId is null)
            return null;

        EncodingPreset? parent = await presetRepository.GetByIdAsync(parentId.Value);
        return parent?.Name;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete presets");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid preset id");

        try
        {
            bool removed = await presetRepository.DeleteAsync(presetId);
            if (!removed)
                return NotFoundResponse("Preset not found");

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ConflictResponse(ex.Message);
        }
    }
}

public record CreatePresetRequest(
    string Name,
    string ProfileJson,
    string? Description = null,
    string? Author = null,
    string? Tags = null,
    Ulid? ParentPresetId = null
);

/// <summary>
/// Adapter that lets <see cref="IPresetResolver"/> walk the parent chain by
/// hitting the database once per ancestor. Synchronous lookup — the resolver
/// is pure and doesn't await, so the adapter blocks on async repository
/// calls. Fine for the rare resolve path; optimize if it ever gets called
/// in a tight loop.
/// </summary>
internal sealed class RepositoryPresetLookup(EncodingPresetRepository repository) : IPresetLookup
{
    public PresetResolveRequest? FindByName(string name)
    {
        EncodingPreset? preset = repository.GetByNameAsync(name).GetAwaiter().GetResult();
        if (preset is null)
            return null;

        string? parentName = preset.ParentPresetId is Ulid parentId
            ? repository.GetByIdAsync(parentId).GetAwaiter().GetResult()?.Name
            : null;

        return new PresetResolveRequest(preset.Name, preset.ProfileJson, parentName);
    }
}

public record PresetExport(
    string Name,
    string ProfileJson,
    string? Description = null,
    string? Author = null,
    string? Tags = null
);

public record ImportFromUrlRequest(string Url);

public record ClonePresetRequest(string? Name = null, string? Author = null);

public record UpdatePresetRequest(
    string? Name = null,
    string? Description = null,
    string? Author = null,
    string? Tags = null,
    string? ProfileJson = null,
    Ulid? ParentPresetId = null
);
