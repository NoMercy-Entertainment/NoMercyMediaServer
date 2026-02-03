using NoMercy.EncoderV2.Abstractions;
using NoMercy.EncoderV2.Codecs.Audio;
using NoMercy.EncoderV2.Codecs.Subtitle;
using NoMercy.EncoderV2.Codecs.Video;

namespace NoMercy.EncoderV2.Factories;

/// <summary>
/// Factory for creating codec instances
/// </summary>
public sealed class CodecFactory : ICodecFactory
{
    public IReadOnlyList<string> AvailableVideoCodecs =>
    [
        // H.264
        "libx264", "h264_nvenc", "h264_qsv", "h264_videotoolbox",
        // H.265
        "libx265", "hevc_nvenc", "hevc_qsv", "hevc_videotoolbox",
        // AV1
        "libaom-av1", "libsvtav1", "av1_nvenc", "av1_qsv",
        // VP9
        "libvpx-vp9",
        // Copy
        "copy"
    ];

    public IReadOnlyList<string> AvailableAudioCodecs =>
    [
        "aac", "libfdk_aac", "libopus", "ac3", "eac3",
        "flac", "libmp3lame", "libvorbis", "copy"
    ];

    public IReadOnlyList<string> AvailableSubtitleCodecs =>
    [
        "webvtt", "srt", "ass", "mov_text", "dvbsub", "copy"
    ];

    public IVideoCodec? CreateVideoCodec(string name)
    {
        return name.ToLowerInvariant() switch
        {
            // H.264
            "libx264" or "h264" or "x264" => new H264Codec(),
            "h264_nvenc" => new H264NvencCodec(),
            "h264_qsv" => new H264QsvCodec(),
            "h264_videotoolbox" => new H264VideoToolboxCodec(),

            // H.265
            "libx265" or "h265" or "x265" or "hevc" => new H265Codec(),
            "hevc_nvenc" or "h265_nvenc" => new H265NvencCodec(),
            "hevc_qsv" or "h265_qsv" => new H265QsvCodec(),
            "hevc_videotoolbox" or "h265_videotoolbox" => new H265VideoToolboxCodec(),

            // AV1
            "libaom-av1" or "av1" or "aom" => new Av1Codec(),
            "libsvtav1" or "svtav1" or "svt-av1" => new Av1SvtCodec(),
            "av1_nvenc" => new Av1NvencCodec(),
            "av1_qsv" => new Av1QsvCodec(),

            // VP9
            "libvpx-vp9" or "vp9" => new Vp9Codec(),

            // Copy
            "copy" => new VideoCopyCodec(),

            _ => null
        };
    }

    public IAudioCodec? CreateAudioCodec(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "aac" => new AacCodec(),
            "libfdk_aac" or "fdk_aac" or "fdkaac" => new FdkAacCodec(),
            "libopus" or "opus" => new OpusCodec(),
            "ac3" => new Ac3Codec(),
            "eac3" or "e-ac3" => new Eac3Codec(),
            "flac" => new FlacCodec(),
            "libmp3lame" or "mp3" => new Mp3Codec(),
            "libvorbis" or "vorbis" => new VorbisCodec(),
            "copy" => new AudioCopyCodec(),
            _ => null
        };
    }

    public ISubtitleCodec? CreateSubtitleCodec(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "webvtt" or "vtt" => new WebvttCodec(),
            "srt" or "subrip" => new SrtCodec(),
            "ass" or "ssa" => new AssCodec(),
            "mov_text" or "movtext" => new MovTextCodec(),
            "dvbsub" or "dvb_subtitle" => new DvbSubCodec(),
            "copy" => new SubtitleCopyCodec(),
            _ => null
        };
    }

    /// <summary>
    /// Creates a video codec with common preset applied
    /// </summary>
    public IVideoCodec CreateVideoCodecWithPreset(string name, VideoPreset preset)
    {
        IVideoCodec? codec = CreateVideoCodec(name);
        if (codec == null)
        {
            throw new ArgumentException($"Unknown video codec: {name}");
        }

        ApplyPreset(codec, preset);
        return codec;
    }

    private static void ApplyPreset(IVideoCodec codec, VideoPreset preset)
    {
        switch (preset)
        {
            case VideoPreset.UltraFast:
                codec.Preset = codec.AvailablePresets.Contains("ultrafast") ? "ultrafast" : codec.AvailablePresets.FirstOrDefault();
                codec.Crf = 28;
                break;
            case VideoPreset.Fast:
                codec.Preset = codec.AvailablePresets.Contains("fast") ? "fast" : codec.AvailablePresets.FirstOrDefault();
                codec.Crf = 23;
                break;
            case VideoPreset.Balanced:
                codec.Preset = codec.AvailablePresets.Contains("medium") ? "medium" : codec.AvailablePresets.FirstOrDefault();
                codec.Crf = 22;
                break;
            case VideoPreset.Quality:
                codec.Preset = codec.AvailablePresets.Contains("slow") ? "slow" : codec.AvailablePresets.FirstOrDefault();
                codec.Crf = 20;
                break;
            case VideoPreset.HighQuality:
                codec.Preset = codec.AvailablePresets.Contains("slower") ? "slower" :
                               codec.AvailablePresets.Contains("slow") ? "slow" : codec.AvailablePresets.FirstOrDefault();
                codec.Crf = 18;
                break;
        }
    }
}

public enum VideoPreset
{
    UltraFast,
    Fast,
    Balanced,
    Quality,
    HighQuality
}
