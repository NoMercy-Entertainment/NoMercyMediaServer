namespace NoMercy.Encoder.Composition;

using NoMercy.Encoder.Pipeline;

public static class EncoderProvider
{
    private static volatile Func<IEncoder>? _factory;

    public static bool IsConfigured => _factory is not null;

    public static IEncoder Resolve() =>
        _factory?.Invoke()
        ?? throw new InvalidOperationException(
            "Encoder has not been configured. Call EncoderProvider.Configure() during startup."
        );

    public static void Configure(Func<IEncoder> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }
}
