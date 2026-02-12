using Microsoft.AspNetCore.WebUtilities;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using NoMercy.Setup;
using Serilog.Events;

namespace NoMercy.Providers.MusixMatch.Client;

public class MusixMatchBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("https://apic-desktop.musixmatch.com/ws/1.1/");

    private readonly HttpClient _client;

    protected MusixMatchBaseClient()
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.MusixMatch);
        _client.BaseAddress ??= _baseUrl;
    }

    protected MusixMatchBaseClient(Guid id)
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.MusixMatch);
        _client.BaseAddress ??= _baseUrl;
        Id = id;
    }

    private static Helpers.Queue? _queue;

    private static Helpers.Queue GetQueue()
    {
        return _queue ??= new(new() { Concurrent = 2, Interval = 1000, Start = true });
    }

    protected Guid Id { get; private set; }

    protected async Task<T?> Get<T>(string url, Dictionary<string, string?> query, bool? priority = false)
        where T : class
    {
        query.Add("format", "json");
        query.Add("namespace", "lyrics_richsynched");
        query.Add("subtitle_format", "mxm");
        query.Add("app_id", "web-desktop-app-v1.0");
        query.Add("usertoken", ApiInfo.MusixmatchKey);

        string newUrl = QueryHelpers.AddQueryString(url, query);

        if (CacheController.Read(newUrl, out T? result)) return result;

        Logger.MusixMatch(_baseUrl + newUrl, LogEventLevel.Verbose);

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