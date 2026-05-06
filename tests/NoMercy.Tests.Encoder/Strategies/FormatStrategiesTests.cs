using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies;
using NoMercy.Encoder.Strategies.Dash;
using NoMercy.Encoder.Strategies.Mkv;
using NoMercy.Encoder.Strategies.Mp4;
using Container = NoMercy.Encoder.Profiles.V2.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.V2.EncodingProfile;

namespace NoMercy.Tests.Encoder.Strategies;

public class FormatStrategiesTests
{
    [Theory]
    [MemberData(nameof(StrategyExpectations))]
    public void Strategy_ExposesExpectedFormatAndMode(
        IEncodingStrategy strategy,
        OutputFormat expectedFormat,
        EncodeMode expectedMode
    )
    {
        Assert.Equal(expectedFormat, strategy.Format);
        Assert.Equal(expectedMode, strategy.EncodeMode);
    }

    [Theory]
    [MemberData(nameof(Strategies))]
    public async Task Strategy_DelegatesToInjectedEncoder(IEncodingStrategy strategy)
    {
        EncodingRequest request = FakeRequest();
        EncodingResult? result = await strategy.EncodeAsync(
            request,
            progress: null,
            ct: CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    public static TheoryData<IEncodingStrategy, OutputFormat, EncodeMode> StrategyExpectations()
    {
        IEncoder encoder = BuildMockEncoder();
        return new()
        {
            { new MkvStrategy(encoder), OutputFormat.Mkv, EncodeMode.SinglePass },
            { new Mp4SinglePassStrategy(encoder), OutputFormat.Mp4, EncodeMode.SinglePass },
            { new DashSinglePassStrategy(encoder), OutputFormat.Dash, EncodeMode.SinglePass },
        };
    }

    public static TheoryData<IEncodingStrategy> Strategies()
    {
        IEncoder encoder = BuildMockEncoder();
        return new()
        {
            new MkvStrategy(encoder),
            new Mp4SinglePassStrategy(encoder),
            new DashSinglePassStrategy(encoder),
        };
    }

    private static IEncoder BuildMockEncoder()
    {
        Mock<IEncoder> mock = new();
        mock.Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
                    Success: true,
                    OutputPath: "/out",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: new(0, 0, 0, "test", null)
                )
            );
        return mock.Object;
    }

    private static EncodingRequest FakeRequest() =>
        new(
            InputPath: "/media/test.mkv",
            OutputDirectory: "/out",
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "Test",
                Container: Container.HlsTs,
                Video: null,
                Audio: [],
                Subtitles: []
            )
        );
}
