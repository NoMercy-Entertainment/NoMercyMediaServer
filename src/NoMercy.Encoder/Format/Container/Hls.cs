using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Encoder.Format.Container;

public class Hls : BaseContainer
{
    public override ContainerDto ContainerDto => AvailableContainers.First(c => c.Name == VideoContainers.Hls);

    public Hls()
    {
        SetContainer(VideoContainers.Hls);
        AddCustomArgument("-f", VideoFormats.Hls);
    }

    public override CodecDto[] AvailableCodecs =>
    [
        VideoCodecs.H264, VideoCodecs.H264Nvenc,
        VideoCodecs.H265, VideoCodecs.H265Nvenc,
        VideoCodecs.Vp9, VideoCodecs.Vp9Nvenc
    ];

    public override CodecDto[] AvailableVideoCodecs =>
    [
        VideoCodecs.H264, VideoCodecs.H264Nvenc,
        VideoCodecs.H265, VideoCodecs.H265Nvenc,
        VideoCodecs.Vp9, VideoCodecs.Vp9Nvenc
    ];

    public override CodecDto[] AvailableAudioCodecs =>
    [
        AudioCodecs.Aac, AudioCodecs.Opus, AudioCodecs.Vorbis,
        AudioCodecs.Mp3, AudioCodecs.Flac, AudioCodecs.Ac3,
        AudioCodecs.Eac3, AudioCodecs.LibOpus, AudioCodecs.TrueHd
    ];

    public override CodecDto[] AvailableSubtitleCodecs =>
    [
        SubtitleCodecs.Webvtt, SubtitleCodecs.Srt, SubtitleCodecs.Ass,
        SubtitleCodecs.Copy
    ];


    public Hls SetHlsTime(int value)
    {
        HlsTime = value;
        return this;
    }

    public Hls SetHlsListSize(int value)
    {
        HlsListSize = value;
        return this;
    }

    public Hls SetHlsFlags(string value)
    {
        HlsFlags = value;
        return this;
    }

    public Hls SetHlsPlaylistType(string value)
    {
        HlsPlaylistType = value;
        return this;
    }

    public override Hls ApplyFlags()
    {
        base.ApplyFlags();
        // Bitstream filter moved to BaseVideo.AddToDictionary (it's a video-specific option, not container)
        AddCustomArgument("-hls_allow_cache", 1);
        AddCustomArgument("-hls_flags", "independent_segments");
        AddCustomArgument("-hls_segment_type", "mpegts");
        AddCustomArgument("-segment_list_type", "m3u8");
        AddCustomArgument("-segment_time_delta", 1);
        AddCustomArgument("-start_number", 0);
        AddCustomArgument("-use_wallclock_as_timestamps", 1);
        AddCustomArgument("-hls_playlist_type", HlsPlaylistType);
        AddCustomArgument("-hls_init_time", HlsTime);
        AddCustomArgument("-hls_time", HlsTime);
        AddCustomArgument("-hls_flags", HlsFlags);
        AddCustomArgument("-hls_list_size", HlsListSize);

        return this;
    }
}