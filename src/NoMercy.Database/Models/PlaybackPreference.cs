using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(UserId),nameof(LibraryId), IsUnique = true)]
[Index(nameof(UserId),nameof(TvId), IsUnique = true)]
[Index(nameof(UserId),nameof(MovieId), IsUnique = true)]
public class PlaybackPreference: MetadataTrack
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();
    
    [JsonProperty("user_id")] public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    [JsonProperty("library_id")] public Ulid? LibraryId { get; set; }
    public Library? Library { get; set; }
    
    [JsonProperty("tv_id")] public int? TvId { get; set; }
    public Tv? Tv { get; set; }
    
    [JsonProperty("movie_id")] public int? MovieId { get; set; }
    public Movie? Movie { get; set; }
    
    [JsonProperty("collection_id")] public int? CollectionId { get; set; }
    public Collection? Collection { get; set; }
    
    [JsonProperty("special_id")] public Ulid? SpecialId { get; set; }
    public Special? Special { get; set; }
    
}