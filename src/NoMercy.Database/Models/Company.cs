using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
public class Company : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [MaxLength(4096)] [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("headquarters")] public string? Headquarters { get; set; }
    [JsonProperty("homepage")] public Uri? Homepage { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("origin_country")] public string? OriginCountry { get; set; }
    [JsonProperty("parent_company")] public int? ParentCompany { get; set; }

    public virtual ICollection<CompanyMovie> CompanyMovie { get; set; } = new HashSet<CompanyMovie>();
    public virtual ICollection<CompanyTv> CompanyTv { get; set; } = new HashSet<CompanyTv>();
}