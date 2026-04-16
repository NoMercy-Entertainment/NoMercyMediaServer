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
}
