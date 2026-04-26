using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.Controllers.V1.Dashboard;

/// <summary>
/// Cross-library video-file search for the dashboard. Used by every page that
/// needs the user to identify a single file by ULID (content-analysis probes,
/// crop detection, OCR, transcription, on-demand encode tests). Without this
/// the user would be forced to paste a 26-character ULID by hand — there is no
/// dashboard list anywhere that displays those IDs natively.
/// </summary>
[ApiController]
[Tags("Dashboard Server Media Files")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/media/files", Order = 10)]
public class MediaFilesController(IDbContextFactory<MediaContext> contextFactory) : BaseController
{
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] int limit = 50,
        CancellationToken ct = default
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to search media files");

        // Hard ceiling on `limit` — clients shouldn't be able to pull the whole
        // catalogue through this picker endpoint.
        int safeLimit = Math.Clamp(limit, 1, 200);
        string normalized = (q ?? string.Empty).Trim();

        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        IQueryable<VideoFile> baseQuery = context
            .VideoFiles.AsNoTracking()
            .Include(f => f.Movie)
            .Include(f => f.Episode)
                .ThenInclude(e => e!.Tv);

        IQueryable<VideoFile> filtered = string.IsNullOrEmpty(normalized)
            ? baseQuery
            : baseQuery.Where(f =>
                EF.Functions.Like(f.Filename, $"%{normalized}%")
                || (f.Movie != null && EF.Functions.Like(f.Movie.Title, $"%{normalized}%"))
                || (f.Episode != null && EF.Functions.Like(f.Episode.Title!, $"%{normalized}%"))
                || (
                    f.Episode != null
                    && f.Episode.Tv != null
                    && EF.Functions.Like(f.Episode.Tv.Title, $"%{normalized}%")
                )
            );

        List<VideoFile> rows = await filtered
            .OrderByDescending(f => f.UpdatedAt)
            .Take(safeLimit)
            .ToListAsync(ct);

        VideoFileSearchDto[] results = rows.Select(BuildDto).ToArray();

        return Ok(new { data = results });
    }

    private static VideoFileSearchDto BuildDto(VideoFile file)
    {
        if (file.Movie is not null)
        {
            return new()
            {
                Id = file.Id.ToString(),
                Type = "movie",
                Label = $"{file.Movie.Title} ({file.Movie.ReleaseDate.Year})",
                ParentLabel = file.Movie.Title,
                Filename = file.Filename,
                Quality = file.Quality,
                Duration = file.Duration,
            };
        }

        if (file.Episode is not null)
        {
            string showTitle = file.Episode.Tv?.Title ?? "Unknown show";
            string code = $"S{file.Episode.SeasonNumber:00}E{file.Episode.EpisodeNumber:00}";
            string episodeTitle = file.Episode.Title ?? "Untitled";
            return new()
            {
                Id = file.Id.ToString(),
                Type = "episode",
                Label = $"{showTitle} — {code} — {episodeTitle}",
                ParentLabel = showTitle,
                Filename = file.Filename,
                Quality = file.Quality,
                Duration = file.Duration,
            };
        }

        return new()
        {
            Id = file.Id.ToString(),
            Type = "orphan",
            Label = file.Filename,
            ParentLabel = file.Folder ?? string.Empty,
            Filename = file.Filename,
            Quality = file.Quality,
            Duration = file.Duration,
        };
    }
}

public class VideoFileSearchDto
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("label")]
    public string Label { get; set; } = string.Empty;

    [JsonProperty("parent_label")]
    public string ParentLabel { get; set; } = string.Empty;

    [JsonProperty("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonProperty("quality")]
    public string Quality { get; set; } = string.Empty;

    [JsonProperty("duration")]
    public string? Duration { get; set; }
}
