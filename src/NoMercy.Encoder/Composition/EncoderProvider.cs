namespace NoMercy.Encoder.Composition;

using NoMercy.Encoder.Pipeline;

public static class EncoderProvider
{
    private static volatile Func<IEncoder>? _factory;
    private static volatile Func<Type, object?>? _serviceResolver;

    public static bool IsConfigured => _factory is not null;

    public static IEncoder Resolve() =>
        _factory?.Invoke()
        ?? throw new InvalidOperationException(
            "Encoder has not been configured. Call EncoderProvider.Configure() during startup."
        );

    /// <summary>
    /// Resolve an arbitrary encoder-owned service (OCR engine, crop detector,
    /// Whisper transcriber, media analyzer, …) via the DI container that was
    /// wired in at startup. Returns <c>null</c> when the service is unregistered.
    /// </summary>
    public static T? ResolveService<T>()
        where T : class
    {
        if (_serviceResolver is null)
            return null;
        return _serviceResolver(typeof(T)) as T;
    }

    public static void Configure(Func<IEncoder> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public static void ConfigureServiceResolver(Func<Type, object?> resolver)
    {
        _serviceResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }
}
