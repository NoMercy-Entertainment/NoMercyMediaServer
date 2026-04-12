namespace NoMercy.Tests.Encoder.V3.Jobs;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Jobs;
using NoMercy.Encoder.V3.Pipeline;
using NoMercy.Encoder.V3.Profiles;

public class BatchEncodingRequestTests
{
    private static EncodingProfile BuildProfile() =>
        new(
            Id: "hls-1080p",
            Name: "HLS 1080p",
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: []
        );

    [Fact]
    public void BatchEncodingRequest_WithThreeFiles_IsConstructable()
    {
        string[] inputPaths = ["/media/a.mkv", "/media/b.mkv", "/media/c.mkv"];

        BatchEncodingRequest request = new(
            InputPaths: inputPaths,
            OutputDirectory: "/output/batch",
            Profile: BuildProfile()
        );

        request.InputPaths.Should().HaveCount(3);
        request.InputPaths.Should().Contain("/media/a.mkv");
        request.InputPaths.Should().Contain("/media/b.mkv");
        request.InputPaths.Should().Contain("/media/c.mkv");
        request.OutputDirectory.Should().Be("/output/batch");
        request.Options.Should().BeNull();
    }

    [Fact]
    public void BatchEncodingRequest_WithOptions_PreservesOptions()
    {
        EncodingOptions options = new(
            ResumeFromCheckpoint: true,
            MaxConcurrentEncodes: 2,
            Priority: Priority.High
        );

        BatchEncodingRequest request = new(
            InputPaths: ["/media/a.mkv"],
            OutputDirectory: "/output",
            Profile: BuildProfile(),
            Options: options
        );

        request.Options.Should().NotBeNull();
        request.Options!.ResumeFromCheckpoint.Should().BeTrue();
        request.Options.MaxConcurrentEncodes.Should().Be(2);
        request.Options.Priority.Should().Be(Priority.High);
    }

    [Fact]
    public void BatchEncodingRequest_WithEmptyPaths_IsConstructable()
    {
        // Empty paths array is constructable — caller validation is responsibility of the consumer
        BatchEncodingRequest request = new(
            InputPaths: [],
            OutputDirectory: "/output",
            Profile: BuildProfile()
        );

        request.InputPaths.Should().BeEmpty();
    }

    [Fact]
    public void BatchEncodingRequest_EmptyPaths_ShouldBeRejected_ByConsumer()
    {
        // Demonstrate that a caller validating for empty paths can detect it
        BatchEncodingRequest request = new(
            InputPaths: [],
            OutputDirectory: "/output",
            Profile: BuildProfile()
        );

        bool isRejected = request.InputPaths.Length == 0;
        isRejected.Should().BeTrue();
    }
}
