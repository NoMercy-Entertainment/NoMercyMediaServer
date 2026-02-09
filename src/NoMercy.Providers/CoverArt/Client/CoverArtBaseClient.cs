using Microsoft.AspNetCore.WebUtilities;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using Serilog.Events;

namespace NoMercy.Providers.CoverArt.Client;

public class CoverArtBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("https://coverartarchive.org/");

    private readonly HttpClient _client;

    protected CoverArtBaseClient()
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.CoverArt);
        _client.BaseAddress ??= _baseUrl;
    }

    protected CoverArtBaseClient(Guid id)
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.CoverArt);
        _client.BaseAddress ??= _baseUrl;
        Id = id;
    }

    private static Helpers.Queue? _queue;

    private static Helpers.Queue GetQueue()
    {
        return _queue ??= new(new() { Concurrent = 3, Interval = 1000, Start = true });
    }

    protected Guid Id { get; private set; }

    protected async Task<T?> Get<T>(string url, Dictionary<string, string>? query = null, bool? priority = false)
        where T : class
    {
        query ??= new();

        string newUrl = QueryHelpers.AddQueryString(url, query!);

        if (CacheController.Read(newUrl, out T? result)) return result;

        Logger.CoverArt(_baseUrl + newUrl, LogEventLevel.Verbose);

        string response = await GetQueue().Enqueue(() => _client.GetStringAsync(newUrl), newUrl, priority);

        await CacheController.Write(newUrl, response);

        T? data = response.FromJson<T>();

        return data;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}