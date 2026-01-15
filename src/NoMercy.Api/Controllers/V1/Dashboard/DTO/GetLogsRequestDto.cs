using Microsoft.AspNetCore.Mvc;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public record GetLogsRequestDto
{
    [FromQuery(Name = "limit")] public int Limit { get; init; } = 50;
    [FromQuery(Name = "types[]")] public string[]? Types { get; init; }
    [FromQuery(Name = "levels[]")] public string[]? Levels { get; init; }
    [FromQuery(Name = "filter")] public string? Filter { get; init; }
}