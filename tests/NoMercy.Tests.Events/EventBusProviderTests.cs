using FluentAssertions;
using NoMercy.Events;
using Xunit;

namespace NoMercy.Tests.Events;

public class EventBusProviderTests
{
    [Fact]
    public void Configure_SetsInstance()
    {
        InMemoryEventBus bus = new();

        EventBusProvider.Configure(bus);

        EventBusProvider.IsConfigured.Should().BeTrue();
        EventBusProvider.Current.Should().BeSameAs(bus);
    }

    [Fact]
    public void Configure_NullArg_ThrowsArgumentNullException()
    {
        Action act = () => EventBusProvider.Configure(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
