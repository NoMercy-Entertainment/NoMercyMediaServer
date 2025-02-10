
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(CreditId), nameof(MovieId), nameof(RoleId), IsUnique = true)]
[Index(nameof(CreditId), nameof(TvId), nameof(RoleId), IsUnique = true)]
[Index(nameof(CreditId), nameof(SeasonId), nameof(RoleId), IsUnique = true)]
[Index(nameof(CreditId), nameof(EpisodeId), nameof(RoleId), IsUnique = true)]
[Index(nameof(RoleId), IsUnique = false)]
public class Cast
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("credit_id")] public string? CreditId { get; set; }

    [JsonProperty("person_id")] public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    [JsonProperty("movie_id")] public int? MovieId { get; set; }
    public Movie? Movie { get; set; } = new();

    [JsonProperty("tv_id")] public int? TvId { get; set; }
    public Tv? Tv { get; set; } = new();

    [JsonProperty("season_id")] public int? SeasonId { get; set; }
    public Season? Season { get; set; } = new();

    [JsonProperty("episode_id")] public int? EpisodeId { get; set; }
    public Episode? Episode { get; set; } = new();

    [JsonProperty("role_id")] public int? RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public Cast()
    {
        //
    }
}
