using Newtonsoft.Json;
using NoMercy.Encoder.Core;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Encoder.Dto;

[Serializable]
public class AudioStream
{
    [JsonProperty("index")] public int Index { get; set; }
    [JsonProperty("profile")] public string? Profile { get; set; }
    [JsonProperty("codec_name")] public string? CodecName { get; set; }
    [JsonProperty("codec_long_name")] public string? CodecLongName { get; set; }
    [JsonProperty("codec_type")] public CodecType CodecType { get; set; }
    [JsonProperty("time_base")] public string? TimeBase { get; set; }
    [JsonProperty("duration")] public double Duration { get; set; }
    [JsonProperty("language")] public string? Language { get; set; }
    [JsonProperty("language_name")] public string? LanguageName => IsoLanguageMapper.GetLanguageName(Language ?? string.Empty);
    [JsonProperty("size")] public long Size { get; set; }
    
    [JsonProperty("sample_fmt")] public string? SampleFmt { get; set; }
    [JsonProperty("sample_rate")] public long? SampleRate { get; set; }
    [JsonProperty("channels")] public long? Channels { get; set; }
    [JsonProperty("channel_layout")] public string? ChannelLayout { get; set; }
    [JsonProperty("bits_per_sample")] public long? BitsPerSample { get; set; }
    [JsonProperty("bit_rate")]  public long? BitRate { get; set; }
    
    [JsonProperty("is_default")] public bool IsDefault  { get; set; }
    [JsonProperty("is_dub")] public bool IsDub  { get; set; }
    [JsonProperty("is_forced")] public bool IsForced  { get; set; }
    [JsonProperty("is_visual_impaired")] public bool IsVisualImpaired  { get; set; }
    
    [JsonProperty("tags")] public Dictionary<string,string> Tags { get; set; } = new();
    
    public AudioStream()
    {
    }

    public AudioStream(FfprobeSourceDataStream ffprobeSourceDataStream)
    {
        Index = ffprobeSourceDataStream.Index;
        Profile = ffprobeSourceDataStream.Profile ?? string.Empty;
        CodecName = ffprobeSourceDataStream.CodecName ?? string.Empty;
        CodecLongName = ffprobeSourceDataStream.CodecLongName ?? string.Empty;
        CodecType = ffprobeSourceDataStream.CodecType;
        TimeBase = ffprobeSourceDataStream.TimeBase ?? string.Empty;
        Language = ffprobeSourceDataStream.Tags.TryGetValue("language", out string? language) ? language :  "und";
        Duration = ffprobeSourceDataStream.Tags.TryGetValue($"DURATION-{Language}", out string? duration) ? duration.ToSeconds() : ffprobeSourceDataStream.Duration.ToSeconds();
        Size = ffprobeSourceDataStream.Tags.TryGetValue($"NUMBER_OF_BYTES-{Language}", out string? numberOfBytes) ? numberOfBytes.ToLong() : 0;
        SampleFmt = ffprobeSourceDataStream.SampleFmt ?? string.Empty;
        SampleRate = ffprobeSourceDataStream.SampleRate;
        Channels = ffprobeSourceDataStream.Channels;
        ChannelLayout = ffprobeSourceDataStream.ChannelLayout ?? string.Empty;
        BitsPerSample = ffprobeSourceDataStream.BitsPerSample;
        BitRate = ffprobeSourceDataStream.BitRate;
        
        if(Size == 0 && BitRate is not null && Duration is not 0)
        {
            Size = (long)(Duration * BitRate / 8);
        }
        
        IsDefault = ffprobeSourceDataStream.Disposition.TryGetValue("default", out int defaultValue) && defaultValue == 1;
        IsDub = ffprobeSourceDataStream.Disposition.TryGetValue("dub", out int dubValue) && dubValue == 1;
        IsForced = ffprobeSourceDataStream.Disposition.TryGetValue("forced", out int forcedValue) && forcedValue == 1;
        IsVisualImpaired = ffprobeSourceDataStream.Disposition.TryGetValue("visual_impaired", out int visualImpairedValue) && visualImpairedValue == 1;
        
        Tags = ffprobeSourceDataStream.Tags;
    }
}