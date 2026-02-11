using Newtonsoft.Json;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.Data.Logic;

public class EncoderProfileParamsDto
{
    [JsonProperty("width")] public int Width { get; set; }
    [JsonProperty("crf")] public int Crf { get; set; }
    [JsonProperty("preset")] public string Preset { get; set; } = string.Empty;
    [JsonProperty("profile")] public string Profile { get; set; } = string.Empty;
    [JsonProperty("codec")] public string Codec { get; set; } = string.Empty;
    [JsonProperty("audio")] public string Audio { get; set; } = string.Empty;

    public EncoderProfileParamsDto()
    {
        
    }
    public EncoderProfileParamsDto(EncoderProfile argEncoderProfile)
    {
        
    }

}