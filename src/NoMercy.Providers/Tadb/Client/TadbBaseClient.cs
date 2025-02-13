using Microsoft.AspNetCore.WebUtilities;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Providers.Helpers;
using Serilog.Events;

namespace NoMercy.Providers.Tadb.Client;

public class TadbBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new($"https://www.theaudiodb.com/api/v1/json/{ApiInfo.TadbKey}/");

    private readonly HttpClient _client = new();

    protected TadbBaseClient()
    {
        _client.BaseAddress = _baseUrl;
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        _client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        _client.Timeout = TimeSpan.FromMinutes(5);
    }

    protected TadbBaseClient(int id)
    {
        _client = new()
        {
            BaseAddress = _baseUrl
        };
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        _client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        _client.Timeout = TimeSpan.FromMinutes(5);
        Id = id;
    }

    private static Helpers.Queue? _queue;

    private static Helpers.Queue GetQueue()
    {
        return _queue ??= new(new() { Concurrent = 2, Interval = 1000, Start = true });
    }

    private static int Max(int available, int wanted, int constraint)
    {
        return wanted < available
            ? wanted > constraint
                ? constraint
                : wanted
            : available;
    }

    public int Id { get; private set; }

    protected async Task<T?> Get<T>(string url, Dictionary<string, string>? query = null, bool? priority = false)
        where T : class
    {
        query ??= new();

        string newUrl = QueryHelpers.AddQueryString(url, query!);

        if (CacheController.Read(newUrl, out T? result)) return result;

        Logger.MovieDb(newUrl, LogEventLevel.Verbose);

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