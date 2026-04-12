namespace NoMercy.Tests.Encoder.V3.Commands;

using NoMercy.Encoder.V3.Commands;

public class FfmpegCommandBuilderTests
{
    [Fact]
    public void SimpleH264Encode_ProducesCorrectArgs()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new InputOptions("/input/video.mkv"))
            .AddOutput(
                new OutputOptions(
                    FilePath: "/output/video.mp4",
                    VideoCodec: "libx264",
                    AudioCodec: "aac",
                    Crf: 23,
                    Preset: "medium"
                )
            )
            .Build("ffmpeg");

        cmd.Arguments.Should().Contain("-y");
        cmd.Arguments.Should().Contain("-hide_banner");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-i", "/input/video.mkv");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-c:v", "libx264");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-c:a", "aac");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-crf", "23");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-preset", "medium");
        cmd.Arguments.Should().Contain("/output/video.mp4");
    }

    [Fact]
    public void HwAccelInput_IncludesHwaccelFlags()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(
                new InputOptions(
                    "/input/video.mkv",
                    HwAccelDevice: "cuda",
                    HwAccelOutputFormat: "cuda"
                )
            )
            .AddOutput(new OutputOptions(FilePath: "/output/video.mp4", VideoCodec: "h264_nvenc"))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-hwaccel", "cuda");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-hwaccel_output_format", "cuda");
    }

    [Fact]
    public void FilterComplex_IncludedInArgs()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new InputOptions("/input.mkv"))
            .WithFilterComplex("[0:v]scale=1920:1080[v0]")
            .AddOutput(
                new OutputOptions(
                    FilePath: "/output.mp4",
                    VideoCodec: "libx264",
                    MapStreams: ["[v0]"]
                )
            )
            .Build("ffmpeg");

        cmd.Arguments.Should()
            .ContainInConsecutiveOrder("-filter_complex", "[0:v]scale=1920:1080[v0]");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-map", "[v0]");
    }

    [Fact]
    public void MultipleOutputs_AllIncluded()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new InputOptions("/input.mkv"))
            .AddOutput(new OutputOptions(FilePath: "/out1.mp4", VideoCodec: "libx264"))
            .AddOutput(new OutputOptions(FilePath: "/out2.mp4", VideoCodec: "libx265"))
            .Build("ffmpeg");

        cmd.Arguments.Should().Contain("/out1.mp4");
        cmd.Arguments.Should().Contain("/out2.mp4");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-c:v", "libx264");
    }

    [Fact]
    public void SeekAndDuration_FormattedCorrectly()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(
                new InputOptions(
                    "/input.mkv",
                    SeekTo: TimeSpan.FromSeconds(30.5),
                    Duration: TimeSpan.FromSeconds(10)
                )
            )
            .AddOutput(new OutputOptions(FilePath: "/output.mp4"))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-ss", "30.500");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-t", "10.000");
    }

    [Fact]
    public void GlobalOptions_ThreadsAndProbesize()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new GlobalOptions(Threads: 4, ProbeSizeBytes: 5000000))
            .AddInput(new InputOptions("/input.mkv"))
            .AddOutput(new OutputOptions(FilePath: "/output.mp4"))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-threads", "4");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-probesize", "5000000");
    }

    [Fact]
    public void ExtraFlags_Included()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new InputOptions("/input.mkv"))
            .AddOutput(
                new OutputOptions(
                    FilePath: "/output.mp4",
                    VideoCodec: "hevc_videotoolbox",
                    ExtraFlags: new Dictionary<string, string> { ["-tag:v"] = "hvc1" }
                )
            )
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-tag:v", "hvc1");
    }

    [Fact]
    public void NoInputs_BuildsEmptyCommand()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder().Build("ffmpeg");

        cmd.Executable.Should().Be("ffmpeg");
        cmd.Arguments.Should().Contain("-y");
    }

    [Fact]
    public void ProgressPipe_EnabledByDefault()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new InputOptions("/input.mkv"))
            .AddOutput(new OutputOptions(FilePath: "/output.mp4"))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-progress", "pipe:1");
    }
}
