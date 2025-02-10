using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(CreditId), nameof(MovieId), nameof(JobId), IsUnique = true)]
[Index(nameof(CreditId), nameof(TvId), nameof(JobId), IsUnique = true)]
[Index(nameof(CreditId), nameof(SeasonId), nameof(JobId), IsUnique = true)]
[Index(nameof(CreditId), nameof(EpisodeId), nameof(JobId), IsUnique = true)]
[Index(nameof(CreditId))]
[Index(nameof(MovieId))]
[Index(nameof(TvId))]
[Index(nameof(SeasonId))]
[Index(nameof(EpisodeId))]
[Index(nameof(PersonId))]
[Index(nameof(JobId), IsUnique = false)]
public class Crew
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }
    [JsonProperty("credit_id")] public string? CreditId { get; set; }
    
    [JsonProperty("movie_id")] public int? MovieId { get; set; }
    public Movie? Movie { get; set; }

    [JsonProperty("tv_id")] public int? TvId { get; set; }
    public Tv? Tv { get; set; }

    [JsonProperty("season_id")] public int? SeasonId { get; set; }
    public Season? Season { get; set; }

    [JsonProperty("episode_id")] public int? EpisodeId { get; set; }
    public Episode? Episode { get; set; }

    [JsonProperty("person_id")] public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    [JsonProperty("job_id")] public int? JobId { get; set; }
    public Job Job { get; set; } = null!;

    public Crew()
    {
    }
}
