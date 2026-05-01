using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Database.Models.Storage;

[PrimaryKey(nameof(Id))]
[Index(nameof(Name), IsUnique = true)]
[Index(nameof(Type))]
public class Driver
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [Required]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("config")]
    public string? Config { get; set; }

    [JsonProperty("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonProperty("folders")]
    public ICollection<Folder> Folders { get; set; } = [];
}
