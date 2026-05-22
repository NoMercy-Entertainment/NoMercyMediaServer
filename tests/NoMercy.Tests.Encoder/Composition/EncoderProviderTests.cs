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

    private static void ResetServiceResolver()
    {
        FieldInfo? field = typeof(EncoderProvider).GetField(
            "_serviceResolver",
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

    // ── Service resolver surface ────────────────────────────────────────────

    [Fact]
    public void ResolveService_BeforeConfigure_ReturnsNull()
    {
        // Before any ConfigureServiceResolver call (server didn't wire DI),
        // ResolveService MUST return null instead of throwing — consumers
        // are expected to handle "service not available" gracefully.
        ResetServiceResolver();

        IEncoder? resolved = EncoderProvider.ResolveService<IEncoder>();

        resolved.Should().BeNull();
    }

    [Fact]
    public void ResolveService_ResolverReturnsInstance_ReturnsTypedInstance()
    {
        Mock<IEncoder> encoder = new();
        EncoderProvider.ConfigureServiceResolver(type =>
            type == typeof(IEncoder) ? encoder.Object : null
        );

        IEncoder? resolved = EncoderProvider.ResolveService<IEncoder>();

        resolved.Should().BeSameAs(encoder.Object);
    }

    [Fact]
    public void ResolveService_ResolverReturnsNull_ReturnsNull()
    {
        // DI container is wired but the asked-for service isn't registered.
        // Must return null (no throw, no exception type leak).
        EncoderProvider.ConfigureServiceResolver(_ => null);

        IEncoder? resolved = EncoderProvider.ResolveService<IEncoder>();

        resolved.Should().BeNull();
    }

    [Fact]
    public void ResolveService_ResolverReturnsWrongType_ReturnsNull()
    {
        // Defensive: if the underlying resolver returns the wrong runtime
        // type (e.g. a misconfigured fake), the `as T` cast yields null
        // rather than throwing InvalidCastException at the call site.
        EncoderProvider.ConfigureServiceResolver(_ => "not an IEncoder");

        IEncoder? resolved = EncoderProvider.ResolveService<IEncoder>();

        resolved.Should().BeNull();
    }

    [Fact]
    public void ConfigureServiceResolver_WithNull_ThrowsArgumentNull()
    {
        Action act = () => EncoderProvider.ConfigureServiceResolver(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("resolver");
    }
}
