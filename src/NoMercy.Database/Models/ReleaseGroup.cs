using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
public class ReleaseGroup : ColorPaletteTimeStamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("year")] public int Year { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }

    [JsonProperty("library_id")] public Ulid? LibraryId { get; set; }
    public Library? Library { get; set; }

    [JsonProperty("albums")] public ICollection<AlbumReleaseGroup> AlbumReleaseGroup { get; set; } = [];
    [JsonProperty("artists")] public ICollection<ArtistReleaseGroup> ArtistReleaseGroup { get; set; } = [];
    [JsonProperty("translations")] public ICollection<Translation> Translations { get; set; } = [];
}
