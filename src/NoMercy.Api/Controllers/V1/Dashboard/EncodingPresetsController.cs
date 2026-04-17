using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Media;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Encoding Presets")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/encoding/presets")]
public class EncodingPresetsController(EncodingPresetRepository presetRepository) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int pageSize = 100,
        [FromQuery] int pageIndex = 0
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view presets");

        pageSize = Math.Clamp(pageSize, 1, 500);
        if (pageIndex < 0)
            pageIndex = 0;

        List<EncodingPreset> presets = await presetRepository.ListAsync(pageSize, pageIndex);
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

public record UpdatePresetRequest(
    string? Name = null,
    string? Description = null,
    string? Author = null,
    string? Tags = null,
    string? ProfileJson = null,
    Ulid? ParentPresetId = null
);
