using Microsoft.AspNetCore.WebUtilities;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Providers.Helpers;
using Serilog.Events;

namespace NoMercy.Providers.MusixMatch.Client;

public class MusixMatchBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("https://apic-desktop.musixmatch.com/ws/1.1/");

    private readonly HttpClient _client = new();

    protected MusixMatchBaseClient()
    {
        _client.BaseAddress = _baseUrl;
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        _client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);

        _client.DefaultRequestHeaders.Add("authority", "apic-desktop.musixmatch.com");
        _client.DefaultRequestHeaders.Add("cookie", "x-mxm-token-guid=");
    }

    protected MusixMatchBaseClient(Guid id)
    {
        _client = new()
        {
            BaseAddress = _baseUrl
        };
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        _client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);

        _client.DefaultRequestHeaders.Add("authority", "apic-desktop.musixmatch.com");
        _client.DefaultRequestHeaders.Add("cookie", "x-mxm-token-guid=");

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

        Logger.MusixMatch(newUrl, LogEventLevel.Verbose);

        string response = await GetQueue().Enqueue(() => _client.GetStringAsync(newUrl), newUrl, priority);

        await CacheController.Write(newUrl, response);

        T? data = response.FromJson<T>();

        return data;
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}