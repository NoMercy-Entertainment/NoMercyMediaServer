
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
public class Special : ColorPaletteTimeStamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("creator")] public string? Creator { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }

    [JsonProperty("items")] public ICollection<SpecialItem> Items { get; set; } = [];
    [JsonProperty("special_user")] public ICollection<SpecialUser> SpecialUser { get; set; } = [];
}
