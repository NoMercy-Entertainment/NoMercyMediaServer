using FluentAssertions;
using NoMercy.Events;
using Xunit;

namespace NoMercy.Tests.Events;

public class InterfaceContractTests
{
    private sealed class TestEvent : EventBase
    {
        public override string Source => "InterfaceTest";
    }

    private sealed class TestEventHandler : IEventHandler<TestEvent>
    {
        public List<TestEvent> ReceivedEvents { get; } = [];

        public Task HandleAsync(TestEvent @event, CancellationToken ct = default)
        {
            ReceivedEvents.Add(@event);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void IEvent_HasRequiredProperties()
    {
        Type eventType = typeof(IEvent);

        eventType.GetProperty("EventId").Should().NotBeNull();
        eventType.GetProperty("Timestamp").Should().NotBeNull();
        eventType.GetProperty("Source").Should().NotBeNull();
    }

    [Fact]
    public void IEventBus_HasPublishMethod()
    {
        Type busType = typeof(IEventBus);

        busType.GetMethod("PublishAsync").Should().NotBeNull();
    }

    [Fact]
    public void IEventBus_HasSubscribeWithDelegateMethod()
    {
        Type busType = typeof(IEventBus);
        System.Reflection.MethodInfo[] subscribeMethods = busType
            .GetMethods()
            .Where(m => m.Name == "Subscribe")
            .ToArray();

        subscribeMethods.Should().HaveCount(2,
            "IEventBus should have two Subscribe overloads: delegate and IEventHandler");
    }

    [Fact]
    public async Task IEventHandler_CanBeImplemented()
    {
        TestEventHandler handler = new();
        TestEvent testEvent = new();

        await handler.HandleAsync(testEvent);

        handler.ReceivedEvents.Should().ContainSingle()
            .Which.Should().BeSameAs(testEvent);
    }

    [Fact]
    public void IEventHandler_IsContravariant()
    {
        Type handlerType = typeof(IEventHandler<>);
        Type genericParam = handlerType.GetGenericArguments()[0];

        genericParam.GenericParameterAttributes
            .HasFlag(System.Reflection.GenericParameterAttributes.Contravariant)
            .Should().BeTrue("IEventHandler<TEvent> should be contravariant (in TEvent)");
    }

    [Fact]
    public void IEventBus_SubscribeReturnsDisposable()
    {
        Type busType = typeof(IEventBus);
        System.Reflection.MethodInfo[] subscribeMethods = busType
            .GetMethods()
            .Where(m => m.Name == "Subscribe")
            .ToArray();

        foreach (System.Reflection.MethodInfo method in subscribeMethods)
        {
            method.ReturnType.Should().Be(typeof(IDisposable),
                "Subscribe should return IDisposable for unsubscription");
        }
    }
}
