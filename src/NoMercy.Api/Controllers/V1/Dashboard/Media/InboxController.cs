using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.Inbox;
using NoMercy.Helpers.Extensions;
using NoMercy.MediaProcessing.Inbox;

namespace NoMercy.Api.Controllers.V1.Dashboard.Media;

[ApiController]
[Tags("Dashboard Inbox")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/inbox", Order = 10)]
public class InboxController(
    IDbContextFactory<MediaContext> mediaContextFactory,
    InboxRoutingService routingService,
    IInboxMetadataProbe metadataProbe
) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? status)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view the inbox");

        await using MediaContext context = await mediaContextFactory.CreateDbContextAsync();

        IQueryable<InboxItem> query = context.InboxItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(item => item.Status == status);

        List<InboxItem> items = await query.OrderByDescending(item => item.CreatedAt).ToListAsync();

        return Ok(new { Data = items.Select(item => new InboxItemDto(item)) });
    }

    [HttpGet("{id:ulid}")]
    public async Task<IActionResult> Show(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view inbox items");

        await using MediaContext context = await mediaContextFactory.CreateDbContextAsync();

        InboxItem? item = await context
            .InboxItems.AsNoTracking()
            .FirstOrDefaultAsync(inboxItem => inboxItem.Id == id);

        if (item is null)
            return NotFoundResponse("Inbox item not found");

        return Ok(new InboxItemDto(item));
    }

    [HttpGet("{id:ulid}/matches")]
    public async Task<IActionResult> Matches(
        Ulid id,
        [FromQuery] string type,
        [FromQuery] string query
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to search for matches");

        if (string.IsNullOrWhiteSpace(type))
            return BadRequestResponse("type is required");

        if (string.IsNullOrWhiteSpace(query))
            return BadRequestResponse("query is required");

        CandidateMatch[] candidates;

        switch (type)
        {
            case "movie":
                candidates = await metadataProbe.SearchMoviesAsync(
                    query,
                    null,
                    HttpContext.RequestAborted
                );
                break;

            case "tv":
            case "anime":
                candidates = await metadataProbe.SearchTvAsync(
                    query,
                    null,
                    HttpContext.RequestAborted
                );
                break;

            case "music":
                candidates = [];
                break;

            default:
                return BadRequestResponse(
                    $"Unsupported type '{type}'. Expected: movie, tv, anime, music"
                );
        }

        return Ok(new { Data = candidates });
    }

    [HttpPost("{id:ulid}/assign")]
    public async Task<IActionResult> Assign(Ulid id, [FromBody] InboxAssignRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to assign inbox items");

        await using MediaContext context = await mediaContextFactory.CreateDbContextAsync();

        InboxItem? item = await context.InboxItems.FirstOrDefaultAsync(inboxItem =>
            inboxItem.Id == id
        );

        if (item is null)
            return NotFoundResponse("Inbox item not found");

        Folder? folder = await context
            .Folders.AsNoTracking()
            .FirstOrDefaultAsync(folderEntity => folderEntity.Id == request.TargetFolderId);

        if (folder is null)
            return NotFoundResponse("Target folder not found");

        InboxDestination destination = new()
        {
            LibraryId = request.TargetLibraryId,
            FolderId = request.TargetFolderId,
            ProfileId = request.TargetProfileId,
            DriverId = folder.DriverId,
            FolderPath = folder.Path,
        };

        item.DetectedType = request.Type;
        item.TargetLibraryId = request.TargetLibraryId;
        item.TargetFolderId = request.TargetFolderId;
        item.TargetProfileId = request.TargetProfileId;

        try
        {
            await routingService.ExecuteAssignment(
                item,
                request.Match,
                destination,
                context,
                HttpContext.RequestAborted
            );
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse(
                $"Failed to assign inbox item: {ex.GetType().Name}: {ex.Message}"
            );
        }

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                new InboxItemUpdatedEvent { Id = item.Id.ToString(), Status = item.Status }
            );
        }

        return Ok(
            new StatusResponseDto<InboxItemDto>
            {
                Status = "ok",
                Message = "Successfully assigned inbox item.",
                Data = new InboxItemDto(item),
            }
        );
    }

    [HttpPost("{id:ulid}/dismiss")]
    public async Task<IActionResult> Dismiss(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to dismiss inbox items");

        await using MediaContext context = await mediaContextFactory.CreateDbContextAsync();

        InboxItem? item = await context.InboxItems.FirstOrDefaultAsync(inboxItem =>
            inboxItem.Id == id
        );

        if (item is null)
            return NotFoundResponse("Inbox item not found");

        item.Status = "Dismissed";

        await context.SaveChangesAsync();

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                new InboxItemUpdatedEvent { Id = item.Id.ToString(), Status = item.Status }
            );
        }

        return Ok(
            new StatusResponseDto<string> { Status = "ok", Message = "Inbox item dismissed." }
        );
    }

    [HttpDelete("{id:ulid}")]
    public async Task<IActionResult> Delete(Ulid id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete inbox items");

        await using MediaContext context = await mediaContextFactory.CreateDbContextAsync();

        InboxItem? item = await context.InboxItems.FirstOrDefaultAsync(inboxItem =>
            inboxItem.Id == id
        );

        if (item is null)
            return NotFoundResponse("Inbox item not found");

        context.InboxItems.Remove(item);
        await context.SaveChangesAsync();

        return Ok(new StatusResponseDto<string> { Status = "ok", Message = "Inbox item removed." });
    }
}
