using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Core;
using NoMercy.Encoder.Format.Audio;
using NoMercy.Encoder.Format.Container;
using NoMercy.Encoder.Format.Rules;
using NoMercy.Encoder.Format.Video;
using NoMercy.NmSystem;

namespace NoMercy.Tests.Encoder;

[Trait("Category", "Characterization")]
public class EncoderCommandBuildingTests : IDisposable
{
    private readonly string _tempDir;

    public EncoderCommandBuildingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "NoMercy_EncoderTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    #region Helper Methods

    private static FfProbeData CreateSdrProbeData(
        int width = 1920, int height = 1080,
        string pixelFormat = "yuv420p", string colorSpace = "bt709")
    {
        return new()
        {
            FilePath = "/input/test-movie.mkv",
            Duration = TimeSpan.FromMinutes(120),
            VideoStreams = [CreateVideoStream(width, height, pixelFormat, colorSpace)],
            AudioStreams = [CreateAudioStream("eng", 2, 48000, 128000)],
            PrimaryVideoStream = CreateVideoStream(width, height, pixelFormat, colorSpace),
            PrimaryAudioStream = CreateAudioStream("eng", 2, 48000, 128000)
        };
    }

    private static FfProbeData CreateHdrProbeData()
    {
        return new()
        {
            FilePath = "/input/hdr-movie.mkv",
            Duration = TimeSpan.FromMinutes(120),
            VideoStreams = [CreateVideoStream(3840, 2160, "yuv420p10le", "bt2020nc")],
            AudioStreams = [CreateAudioStream("eng", 6, 48000, 640000)],
            PrimaryVideoStream = CreateVideoStream(3840, 2160, "yuv420p10le", "bt2020nc"),
            PrimaryAudioStream = CreateAudioStream("eng", 6, 48000, 640000)
        };
    }

    private static FfProbeVideoStream CreateVideoStream(
        int width, int height,
        string pixelFormat = "yuv420p", string colorSpace = "bt709",
        int index = 0)
    {
        return new()
        {
            Width = width,
            Height = height,
            PixFmt = pixelFormat,
            ColorSpace = colorSpace,
            Index = index,
            CodecName = "h264"
        };
    }

    private static FfProbeAudioStream CreateAudioStream(
        string language = "eng", int channels = 2,
        int sampleRate = 48000, long bitRate = 128000,
        int index = 1)
    {
        return new()
        {
            Language = language,
            Channels = channels,
            SampleRate = sampleRate,
            BitRate = bitRate,
            Index = index,
            CodecName = "aac"
        };
    }

    private Hls CreateHlsContainer(FfProbeData probeData, BaseVideo videoCodec, BaseAudio audioCodec)
    {
        Hls hls = new();
        hls.InputFile = probeData.FilePath;
        hls.FfProbeData = probeData;
        hls.Title = "Test Movie";
        hls.BasePath = _tempDir;
        hls.FileName = "playlist";
        hls.IsVideo = true;

        // Build video stream manually (mimics what VideoAudioFile.AddContainer does)
        videoCodec.VideoStreams = probeData.VideoStreams;
        videoCodec.VideoStream = probeData.PrimaryVideoStream;
        videoCodec.Index = probeData.PrimaryVideoStream!.Index;
        videoCodec.Title = "Test Movie";
        videoCodec.Container = hls;
        videoCodec.FileName = "playlist";
        videoCodec.BasePath = _tempDir;
        BaseVideo builtVideo = videoCodec.Build();
        builtVideo.ApplyFlags();
        hls.VideoStreams.Add(builtVideo);

        // Build audio stream manually
        audioCodec.AudioStreams = probeData.AudioStreams;
        audioCodec.AudioStream = probeData.PrimaryAudioStream!;
        audioCodec.IsAudio = true;
        audioCodec.FileName = "playlist";
        audioCodec.BasePath = _tempDir;
        List<BaseAudio> audioStreams = audioCodec.Build();
        foreach (BaseAudio a in audioStreams)
            a.Extension = hls.Extension;
        hls.AudioStreams.AddRange(audioStreams);

        return hls;
    }

    private static string BuildCommand(BaseContainer container, FfProbeData probeData)
    {
        FFmpegCommandBuilder builder = new(
            container: container,
            ffProbeData: probeData,
            accelerators: [],
            priority: false
        );
        return builder.BuildCommand();
    }

    #endregion

    #region FFmpegCommandBuilder — Global Options

    [Fact]
    public void BuildCommand_ContainsHideBanner()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-hide_banner", command);
    }

    [Fact]
    public void BuildCommand_Video_ContainsProbeSizeAndAnalyzeDuration()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-probesize 4092M", command);
        Assert.Contains("-analyzeduration 9999M", command);
    }

    [Fact]
    public void BuildCommand_ContainsProgressFlag()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-progress -", command);
    }

    [Fact]
    public void BuildCommand_NoPriority_ContainsThreads0()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-threads 0", command);
    }

    [Fact]
    public void BuildCommand_Priority_ContainsHigherThreadCount()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        FFmpegCommandBuilder builder = new(
            container: hls,
            ffProbeData: probe,
            accelerators: [],
            priority: true
        );
        string command = builder.BuildCommand();

        int expectedThreads = (int)Math.Floor(Environment.ProcessorCount * 2.0);
        Assert.Contains($"-threads {expectedThreads}", command);
    }

    #endregion

    #region FFmpegCommandBuilder — Input Options

    [Fact]
    public void BuildCommand_ContainsInputFile()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-i \"/input/test-movie.mkv\"", command);
    }

    [Fact]
    public void BuildCommand_ContainsOverwriteFlag()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains(" -y ", command);
    }

    [Fact]
    public void BuildCommand_ContainsMapMetadata()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-map_metadata -1", command);
    }

    [Fact]
    public void BuildCommand_NoAccelerators_NoGpuFlag()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.DoesNotContain("-gpu any", command);
    }

    [Fact]
    public void BuildCommand_WithAccelerators_ContainsGpuFlag()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        List<GpuAccelerator> accelerators =
        [
            new(GpuVendor.Nvidia, "-init_hw_device cuda=cu:0", "cuda")
        ];

        FFmpegCommandBuilder builder = new(
            container: hls,
            ffProbeData: probe,
            accelerators: accelerators,
            priority: false
        );
        string command = builder.BuildCommand();

        Assert.Contains("-gpu any", command);
        Assert.Contains("-init_hw_device cuda=cu:0", command);
    }

    #endregion

    #region FFmpegCommandBuilder — Video Codec Selection

    [Fact]
    public void BuildCommand_X264_ContainsLibx264Codec()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-c:v libx264", command);
    }

    [Fact]
    public void BuildCommand_X265_ContainsLibx265Codec()
    {
        FfProbeData probe = CreateSdrProbeData();
        X265 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-c:v libx265", command);
    }

    [Fact]
    public void BuildCommand_Av1_ContainsLibrav1eCodec()
    {
        FfProbeData probe = CreateSdrProbeData();
        Av1 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-c:v librav1e", command);
    }

    #endregion

    #region FFmpegCommandBuilder — Video Filter Complex

    [Fact]
    public void BuildCommand_SdrVideo_ContainsFilterComplexWithScaleAndFormat()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        video.CropValue = "1920:1080:0:0";
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-filter_complex", command);
        Assert.Contains("format=yuv420p", command);
        Assert.Contains("[v0_hls_0]", command);
    }

    [Fact]
    public void BuildCommand_AudioStreams_ContainsVolumeFilter()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("volume=3", command);
        Assert.Contains("[a0_hls_0]", command);
    }

    #endregion

    #region FFmpegCommandBuilder — Video Output Parameters

    [Fact]
    public void BuildCommand_VideoMap_ContainsStreamMapping()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-map [v0_hls_0]", command);
    }

    [Fact]
    public void BuildCommand_SdrVideo_ContainsBt709ColorSpace()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-color_primaries bt709", command);
        Assert.Contains("-colorspace bt709", command);
        Assert.Contains("-color_trc bt709", command);
        Assert.Contains("-color_range tv", command);
    }

    [Fact]
    public void BuildCommand_HlsContainer_ContainsBitstreamFilter_H264()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-bsf:v h264_mp4toannexb", command);
    }

    [Fact]
    public void BuildCommand_HlsContainer_ContainsBitstreamFilter_Hevc()
    {
        FfProbeData probe = CreateSdrProbeData();
        X265 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-bsf:v hevc_mp4toannexb", command);
    }

    [Fact]
    public void BuildCommand_ContainsTitle()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-metadata title=", command);
        Assert.Contains("Test Movie", command);
    }

    #endregion

    #region FFmpegCommandBuilder — HLS-Specific Parameters

    [Fact]
    public void BuildCommand_Hls_ContainsSegmentFilename()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-hls_segment_filename", command);
        Assert.Contains("_%05d.ts", command);
    }

    [Fact]
    public void BuildCommand_Hls_ContainsHlsAllowCache()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-hls_allow_cache 1", command);
    }

    [Fact]
    public void BuildCommand_Hls_ContainsSegmentType()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-hls_segment_type mpegts", command);
    }

    [Fact]
    public void BuildCommand_Hls_ContainsPlaylistType()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-hls_playlist_type", command);
    }

    [Fact]
    public void BuildCommand_Hls_ContainsHlsTime()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-hls_time 4", command);
        Assert.Contains("-hls_init_time 4", command);
    }

    [Fact]
    public void BuildCommand_Hls_ContainsHlsFlags()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-hls_flags independent_segments", command);
    }

    [Fact]
    public void BuildCommand_Hls_ContainsFormat()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-f hls", command);
    }

    [Fact]
    public void BuildCommand_Hls_ContainsStartNumber()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-start_number 0", command);
    }

    [Fact]
    public void BuildCommand_HlsCustomTime_ReflectsCustomValue()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");

        Hls hls = new Hls().SetHlsTime(10);
        hls.InputFile = probe.FilePath;
        hls.FfProbeData = probe;
        hls.Title = "Test Movie";
        hls.BasePath = _tempDir;
        hls.FileName = "playlist";
        hls.IsVideo = true;

        video.VideoStreams = probe.VideoStreams;
        video.VideoStream = probe.PrimaryVideoStream;
        video.Index = probe.PrimaryVideoStream!.Index;
        video.Title = "Test Movie";
        video.Container = hls;
        video.FileName = "playlist";
        video.BasePath = _tempDir;
        BaseVideo builtVideo = video.Build();
        builtVideo.ApplyFlags();
        hls.VideoStreams.Add(builtVideo);

        audio.AudioStreams = probe.AudioStreams;
        audio.AudioStream = probe.PrimaryAudioStream!;
        audio.IsAudio = true;
        audio.FileName = "playlist";
        audio.BasePath = _tempDir;
        List<BaseAudio> audioStreams = audio.Build();
        foreach (BaseAudio a in audioStreams)
            a.Extension = hls.Extension;
        hls.AudioStreams.AddRange(audioStreams);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-hls_time 10", command);
    }

    #endregion

    #region FFmpegCommandBuilder — Audio Output Parameters

    [Fact]
    public void BuildCommand_AacAudio_ContainsAacCodec()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-c:a aac", command);
    }

    [Fact]
    public void BuildCommand_AudioMap_ContainsAudioStreamMapping()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-map [a0_hls_0]", command);
    }

    [Fact]
    public void BuildCommand_AudioWithBitrate_ContainsBitrate()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetAudioKiloBitrate(192);
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-b:a 192k", command);
    }

    [Fact]
    public void BuildCommand_AudioWithChannels_ContainsChannels()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetAudioChannels(6);
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-ac 6", command);
    }

    [Fact]
    public void BuildCommand_AudioWithSampleRate_ContainsSampleRate()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetSampleRate(44100);
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-ar 44100", command);
    }

    #endregion

    #region BaseVideo — Codec-Specific Quality Flags

    [Fact]
    public void BuildCommand_X264WithCrf_ContainsCrfAndVbrFlags()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetConstantRateFactor(23);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-crf", command);
        Assert.Contains("-rc", command);
    }

    [Fact]
    public void BuildCommand_X264WithPreset_ContainsPreset()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetPreset("medium");
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-preset", command);
        Assert.Contains("medium", command);
    }

    [Fact]
    public void BuildCommand_X264WithProfile_ContainsProfile()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetProfile("high");
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-profile:v", command);
        Assert.Contains("high", command);
    }

    [Fact]
    public void BuildCommand_X264WithBitrate_ContainsBitrate()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetKiloBitrate(5000);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-b:v", command);
        Assert.Contains("5000k", command);
    }

    [Fact]
    public void BuildCommand_X264WithMaxRate_ContainsMaxRate()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetMaxRate(8000);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-maxrate", command);
        Assert.Contains("8000k", command);
    }

    [Fact]
    public void BuildCommand_X264WithBufferSize_ContainsBufferSize()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetBufferSize(10000);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-bufsize", command);
        Assert.Contains("10000k", command);
    }

    [Fact]
    public void BuildCommand_X264WithLevel_ContainsLevel()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetLevel("4.1");
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-level:v", command);
        Assert.Contains("4.1", command);
    }

    [Fact]
    public void BuildCommand_X264WithKeyIntAndFrameRate_ContainsKeyframeInterval()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetKeyInt(48);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-g", command);
        Assert.Contains("-keyint_min", command);
    }

    #endregion

    #region BaseVideo — Codec Factory

    [Theory]
    [InlineData("libx264", typeof(X264))]
    [InlineData("h264_nvenc", typeof(X264))]
    [InlineData("libx265", typeof(X265))]
    [InlineData("hevc_nvenc", typeof(X265))]
    [InlineData("vp9", typeof(Vp9))]
    [InlineData("libvpx-vp9", typeof(Vp9))]
    public void BaseVideoCreate_ReturnsCorrectType(string codec, Type expectedType)
    {
        BaseVideo result = BaseVideo.Create(codec);
        Assert.IsType(expectedType, result);
    }

    [Fact]
    public void BaseVideoCreate_UnsupportedCodec_Throws()
    {
        Assert.Throws<Exception>(() => BaseVideo.Create("unsupported_codec"));
    }

    #endregion

    #region BaseAudio — Codec Factory

    [Theory]
    [InlineData("aac", typeof(Aac))]
    [InlineData("libmp3lame", typeof(NoMercy.Encoder.Format.Audio.Mp3))]
    [InlineData("opus", typeof(Opus))]
    [InlineData("flac", typeof(NoMercy.Encoder.Format.Audio.Flac))]
    [InlineData("ac3", typeof(Ac3))]
    [InlineData("eac3", typeof(Eac3))]
    [InlineData("truehd", typeof(TrueHd))]
    public void BaseAudioCreate_ReturnsCorrectType(string codec, Type expectedType)
    {
        BaseAudio result = BaseAudio.Create(codec);
        Assert.IsType(expectedType, result);
    }

    [Fact]
    public void BaseAudioCreate_UnsupportedCodec_Throws()
    {
        Assert.Throws<Exception>(() => BaseAudio.Create("unsupported_codec"));
    }

    #endregion

    #region BaseContainer — Container Factory

    [Theory]
    [InlineData("mkv", typeof(Mkv))]
    [InlineData("Mp4", typeof(Mp4))]
    [InlineData("mp3", typeof(NoMercy.Encoder.Format.Container.Mp3))]
    [InlineData("flac", typeof(NoMercy.Encoder.Format.Container.Flac))]
    [InlineData("m3u8", typeof(Hls))]
    public void BaseContainerCreate_ReturnsCorrectType(string container, Type expectedType)
    {
        BaseContainer result = BaseContainer.Create(container);
        Assert.IsType(expectedType, result);
    }

    [Fact]
    public void BaseContainerCreate_UnsupportedContainer_Throws()
    {
        Assert.Throws<Exception>(() => BaseContainer.Create("unsupported_container"));
    }

    #endregion

    #region Codec Fluent API — Setters

    [Fact]
    public void X264_SetConstantRateFactor_InvalidValue_Throws()
    {
        X264 video = new();
        Assert.Throws<Exception>(() => video.SetConstantRateFactor(55));
    }

    [Fact]
    public void X264_SetConstantRateFactor_NegativeValue_Throws()
    {
        X264 video = new();
        Assert.Throws<Exception>(() => video.SetConstantRateFactor(-1));
    }

    [Fact]
    public void X264_SetKiloBitrate_NegativeValue_Throws()
    {
        X264 video = new();
        Assert.Throws<Exception>(() => video.SetKiloBitrate(-5));
    }

    [Fact]
    public void X264_SetPreset_Invalid_Throws()
    {
        X264 video = new();
        Assert.Throws<Exception>(() => video.SetPreset("invalid_preset"));
    }

    [Fact]
    public void X264_SetProfile_Invalid_Throws()
    {
        X264 video = new();
        Assert.Throws<Exception>(() => video.SetProfile("invalid_profile"));
    }

    [Fact]
    public void X264_SetTune_Invalid_Throws()
    {
        X264 video = new();
        Assert.Throws<Exception>(() => video.SetTune("invalid_tune"));
    }

    [Fact]
    public void X264_SetLevel_Invalid_Throws()
    {
        X264 video = new();
        Assert.Throws<Exception>(() => video.SetLevel("99"));
    }

    [Fact]
    public void X264_GetPasses_NoBitrate_Returns1()
    {
        X264 video = new();
        Assert.Equal(1, video.GetPasses());
    }

    [Fact]
    public void X264_GetPasses_WithBitrate_Returns2()
    {
        X264 video = new();
        video.SetKiloBitrate(5000);
        Assert.Equal(2, video.GetPasses());
    }

    [Fact]
    public void X265_GetPasses_NoBitrate_Returns1()
    {
        X265 video = new();
        Assert.Equal(1, video.GetPasses());
    }

    [Fact]
    public void X264_SetConstantRateFactor_MaxValid51_DoesNotThrow()
    {
        X264 video = new();
        Exception? exception = Record.Exception(() => video.SetConstantRateFactor(51));
        Assert.Null(exception);
    }

    [Fact]
    public void X264_SetConstantRateFactor_0_DoesNotThrow()
    {
        X264 video = new();
        Exception? exception = Record.Exception(() => video.SetConstantRateFactor(0));
        Assert.Null(exception);
    }

    [Fact]
    public void X264_DefaultCodec_IsLibx264()
    {
        X264 video = new();
        Assert.Equal("libx264", video.VideoCodec.Value);
    }

    [Fact]
    public void X265_DefaultCodec_IsLibx265()
    {
        X265 video = new();
        Assert.Equal("libx265", video.VideoCodec.Value);
    }

    [Fact]
    public void Av1_DefaultCodec_IsLibrav1e()
    {
        Av1 video = new();
        Assert.Equal("librav1e", video.VideoCodec.Value);
    }

    #endregion

    #region Audio — Setter Validation

    [Fact]
    public void Aac_SetAudioKiloBitrate_InvalidValue_Throws()
    {
        Aac audio = new();
        Assert.Throws<Exception>(() => audio.SetAudioKiloBitrate(0));
    }

    [Fact]
    public void Aac_SetAudioChannels_NegativeValue_Throws()
    {
        Aac audio = new();
        Assert.Throws<Exception>(() => audio.SetAudioChannels(-1));
    }

    [Fact]
    public void Aac_SetSampleRate_InvalidValue_Throws()
    {
        Aac audio = new();
        Assert.Throws<Exception>(() => audio.SetSampleRate(0));
    }

    #endregion

    #region Container — HLS Configuration

    [Fact]
    public void Hls_SetHlsTime_ReflectedInCommand()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");

        Hls hls = new Hls().SetHlsTime(6);
        hls.InputFile = probe.FilePath;
        hls.FfProbeData = probe;
        hls.Title = "Test";
        hls.BasePath = _tempDir;
        hls.FileName = "pl";
        hls.IsVideo = true;
        video.VideoStreams = probe.VideoStreams;
        video.VideoStream = probe.PrimaryVideoStream;
        video.Index = 0;
        video.Title = "Test";
        video.Container = hls;
        video.FileName = "pl";
        video.BasePath = _tempDir;
        hls.VideoStreams.Add(video.Build().ApplyFlags());
        audio.AudioStreams = probe.AudioStreams;
        audio.AudioStream = probe.PrimaryAudioStream!;
        audio.IsAudio = true;
        audio.FileName = "pl";
        audio.BasePath = _tempDir;
        hls.AudioStreams.AddRange(audio.Build());

        string command = BuildCommand(hls, probe);
        Assert.Contains("-hls_time 6", command);
    }

    [Fact]
    public void Hls_SetHlsPlaylistType_ReflectedInCommand()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");

        Hls hls = new Hls().SetHlsPlaylistType("event");
        hls.InputFile = probe.FilePath;
        hls.FfProbeData = probe;
        hls.Title = "Test";
        hls.BasePath = _tempDir;
        hls.FileName = "pl";
        hls.IsVideo = true;
        video.VideoStreams = probe.VideoStreams;
        video.VideoStream = probe.PrimaryVideoStream;
        video.Index = 0;
        video.Title = "Test";
        video.Container = hls;
        video.FileName = "pl";
        video.BasePath = _tempDir;
        hls.VideoStreams.Add(video.Build().ApplyFlags());
        audio.AudioStreams = probe.AudioStreams;
        audio.AudioStream = probe.PrimaryAudioStream!;
        audio.IsAudio = true;
        audio.FileName = "pl";
        audio.BasePath = _tempDir;
        hls.AudioStreams.AddRange(audio.Build());

        string command = BuildCommand(hls, probe);
        Assert.Contains("-hls_playlist_type event", command);
    }

    [Fact]
    public void Hls_ContainerDto_IsHls()
    {
        Hls hls = new();
        Assert.Equal(VideoContainers.Hls, hls.ContainerDto.Name);
    }

    [Fact]
    public void Hls_Extension_IsM3u8()
    {
        Hls hls = new();
        Assert.Equal("m3u8", hls.Extension);
    }

    #endregion

    #region Container — Available Codecs

    [Fact]
    public void Hls_AvailableVideoCodecs_IncludesH264()
    {
        Hls hls = new();
        Assert.Contains(hls.AvailableVideoCodecs, c => c.Value == "libx264");
    }

    [Fact]
    public void Hls_AvailableVideoCodecs_IncludesH265()
    {
        Hls hls = new();
        Assert.Contains(hls.AvailableVideoCodecs, c => c.Value == "libx265");
    }

    [Fact]
    public void Hls_AvailableAudioCodecs_IncludesAac()
    {
        Hls hls = new();
        Assert.Contains(hls.AvailableAudioCodecs, c => c.Value == "aac");
    }

    [Fact]
    public void BaseContainer_GetName_MapsCorrectly()
    {
        Assert.Equal("Hls", BaseContainer.GetName("m3u8"));
        Assert.Equal("Mkv", BaseContainer.GetName("mkv"));
        Assert.Equal("Mp4", BaseContainer.GetName("mp4"));
        Assert.Equal("WebM", BaseContainer.GetName("webm"));
    }

    [Fact]
    public void BaseContainer_GetName_UnsupportedFormat_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BaseContainer.GetName("unsupported"));
    }

    #endregion

    #region Video Codec Constants

    [Fact]
    public void VideoCodecs_H264_HasCorrectValues()
    {
        Assert.Equal("H.264", VideoCodecs.H264.Name);
        Assert.Equal("libx264", VideoCodecs.H264.Value);
        Assert.Equal("h264", VideoCodecs.H264.SimpleValue);
        Assert.False(VideoCodecs.H264.RequiresGpu);
    }

    [Fact]
    public void VideoCodecs_H264Nvenc_RequiresGpu()
    {
        Assert.Equal("h264_nvenc", VideoCodecs.H264Nvenc.Value);
        Assert.True(VideoCodecs.H264Nvenc.RequiresGpu);
    }

    [Fact]
    public void VideoCodecs_H265_HasCorrectValues()
    {
        Assert.Equal("libx265", VideoCodecs.H265.Value);
        Assert.Equal("h265", VideoCodecs.H265.SimpleValue);
        Assert.False(VideoCodecs.H265.RequiresGpu);
    }

    [Fact]
    public void VideoCodecs_Av1_HasCorrectValues()
    {
        Assert.Equal("librav1e", VideoCodecs.Av1.Value);
        Assert.True(VideoCodecs.Av1.RequiresGpu);
    }

    #endregion

    #region Audio Codec Constants

    [Fact]
    public void AudioCodecs_Aac_HasCorrectValues()
    {
        Assert.Equal("aac", AudioCodecs.Aac.Value);
        Assert.Equal("aac", AudioCodecs.Aac.SimpleValue);
    }

    [Fact]
    public void AudioCodecs_Mp3_HasCorrectValues()
    {
        Assert.Equal("libmp3lame", AudioCodecs.Mp3.Value);
        Assert.Equal("mp3", AudioCodecs.Mp3.SimpleValue);
    }

    [Fact]
    public void AudioCodecs_Flac_HasCorrectValues()
    {
        Assert.Equal("flac", AudioCodecs.Flac.Value);
        Assert.Equal("flac", AudioCodecs.Flac.SimpleValue);
    }

    #endregion

    #region Command Order Verification

    [Fact]
    public void BuildCommand_GlobalOptionsBeforeInput()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        int hideBannerPos = command.IndexOf("-hide_banner", StringComparison.Ordinal);
        int inputPos = command.IndexOf("-i \"", StringComparison.Ordinal);

        Assert.True(hideBannerPos < inputPos, "Global options should appear before input");
    }

    [Fact]
    public void BuildCommand_InputBeforeFilterComplex()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        int inputPos = command.IndexOf("-i \"", StringComparison.Ordinal);
        int filterPos = command.IndexOf("-filter_complex", StringComparison.Ordinal);

        Assert.True(inputPos < filterPos, "Input should appear before filter_complex");
    }

    [Fact]
    public void BuildCommand_FilterComplexBeforeOutputs()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        int filterPos = command.IndexOf("-filter_complex", StringComparison.Ordinal);
        int codecPos = command.IndexOf("-c:v", StringComparison.Ordinal);

        Assert.True(filterPos < codecPos, "filter_complex should appear before output codec");
    }

    [Fact]
    public void BuildCommand_VideoOutputsBeforeAudioOutputs()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        int videoCodecPos = command.IndexOf("-c:v", StringComparison.Ordinal);
        int audioCodecPos = command.IndexOf("-c:a", StringComparison.Ordinal);

        Assert.True(videoCodecPos < audioCodecPos, "Video outputs should appear before audio outputs");
    }

    #endregion

    #region Scale Configuration

    [Fact]
    public void SetScale_SingleValue_ReflectedInCommand()
    {
        FfProbeData probe = CreateSdrProbeData(1280, 720);
        X264 video = new();
        video.SetScale(1280);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1280x720");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);
        Assert.Contains("scale=1280:", command);
    }

    [Fact]
    public void SetScale_WidthAndHeight_ReflectedInCommand()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);
        Assert.Contains("scale=1920:1080", command);
    }

    [Fact]
    public void SetScale_FluentApi_ReturnsSelf()
    {
        X264 video = new();
        BaseVideo result = video.SetScale(1920, 1080);
        Assert.Same(video, result);
    }

    #endregion

    #region HDR Detection and Color Space

    [Fact]
    public void BuildCommand_UhdVideo_ContainsBt2020ColorPrimaries()
    {
        FfProbeData probe = CreateSdrProbeData(3840, 2160);
        X264 video = new();
        video.SetScale(3840, 2160);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_3840x2160");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-color_primaries bt2020", command);
        Assert.Contains("-colorspace bt2020nc", command);
    }

    [Fact]
    public void BuildCommand_10BitPixelFormat_SetsHdrColorTrc()
    {
        // Use HDR probe data so the 10-bit profile is not skipped by ShouldSkipHdrProfile
        FfProbeData probe = CreateHdrProbeData();
        X265 video = new();
        video.SetScale(3840, 2160);
        video.SetColorSpace(VideoPixelFormats.Yuv420P10Le);
        video.SetHlsPlaylistFilename("video_3840x2160");
        video.AllowHdr();
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-color_trc smpte2084", command);
    }

    #endregion

    #region GetFullCommand — Integration via VideoAudioFile

    [Fact]
    public void GetFullCommand_ReturnsNonEmptyString()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        VideoFile file = new(probe, "/usr/bin/ffmpeg");
        file.Container = hls;

        string command = file.GetFullCommand();

        Assert.False(string.IsNullOrWhiteSpace(command));
        Assert.Contains("-hide_banner", command);
        Assert.Contains("-c:v libx264", command);
    }

    #endregion

    #region Container Custom Arguments

    [Fact]
    public void BaseContainer_AddCustomArgument_KeyValue_AppearsInCommand()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);
        hls.AddCustomArgument("-custom_flag", "custom_value");

        string command = BuildCommand(hls, probe);

        Assert.Contains("-custom_flag custom_value", command);
    }

    [Fact]
    public void BaseVideo_AddCustomArgument_AppearsInCommand()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        video.AddCustomArgument("-custom_video_flag", "test");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-custom_video_flag", command);
    }

    #endregion

    #region Audio Metadata

    [Fact]
    public void BuildCommand_AudioStream_ContainsLanguageMetadata()
    {
        FfProbeData probe = CreateSdrProbeData();
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");
        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng_aac");
        Hls hls = CreateHlsContainer(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("-metadata:s:a:0", command);
        Assert.Contains("language=", command);
    }

    #endregion

    #region FrameSizes Constants

    [Fact]
    public void AvailableVideoSizes_ContainsExpectedResolutions()
    {
        Classes.VideoQualityDto[] sizes = BaseVideo.AvailableVideoSizes;

        Assert.Contains(sizes, s => s.Name == "240p");
        Assert.Contains(sizes, s => s.Name == "360p");
        Assert.Contains(sizes, s => s.Name == "480p");
        Assert.Contains(sizes, s => s.Name == "720p");
        Assert.Contains(sizes, s => s.Name == "1080p");
        Assert.Contains(sizes, s => s.Name == "4k");
    }

    #endregion
}
