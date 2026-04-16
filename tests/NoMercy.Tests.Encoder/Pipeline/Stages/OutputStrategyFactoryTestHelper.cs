namespace NoMercy.Tests.Encoder.Pipeline.Stages;

using NoMercy.Encoder.Output;

internal static class OutputStrategyFactoryTestHelper
{
    public static OutputStrategyFactory Create() =>
        new([
            new HlsOutputStrategy(),
            new MkvOutputStrategy(),
            new Mp4OutputStrategy(),
            new DashOutputStrategy(),
            new Mp3OutputStrategy(),
            new FlacOutputStrategy(),
            new OggOutputStrategy(),
        ]);
}
