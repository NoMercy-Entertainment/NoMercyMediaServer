namespace NoMercy.Tests.Encoder.Composition;

using Moq;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Pipeline;

[Collection("EncoderProvider")]
public class EncoderProviderTests
{
    [Fact]
    public void Resolve_WhenConfigured_ReturnsEncoderFromFactory()
    {
        Mock<IEncoder> mockEncoder = new();
        EncoderProvider.Configure(() => mockEncoder.Object);

        IEncoder result = EncoderProvider.Resolve();

        result.Should().BeSameAs(mockEncoder.Object);

        // Reset: leave factory pointing at a dummy to avoid poisoning later tests
        EncoderProvider.Configure(() => new Mock<IEncoder>().Object);
    }

    [Fact]
    public void Configure_WithNull_ThrowsArgumentNull()
    {
        Action act = () => EncoderProvider.Configure(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("factory");

        // Reset
        EncoderProvider.Configure(() => new Mock<IEncoder>().Object);
    }

    [Fact]
    public void IsConfigured_AfterConfigure_ReturnsTrue()
    {
        EncoderProvider.Configure(() => new Mock<IEncoder>().Object);

        EncoderProvider.IsConfigured.Should().BeTrue();

        // Reset
        EncoderProvider.Configure(() => new Mock<IEncoder>().Object);
    }
}
