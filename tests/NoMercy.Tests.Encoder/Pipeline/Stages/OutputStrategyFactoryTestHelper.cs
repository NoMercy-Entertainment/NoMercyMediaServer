namespace NoMercy.Tests.Encoder.Pipeline.Stages;

using NoMercy.Encoder.Output;
using NoMercy.Tests.Encoder.Storage;

internal static class OutputStrategyFactoryTestHelper
{
    public static OutputStrategyFactory Create() =>
        new([
            new HlsOutputStrategy(TestStorageFactory.CreateLocal()),
            new MkvOutputStrategy(TestStorageFactory.CreateLocal()),
            new Mp4OutputStrategy(TestStorageFactory.CreateLocal()),
            new DashOutputStrategy(TestStorageFactory.CreateLocal()),
            new Mp3OutputStrategy(TestStorageFactory.CreateLocal()),
            new FlacOutputStrategy(TestStorageFactory.CreateLocal()),
            new OggOutputStrategy(TestStorageFactory.CreateLocal()),
        ]);
}
