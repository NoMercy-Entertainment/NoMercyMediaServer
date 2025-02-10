
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(CertificationId), nameof(TvId))]
[Index(nameof(CertificationId))]
[Index(nameof(TvId))]
public class CertificationTv
{
    [JsonProperty("certification_id")] public int CertificationId { get; set; }
    public Certification Certification { get; set; } = null!;

    [JsonProperty("tv_id")] public int TvId { get; set; }
    public Tv Tv { get; set; } = null!;

    public CertificationTv()
    {
        //
    }
}
