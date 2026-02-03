using NoMercy.EncoderV2.Abstractions;
using NoMercy.EncoderV2.Codecs.Audio;
using NoMercy.EncoderV2.Codecs.Video;
using NoMercy.EncoderV2.Commands;
using NoMercy.EncoderV2.Containers;
using NoMercy.EncoderV2.Profiles;

namespace NoMercy.Tests.EncoderV2.Commands;

public class CommandBuilderTests
{
    [Fact]
    public void FFmpegCommandBuilder_BasicCommand_BuildsCorrectly()
    {
        string command = FFmpegCommandBuilder.Create()
            .WithInput("/path/to/input.mp4")
            .WithOutput("/path/to/output.mp4")
            .Build();

        Assert.Contains("-i \"/path/to/input.mp4\"", command);
        Assert.Contains("\"/path/to/output.mp4\"", command);
    }

    [Fact]
    public void FFmpegCommandBuilder_WithOverwrite_IncludesYFlag()
    {
        string command = FFmpegCommandBuilder.Create()
            .WithInput("/input.mp4")
            .WithOutput("/output.mp4")
            .WithOverwrite()
            .Build();

        Assert.Contains("-y", command);
    }

    [Fact]
    public void FFmpegCommandBuilder_WithThreads_IncludesThreadsOption()
    {
        string command = FFmpegCommandBuilder.Create()
            .WithInput("/input.mp4")
            .WithOutput("/output.mp4")
            .WithThreads(4)
            .Build();

        Assert.Contains("-threads", command);
        Assert.Contains("4", command);
    }

    [Fact]
    public void FFmpegCommandBuilder_WithGlobalOptions_PlacesBeforeInput()
    {
        string command = FFmpegCommandBuilder.Create()
            .AddGlobalOptions("-hide_banner", "-loglevel", "error")
            .WithInput("/input.mp4")
            .WithOutput("/output.mp4")
            .Build();

        int globalPos = command.IndexOf("-hide_banner");
        int inputPos = command.IndexOf("-i");

        Assert.True(globalPos < inputPos);
    }

    [Fact]
    public void FFmpegCommandBuilder_WithInputOptions_PlacesBeforeInput()
    {
        string command = FFmpegCommandBuilder.Create()
            .AddInputOptions("-ss", "00:01:00")
            .WithInput("/input.mp4")
            .WithOutput("/output.mp4")
            .Build();

        int optionPos = command.IndexOf("-ss");
        int inputPos = command.IndexOf("-i");

        Assert.True(optionPos < inputPos);
    }

    [Fact]
    public void FFmpegCommandBuilder_WithOutputOptions_PlacesAfterInput()
    {
        string command = FFmpegCommandBuilder.Create()
            .WithInput("/input.mp4")
            .WithOutput("/output.mp4")
            .AddOutputOptions("-c:v", "libx264")
            .Build();

        int inputPos = command.IndexOf("-i");
        int optionPos = command.IndexOf("-c:v");

        Assert.True(optionPos > inputPos);
    }

    [Fact]
    public void FFmpegCommandBuilder_WithVideoFilter_IncludesVfOption()
    {
        string command = FFmpegCommandBuilder.Create()
            .WithInput("/input.mp4")
            .WithOutput("/output.mp4")
            .AddVideoFilter("scale=1920:1080")
            .Build();

        Assert.Contains("-vf", command);
        Assert.Contains("scale=1920:1080", command);
    }

    [Fact]
    public void FFmpegCommandBuilder_MultipleVideoFilters_CombinesWithComma()
    {
        string command = FFmpegCommandBuilder.Create()
            .WithInput("/input.mp4")
            .WithOutput("/output.mp4")
            .AddVideoFilter("scale=1920:1080")
            .AddVideoFilter("format=yuv420p")
            .Build();

        Assert.Contains("-vf", command);
        Assert.Contains("scale=1920:1080,format=yuv420p", command);
    }

    [Fact]
    public void FFmpegCommandBuilder_WithComplexFilter_IncludesFilterComplex()
    {
        string command = FFmpegCommandBuilder.Create()
            .WithInput("/input.mp4")
            .WithOutput("/output.mp4")
            .AddComplexFilter("[0:v]split=2[v1][v2]")
            .Build();

        Assert.Contains("-filter_complex", command);
        Assert.Contains("[0:v]split=2[v1][v2]", command);
    }

    [Fact]
    public void FFmpegCommandBuilder_WithHardwareAcceleration_IncludesHwaccelOptions()
    {
        string command = FFmpegCommandBuilder.Create()
            .WithHardwareAcceleration(HardwareAcceleration.Nvenc)
            .WithInput("/input.mp4")
            .WithOutput("/output.mp4")
            .Build();

        Assert.Contains("-hwaccel", command);
        Assert.Contains("cuda", command);
    }

    [Fact]
    public void FFmpegCommandBuilder_WithoutInput_ThrowsException()
    {
        FFmpegCommandBuilder builder = FFmpegCommandBuilder.Create()
            .WithOutput("/output.mp4");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void FFmpegCommandBuilder_WithoutOutput_ThrowsException()
    {
        FFmpegCommandBuilder builder = FFmpegCommandBuilder.Create()
            .WithInput("/input.mp4");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void FFmpegCommandBuilder_FromProfile_BuildsCompleteCommand()
    {
        EncodingProfile profile = new()
        {
            Id = "test",
            Name = "Test",
            Container = new Mp4Container { FastStart = true },
            VideoOutputs =
            [
                new VideoOutputConfig
                {
                    Id = "main",
                    Codec = new H264Codec { Preset = "medium", Crf = 23 },
                    Width = 1920,
                    Height = 1080
                }
            ],
            AudioOutputs =
            [
                new AudioOutputConfig
                {
                    Id = "stereo",
                    Codec = new AacCodec { Bitrate = 192, Channels = 2 }
                }
            ],
            Options = new EncodingOptions { OverwriteOutput = true }
        };

        FFmpegCommandBuilder builder = FFmpegCommandBuilder.FromProfile(
            "/input.mkv",
            "/output.mp4",
            profile);

        string command = builder.Build();

        Assert.Contains("-i \"/input.mkv\"", command);
        Assert.Contains("-c:v", command);
        Assert.Contains("libx264", command);
        Assert.Contains("-preset", command);
        Assert.Contains("medium", command);
        Assert.Contains("-crf", command);
        Assert.Contains("23", command);
        Assert.Contains("-c:a", command);
        Assert.Contains("aac", command);
        Assert.Contains("-b:a", command);
        Assert.Contains("192k", command);
        Assert.Contains("-y", command);
        Assert.Contains("\"/output.mp4\"", command);
    }

    [Fact]
    public void FFmpegCommandBuilder_FromProfile_HlsContainer_IncludesBitstreamFilter()
    {
        EncodingProfile profile = new()
        {
            Id = "test",
            Name = "Test",
            Container = new HlsContainer { SegmentDuration = 4 },
            VideoOutputs =
            [
                new VideoOutputConfig
                {
                    Id = "main",
                    Codec = new H264Codec { Preset = "medium", Crf = 23 }
                }
            ],
            AudioOutputs =
            [
                new AudioOutputConfig
                {
                    Id = "stereo",
                    Codec = new AacCodec { Bitrate = 128 }
                }
            ],
            Options = new EncodingOptions()
        };

        FFmpegCommandBuilder builder = FFmpegCommandBuilder.FromProfile(
            "/input.mkv",
            "/output/playlist.m3u8",
            profile);

        string command = builder.Build();

        Assert.Contains("-bsf:v", command);
        Assert.Contains("h264_mp4toannexb", command);
        Assert.Contains("-hls_time", command);
    }

    #region FilterGraphBuilder Tests

    [Fact]
    public void FilterGraphBuilder_AddFilter_BuildsCorrectGraph()
    {
        FilterGraphBuilder builder = new();

        builder.AddInput(0, out string input);
        builder.AddFilter(input, "scale=1920:1080", out string scaled);

        string graph = builder.Build();

        Assert.Contains("scale=1920:1080", graph);
    }

    [Fact]
    public void FilterGraphBuilder_AddSplit_BuildsCorrectGraph()
    {
        FilterGraphBuilder builder = new();

        builder.AddInput(0, out string input);
        builder.AddSplit(input, 2, out string[] outputs);

        Assert.Equal(2, outputs.Length);
    }

    #endregion
}
