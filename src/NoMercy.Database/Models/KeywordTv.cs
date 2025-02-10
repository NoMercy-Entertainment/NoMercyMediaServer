
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(KeywordId), nameof(TvId))]
[Index(nameof(KeywordId))]
[Index(nameof(TvId))]
public class KeywordTv
{
    [JsonProperty("keyword_id")] public int KeywordId { get; set; }
    public Keyword Keyword { get; set; } = null!;

    [JsonProperty("tv_id")] public int TvId { get; set; }
    public Tv Tv { get; set; } = null!;

    public KeywordTv()
    {
        //
    }
}
