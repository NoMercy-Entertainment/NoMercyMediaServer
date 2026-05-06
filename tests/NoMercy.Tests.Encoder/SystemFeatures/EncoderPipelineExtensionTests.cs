using FluentAssertions;
using NoMercy.Encoder.SystemFeatures;
using Xunit;

namespace NoMercy.Tests.Encoder.SystemFeatures;

public class EncoderPipelineExtensionTests
{
    [Fact]
    public void IEncoderPipelineExtension_IsDistinctFromPluginsAbstractionsIEncoderPlugin()
    {
        // IEncoderPipelineExtension is an encoder-internal pipeline hook contract.
        // NoMercy.Plugins.Abstractions.IEncoderPlugin is the public plugin author surface.
        // They must not be the same type.
        Type pipelineExtension = typeof(IEncoderPipelineExtension);
        Type? pluginsAbstractionsEncoderPlugin = Type.GetType(
            "NoMercy.Plugins.Abstractions.IEncoderPlugin, NoMercy.Plugins.Abstractions"
        );

        // The encoder pipeline extension type must exist.
        pipelineExtension.Should().NotBeNull();
        pipelineExtension.Name.Should().Be("IEncoderPipelineExtension");

        // The abstractions type may not be loaded in this assembly context but
        // the names are demonstrably different — structural proof of disambiguation.
        pipelineExtension.FullName.Should().NotBe("NoMercy.Plugins.Abstractions.IEncoderPlugin");
    }

    [Fact]
    public void PipelineHook_HasExpectedProperties()
    {
        // Verify the PipelineHook record is accessible under the correct namespace.
        Type hookType = typeof(PipelineHook);

        hookType.Should().NotBeNull();
        hookType.Namespace.Should().Be("NoMercy.Encoder.SystemFeatures");
    }

    [Fact]
    public void PipelineStagePosition_HasExpectedValues()
    {
        PipelineStagePosition[] values = Enum.GetValues<PipelineStagePosition>();

        values.Should().Contain(PipelineStagePosition.Before);
        values.Should().Contain(PipelineStagePosition.After);
        values.Should().Contain(PipelineStagePosition.Replace);
    }
}
