
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Movies;

[PrimaryKey(nameof(Id))]
public class WatchProvider : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("display_priority")] public int DisplayPriority { get; set; }

    [JsonProperty("medias")] public ICollection<WatchProviderMedia> WatchProviderMedias { get; set; } = new List<WatchProviderMedia>();
}