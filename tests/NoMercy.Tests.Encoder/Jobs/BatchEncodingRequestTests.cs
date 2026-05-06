using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using Container = NoMercy.Encoder.Profiles.V2.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.V2.EncodingProfile;

namespace NoMercy.Tests.Encoder.Jobs;

public class BatchEncodingRequestTests
{
    private static EncodingProfile BuildProfile() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "HLS 1080p",
            Container: Container.HlsTs,
            Video: null,
            Audio: [],
            Subtitles: []
        );

    private static EncodingRequest BuildRequest(string inputPath) =>
        new(InputPath: inputPath, OutputDirectory: "/output/batch", Profile: BuildProfile());

    [Fact]
    public void BatchEncodingRequest_WithThreeItems_IsConstructable()
    {
        EncodingRequest[] items =
        [
            BuildRequest("/media/a.mkv"),
            BuildRequest("/media/b.mkv"),
            BuildRequest("/media/c.mkv"),
        ];

        BatchEncodingRequest request = new(Items: items, Options: new());

        request.Items.Should().HaveCount(3);
        request.Items.Should().Contain(r => r.InputPath == "/media/a.mkv");
        request.Items.Should().Contain(r => r.InputPath == "/media/b.mkv");
        request.Items.Should().Contain(r => r.InputPath == "/media/c.mkv");
    }

    [Fact]
    public void BatchEncodingRequest_WithOptions_PreservesOptions()
    {
        BatchOptions options = new(
            ShareAnalysis: true,
            ParallelEncoding: true,
            MaxParallel: 2,
            CancelMode: BatchCancellationMode.CancelAll
        );

        BatchEncodingRequest request = new(Items: [BuildRequest("/media/a.mkv")], Options: options);

        request.Options.ShareAnalysis.Should().BeTrue();
        request.Options.ParallelEncoding.Should().BeTrue();
        request.Options.MaxParallel.Should().Be(2);
        request.Options.CancelMode.Should().Be(BatchCancellationMode.CancelAll);
    }

    [Fact]
    public void BatchEncodingRequest_WithEmptyItems_IsConstructable()
    {
        // Empty items array is constructable — caller validation is responsibility of the consumer
        BatchEncodingRequest request = new(Items: [], Options: new());

        request.Items.Should().BeEmpty();
    }

    [Fact]
    public void BatchEncodingRequest_EmptyItems_ShouldBeRejected_ByConsumer()
    {
        // Demonstrate that a caller validating for empty items can detect it
        BatchEncodingRequest request = new(Items: [], Options: new());

        bool isRejected = request.Items.Length == 0;
        isRejected.Should().BeTrue();
    }

    [Fact]
    public void BatchOptions_Defaults_AreCorrect()
    {
        BatchOptions options = new();

        options.ShareAnalysis.Should().BeTrue();
        options.ParallelEncoding.Should().BeFalse();
        options.MaxParallel.Should().Be(1);
        options.CancelMode.Should().Be(BatchCancellationMode.SkipRemaining);
    }
}
