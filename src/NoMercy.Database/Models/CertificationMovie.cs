
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(CertificationId), nameof(MovieId))]
[Index(nameof(CertificationId))]
[Index(nameof(MovieId))]
public class CertificationMovie
{
    [JsonProperty("certification_id")] public int CertificationId { get; set; }
    public Certification Certification { get; set; } = null!;

    [JsonProperty("movie_id")] public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public CertificationMovie()
    {
        //
    }
}
