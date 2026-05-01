using System.Reflection;
using Moq;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Pipeline;

namespace NoMercy.Tests.Encoder.Composition;

/// <summary>
/// Tests for EncoderProvider lifecycle: configure, resolve, guard checks.
///
/// EncoderProvider uses static volatile fields. Each test resets the private
/// _factory field via reflection so execution order does not matter.
/// There is no public Reset() API — reflection is intentional here.
/// </summary>
[Collection("EncoderProvider")]
public class EncoderProviderTests
{
    private static void ResetFactory()
    {
        FieldInfo? field = typeof(EncoderProvider).GetField(
            "_factory",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        field?.SetValue(null, null);
    }

    [Fact]
    public void Resolve_WhenNotConfigured_ThrowsInvalidOperation()
    {
        ResetFactory();

        Action act = () => EncoderProvider.Resolve();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*EncoderProvider.Configure()*");
    }

    [Fact]
    public void Resolve_WhenConfigured_ReturnsEncoderFromFactory()
    {
        Mock<IEncoder> mockEncoder = new();
        EncoderProvider.Configure(() => mockEncoder.Object);

        IEncoder result = EncoderProvider.Resolve();

        result.Should().BeSameAs(mockEncoder.Object);
    }

    [Fact]
    public void IsConfigured_BeforeConfigure_ReturnsFalse()
    {
        ResetFactory();

        bool configured = EncoderProvider.IsConfigured;

        configured.Should().BeFalse();
    }

    [Fact]
    public void Configure_WithNull_ThrowsArgumentNull()
    {
        Action act = () => EncoderProvider.Configure(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("factory");
    }
}
