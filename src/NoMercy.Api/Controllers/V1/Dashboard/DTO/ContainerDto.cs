using Newtonsoft.Json;
using NoMercy.Data.Logic.Seeds;
using NoMercy.Encoder.Core;
using NoMercy.Encoder.Format.Audio;
using NoMercy.Encoder.Format.Container;
using NoMercy.Encoder.Format.Rules;
using NoMercy.Encoder.Format.Subtitle;
using NoMercy.Encoder.Format.Video;
using NoMercy.NmSystem.Extensions;
using Ass = NoMercy.Encoder.Format.Container.Ass;
using Flac = NoMercy.Encoder.Format.Container.Flac;
using Mp3 = NoMercy.Encoder.Format.Container.Mp3;
using Srt = NoMercy.Encoder.Format.Container.Srt;
using Vtt = NoMercy.Encoder.Format.Container.Vtt;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;
public record ContainerDto
{
    [JsonProperty("label")] public string Label { get; set; } = string.Empty;
    [JsonProperty("value")] public string Value { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("default")] public bool IsDefault { get; set; }
    [JsonProperty("available_video_codecs")] public VideoCodecDto[]  AvailableVideoCodecs { get; set; }
    [JsonProperty("available_audio_codecs")] public AudioCodecDto[] AvailableAudioCodecs { get; set; }
    [JsonProperty("available_subtitle_codecs")] public SubtitleCodecDto[] AvailableSubtitleCodecs { get; set; }
    [JsonProperty("available_resolutions")] public Classes.VideoQualityDto[] AvailableVideoSizes { get; set; }

    public ContainerDto(Classes.ContainerDto container)
    {
        BaseContainer containerData = container switch
        {
            { Name: "m3u8" } => new Hls(),
            { Name: "mkv" } => new Mkv(),
            { Name: "mp4" } => new Mp4(),
            { Name: "webm" } => new WebM(),
            { Name: "ass" } => new Ass(),
            { Name: "flac" } => new Flac(),
            { Name: "mp3" } => new Mp3(),
            { Name: "vtt" } => new Vtt(),
            { Name: "srt" } => new Srt(),
            _ => throw new ArgumentOutOfRangeException(nameof(container.Name))
        };

        VideoCodecDto[] videoCodecs  = containerData.AvailableVideoCodecs
            .Select(c => new VideoCodecDto(c))
            .ToArray();

        AudioCodecDto[] audioCodecs  = containerData.AvailableAudioCodecs
            .Select(c => new AudioCodecDto(c))
            .ToArray();

        SubtitleCodecDto[] subtitleCodecs = containerData.AvailableSubtitleCodecs
            .Select(c => new SubtitleCodecDto(c))
            .ToArray();

        Label = BaseContainer.GetName(container.Name);
        Value = container.Name;
        Type = container.Type;
        IsDefault = container.IsDefault;
        AvailableVideoCodecs = videoCodecs;
        AvailableAudioCodecs = audioCodecs;
        AvailableSubtitleCodecs = subtitleCodecs;

        AvailableVideoSizes = container.Type == "video"
            ? BaseVideo.AvailableVideoSizes
            : [];
    }
}

public class VideoCodecDto: Classes.CodecDto {
    [JsonProperty("color_spaces")] public LabelValueDto[]  AvailableVideoColorSpaces { get; set; }
    [JsonProperty("tunes")] public LabelValueDto[]  AvailableVideoTunes { get; set; }
    [JsonProperty("profiles")] public LabelValueDto[] AvailableVideoProfiles { get; set; }
    [JsonProperty("presets")] public LabelValueDto[] AvailablePresets { get; set; }

    public VideoCodecDto(Classes.CodecDto codecDto)
    {
        BaseVideo codecData = codecDto switch
        {
            { SimpleValue: "h264" } => new X264(),
            { SimpleValue: "h264_nvenc"} => new X264("h264_nvenc"),
            { SimpleValue: "h265" } => new X265(),
            { SimpleValue: "hevc_nvenc"} => new X265("hevc_nvenc"),
            { SimpleValue: "vp9" } => new Vp9(),
            { SimpleValue: "vp9_nvenc"} => new Vp9("vp9_nvenc"),
            _ => throw new ArgumentOutOfRangeException(nameof(codecDto.SimpleValue))
        };

        Name = codecDto.Name;
        Value = codecDto.Value;
        SimpleValue = codecDto.SimpleValue;
        RequiresGpu = codecDto.RequiresGpu;
        IsDefault = codecDto.IsDefault;
        AvailableVideoColorSpaces = codecData.AvailableColorSpaces.Select(p => new LabelValueDto(p)).ToArray();
        AvailableVideoProfiles = codecData.AvailableProfiles.Select(p => new LabelValueDto(p)).ToArray();
        AvailableVideoTunes = codecData.AvailableTune.Select(t => new LabelValueDto(t)).ToArray();
        AvailablePresets = codecData.AvailablePresets.Select(p => new LabelValueDto(p)).ToArray();
    }

}

public class AudioCodecDto : Classes.CodecDto
{
    [JsonProperty("available_languages")] public LabelValueDto[] AvailableLanguages { get; set; }
    [JsonProperty("audio_quality_level")] public int AudioQualityLevel { get; set; }
    [JsonProperty("audio_channels")] public int AudioChannels { get; set; }
    [JsonProperty("hls_segment_filename")] public string HlsSegmentFilename { get; set; }
    [JsonProperty("hls_playlist_filename")] public string HlsPlaylistFilename { get; set; }
    [JsonProperty("bit_rate")] public long BitRate { get; set; }

    public AudioCodecDto(Classes.CodecDto codecDto)
    {
        BaseAudio codecData = codecDto switch
        {
            { SimpleValue: "aac" } => new Aac(),
            { SimpleValue: "ac3" } => new Ac3(),
            { SimpleValue: "eac3" } => new Eac3(),
            { SimpleValue: "flac" } => new Encoder.Format.Audio.Flac(),
            { SimpleValue: "mp3" } => new Encoder.Format.Audio.Mp3(),
            { SimpleValue: "opus" } => new Opus(),
            { SimpleValue: "truehd" } => new TrueHd(),
            { SimpleValue: "vorbis" } => new Vorbis(),
            _ => throw new ArgumentOutOfRangeException(nameof(codecDto.SimpleValue))
        };

        Name = codecDto.Name;
        Value = codecDto.Value;
        SimpleValue = codecDto.SimpleValue;
        IsDefault = codecDto.IsDefault;
        AudioQualityLevel = codecData.AudioQualityLevel;
        AudioChannels = codecData.AudioChannels;
        HlsSegmentFilename = codecData.HlsSegmentFilename;
        HlsPlaylistFilename = codecData.HlsPlaylistFilename;
        BitRate = codecData._bitRate;
        AvailableLanguages = EncoderProfileSeedData.AllLanguages()
            .Select(l => new LabelValueDto
            {
                Label = IsoLanguageMapper.IsoToLanguage[l].ToTitleCase(),
                Value = l
            }).ToArray();
    }

}

public class SubtitleCodecDto : Classes.CodecDto
{
    [JsonProperty("available_languages")] public LabelValueDto[] AvailableLanguages { get; set; }
    [JsonProperty("hls_segment_filename")] public string HlsSegmentFilename { get; set; }
    [JsonProperty("hls_playlist_filename")] public string HlsPlaylistFilename { get; set; }

    public SubtitleCodecDto(Classes.CodecDto codecDto)
    {
        BaseSubtitle codecData = codecDto switch
        {
            { SimpleValue: "vtt" } => new Encoder.Format.Subtitle.Vtt(),
            { SimpleValue: "srt" } => new Encoder.Format.Subtitle.Srt(),
            { SimpleValue: "ass" } => new Encoder.Format.Subtitle.Ass(),
            { SimpleValue: "copy" } => new Copy(),
            _ => throw new ArgumentOutOfRangeException(nameof(codecDto.SimpleValue))
        };

        Name = codecDto.Name;
        Value = codecDto.Value;
        SimpleValue = codecDto.SimpleValue;
        IsDefault = codecDto.IsDefault;
        HlsSegmentFilename = codecData.HlsSegmentFilename;
        HlsPlaylistFilename = codecData.HlsPlaylistFilename;
        AvailableLanguages = EncoderProfileSeedData.AllLanguages()
            .Select(l => new LabelValueDto
            {
                Label = IsoLanguageMapper.IsoToLanguage[l].ToTitleCase(),
                Value = l
            }).ToArray();
    }
}

public class LabelValueDto
{
    [JsonProperty("label")] public string Label { get; set; } = string.Empty;
    [JsonProperty("value")] public string Value { get; set; } = string.Empty;

    public LabelValueDto(string s)
    {
        Label = s;
        Value = s;
    }

    public LabelValueDto()
    {
        //
    }
}