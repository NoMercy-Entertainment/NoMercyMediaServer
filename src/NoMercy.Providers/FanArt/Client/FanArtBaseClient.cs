using Microsoft.AspNetCore.WebUtilities;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using NoMercy.Setup;
using Serilog.Events;

namespace NoMercy.Providers.FanArt.Client;

public class FanArtBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("http://webservice.fanart.tv/v3.2/");

    protected Guid Id { get; private set; }
    private readonly HttpClient _client;

    protected FanArtBaseClient()
    {
        _client = new()
        {
            BaseAddress = _baseUrl
        };
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        _client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        _client.DefaultRequestHeaders.Add("api-key", ApiInfo.FanArtApiKey);
        if (string.IsNullOrEmpty(ApiInfo.FanArtClientKey))
        {
            _client.DefaultRequestHeaders.Add("client-key", ApiInfo.FanArtClientKey);
        }
    }

    protected FanArtBaseClient(Guid id)
    {
        _client = new()
        {
            BaseAddress = _baseUrl
        };
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        _client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        _client.DefaultRequestHeaders.Add("api-key", ApiInfo.FanArtApiKey);
        if (string.IsNullOrEmpty(ApiInfo.FanArtClientKey))
        {
            _client.DefaultRequestHeaders.Add("client-key", ApiInfo.FanArtClientKey);
        }
        Id = id;
    }

    private static Helpers.Queue? _queue;

    private static Helpers.Queue GetQueue()
    {
        return _queue ??= new(new() { Concurrent = 3, Interval = 1000, Start = true });
    }

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
        _client.Dispose();
    }
}