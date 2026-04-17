using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Common;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Media;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Encoding History")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/encoding/history", Order = 10)]
public class EncodingHistoryController(EncodingHistoryRepository historyRepository) : BaseController
{
    /// <summary>
    /// Paginated encoding history. Newest encodes first.
    /// </summary>
    /// <param name="pageSize">Rows per page (1–500, default 50).</param>
    /// <param name="pageIndex">Zero-based page index (default 0).</param>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] int pageSize = 50,
        [FromQuery] int pageIndex = 0
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view encoding history");

        pageSize = Math.Clamp(pageSize, 1, 500);
        if (pageIndex < 0)
            pageIndex = 0;

        List<EncodingHistory> entries = await historyRepository.GetRecentAsync(pageSize, pageIndex);
        int total = await historyRepository.GetTotalCountAsync();

        return Ok(
            new
            {
                data = entries,
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
    /// Delete a single history row. Users clean up individual rows from
    /// the dashboard; the encoded output on disk is untouched.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete encoding history");

        if (!Ulid.TryParse(id, out Ulid entryId))
            return BadRequestResponse("Invalid history id");

        bool removed = await historyRepository.DeleteAsync(entryId);
        return removed ? NoContent() : NotFoundResponse("History entry not found");
    }

    /// <summary>
    /// Bulk purge. <c>older_than_days</c> drops every row older than N days;
    /// omit it to clear the entire history. Owner-only because clearing the
    /// full history is a coarse change.
    /// </summary>
    [HttpPost("purge")]
    public async Task<IActionResult> Purge([FromBody] PurgeHistoryRequest request)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("Only the server owner can bulk-purge encoding history");

        int removed = request.OlderThanDays.HasValue
            ? await historyRepository.DeleteOlderThanAsync(
                DateTime.UtcNow.AddDays(-Math.Max(0, request.OlderThanDays.Value))
            )
            : await historyRepository.DeleteAllAsync();

        return Ok(new { removed });
    }
}

public record PurgeHistoryRequest(int? OlderThanDays = null);
