using Microsoft.AspNetCore.Mvc;

namespace NoMercy.Api.DTOs.Media;

public class PageRequestDto
{
    [FromQuery(Name = "page")] public int Page { get; set; }
    [FromQuery(Name = "take")] public int Take { get; set; } = 300;
    [FromQuery(Name = "version")] public string? Version { get; set; }
}