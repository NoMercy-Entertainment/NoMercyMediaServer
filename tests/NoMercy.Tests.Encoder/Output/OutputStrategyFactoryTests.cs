using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Output;

public class OutputStrategyFactoryTests
{
    [Theory]
    [InlineData(OutputFormat.Hls, typeof(HlsOutputStrategy))]
    [InlineData(OutputFormat.Mkv, typeof(MkvOutputStrategy))]
    [InlineData(OutputFormat.Mp4, typeof(Mp4OutputStrategy))]
    [InlineData(OutputFormat.Dash, typeof(DashOutputStrategy))]
    [InlineData(OutputFormat.Mp3, typeof(Mp3OutputStrategy))]
    [InlineData(OutputFormat.Flac, typeof(FlacOutputStrategy))]
    [InlineData(OutputFormat.Ogg, typeof(OggOutputStrategy))]
    public void Resolve_BuiltInFormat_ReturnsMatchingStrategy(OutputFormat format, Type expected)
    {
        OutputStrategyFactory factory = new([
            new HlsOutputStrategy(TestStorageFactory.CreateLocal()),
            new MkvOutputStrategy(TestStorageFactory.CreateLocal()),
            new Mp4OutputStrategy(TestStorageFactory.CreateLocal()),
            new DashOutputStrategy(TestStorageFactory.CreateLocal()),
            new Mp3OutputStrategy(TestStorageFactory.CreateLocal()),
            new FlacOutputStrategy(TestStorageFactory.CreateLocal()),
            new OggOutputStrategy(TestStorageFactory.CreateLocal()),
        ]);

        IOutputStrategy resolved = factory.Resolve(format);

        resolved.Should().BeOfType(expected);
    }

    [Fact]
    public void Resolve_UnknownFormat_Throws()
    {
        OutputStrategyFactory factory = new([
            new HlsOutputStrategy(TestStorageFactory.CreateLocal()),
        ]);

        Action act = () => factory.Resolve((OutputFormat)99);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Resolve_PluginOverride_TakesPrecedenceOverBuiltIn()
    {
        // Last-registration-wins: plugin HLS strategy registered after built-in
        // should be preferred by the factory.
        FakeHlsStrategy pluginOverride = new();
        OutputStrategyFactory factory = new([
            new HlsOutputStrategy(TestStorageFactory.CreateLocal()),
            pluginOverride,
        ]);

        IOutputStrategy resolved = factory.Resolve(OutputFormat.Hls);

        resolved.Should().BeSameAs(pluginOverride);
    }

    private sealed class FakeHlsStrategy : IOutputStrategy
    {
        public OutputFormat Format => OutputFormat.Hls;

        public void ConfigureOutput(
            FfmpegCommandBuilder builder,
            OutputPlan plan,
            string outputDirectory
        ) { }

        public Task FinalizeAsync(
            string outputDirectory,
            OutputPlan plan,
            string mediaTitle,
            CancellationToken ct
        ) => Task.CompletedTask;

        public string[] GetOutputSubdirectories(OutputPlan plan) => [];
    }
}
