using Newtonsoft.Json;
using NoMercy.Database.Models.Media;

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