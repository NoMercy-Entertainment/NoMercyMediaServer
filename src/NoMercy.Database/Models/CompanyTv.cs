using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(CompanyId), nameof(TvId))]
[Index(nameof(CompanyId), nameof(TvId), IsUnique = true)]
public class CompanyTv : Timestamps
{
    [JsonProperty("company_id")] public int CompanyId { get; set; }
    [JsonProperty("company")] public Company Company { get; set; } = null!;
    [JsonProperty("tvid")] public int TvId { get; set; }
    [JsonProperty("tv")] public Tv Tv { get; set; } = null!;
}