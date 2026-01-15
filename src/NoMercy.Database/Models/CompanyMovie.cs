using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(CompanyId), nameof(MovieId))]
[Index(nameof(CompanyId), nameof(MovieId), IsUnique = true)]
public class CompanyMovie : Timestamps
{
    [JsonProperty("company_id")] public int CompanyId { get; set; }
    [JsonProperty("company")] public Company Company { get; set; } = null!;
    [JsonProperty("movieid")] public int MovieId { get; set; }
    [JsonProperty("movie")] public Movie Movie { get; set; } = null!;
}