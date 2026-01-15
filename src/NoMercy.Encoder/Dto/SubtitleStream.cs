using Newtonsoft.Json;
using NoMercy.Encoder.Core;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Encoder.Dto;

[Serializable]
public class SubtitleStream
{
    [JsonProperty("index")] public int Index { get; set; }
    [JsonProperty("codec_name")] public string? CodecName { get; set; }
    [JsonProperty("codec_long_name")] public string? CodecLongName { get; set; }
    [JsonProperty("codec_type")] public CodecType CodecType { get; set; }
    [JsonProperty("time_base")] public string? TimeBase { get; set; }
    [JsonProperty("duration")] public double Duration { get; set; }
    [JsonProperty("language")] public string? Language { get; set; }
    [JsonProperty("language_name")] public string? LanguageName => IsoLanguageMapper.GetLanguageName(Language ?? string.Empty);
    [JsonProperty("size")] public long Size { get; set; }
    
    [JsonProperty("is_default")] public bool IsDefault  { get; set; }
    [JsonProperty("is_dub")] public bool IsDub  { get; set; }
    [JsonProperty("is_forced")] public bool IsForced  { get; set; }
    [JsonProperty("is_hearing_impaired")] public bool IsHearingImpaired  { get; set; }
    
    [JsonProperty("tags")] public Dictionary<string, string> Tags { get; set; } = new();

    public SubtitleStream()
    {
        
    }
    
    public SubtitleStream(FfprobeSourceDataStream ffprobeSourceDataStream)
    {
        Index = ffprobeSourceDataStream.Index;
        CodecName = ffprobeSourceDataStream.CodecName;
        CodecLongName = ffprobeSourceDataStream.CodecLongName;
        CodecType = ffprobeSourceDataStream.CodecType;
        TimeBase = ffprobeSourceDataStream.TimeBase;
        Duration = ffprobeSourceDataStream.Duration;
        Language = ffprobeSourceDataStream.Tags.TryGetValue("language", out string? language) ? language :  "und";
        Size = ffprobeSourceDataStream.Tags.TryGetValue("number_of_bytes", out string? numberOfBytes) ? numberOfBytes.ToLong() : 0;
        
        IsDefault = ffprobeSourceDataStream.Disposition.TryGetValue("default", out int defaultValue) && defaultValue == 1;
        IsDub = ffprobeSourceDataStream.Disposition.TryGetValue("dub", out int dubValue) && dubValue == 1;
        IsForced = ffprobeSourceDataStream.Disposition.TryGetValue("forced", out int forcedValue) && forcedValue == 1;
        IsHearingImpaired = ffprobeSourceDataStream.Disposition.TryGetValue("hearing_impaired", out int hearingImpairedValue) && hearingImpairedValue == 1;
        
        Tags = ffprobeSourceDataStream.Tags;
    }
}