namespace NoMercy.Events;

public static class EventBusProvider
{
    private static IEventBus? _instance;

    public static IEventBus Current =>
        _instance ?? throw new InvalidOperationException(
            "EventBus has not been configured. Call EventBusProvider.Configure() during startup.");

    public static bool IsConfigured => _instance is not null;

    public static void Configure(IEventBus eventBus)
    {
        _instance = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }
}
