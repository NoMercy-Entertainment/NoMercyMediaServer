using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[Index(nameof(Name))]
public class Network : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("origin_country")] public string? OriginCountry { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("headquarters")] public string? Headquarters { get; set; }
    [JsonProperty("homepage")] public Uri? Homepage { get; set; }
    
    [JsonProperty("network_tv")] public ICollection<NetworkTv> NetworkTv { get; set; } = new HashSet<NetworkTv>();
}