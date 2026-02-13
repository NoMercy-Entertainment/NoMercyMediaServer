namespace NoMercy.Providers.Helpers;

public static class HttpClientProvider
{
    private static IHttpClientFactory? _factory;

    public static void Initialize(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    internal static void Reset()
    {
        _factory = null;
    }

    public static HttpClient CreateClient(string name)
    {
        if (_factory is not null)
        {
            try
            {
                return _factory.CreateClient(name);
            }
            catch (ObjectDisposedException)
            {
                _factory = null;
            }
        }

        return new();
    }
}
