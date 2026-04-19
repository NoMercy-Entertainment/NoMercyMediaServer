using Microsoft.AspNetCore.Mvc;

namespace NoMercy.Api.DTOs.Media;

public class PageRequestDto
{
    [FromQuery(Name = "page")]
    public int Page { get; set; }

    [FromQuery(Name = "take")]
    public int Take { get; set; } = 300;

    [FromQuery(Name = "version")]
    public string? Version { get; set; }
}

public static class PageRequestDtoExtensions
{
    /// <summary>
    /// Returns true when the client has requested lolomo layout (Android TV row-per-row mode).
    /// Case-insensitive; null Version returns false.
    /// </summary>
    public static bool IsLolomo(this PageRequestDto request) =>
        string.Equals(request.Version, "lolomo", StringComparison.OrdinalIgnoreCase);
}
