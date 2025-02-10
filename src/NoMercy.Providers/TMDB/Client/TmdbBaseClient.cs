using Microsoft.AspNetCore.WebUtilities;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TMDB.Models.Shared;
using Serilog.Events;

namespace NoMercy.Providers.TMDB.Client;

public class TmdbBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("https://api.themoviedb.org/3/");

    private readonly HttpClient _client = new();

    protected TmdbBaseClient()
    {
        _client.BaseAddress = _baseUrl;
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiInfo.TmdbToken}");
        _client.DefaultRequestHeaders.Add("User-Agent", ApiInfo.UserAgent);
        _client.Timeout = TimeSpan.FromMinutes(5);
    }

    protected TmdbBaseClient(int id)
    {
        _client = new()
        {
            BaseAddress = _baseUrl
        };
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiInfo.TmdbToken}");
        _client.DefaultRequestHeaders.Add("User-Agent", ApiInfo.UserAgent);
        _client.Timeout = TimeSpan.FromMinutes(5);
        Id = id;
    }

    private static Helpers.Queue? _queue;

    protected static Helpers.Queue GetQueue()
    {
        return _queue ??= new(new() { Concurrent = 50, Interval = 1000, Start = true });
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

    protected async Task<T?> Get<T>(string url, Dictionary<string, string?>? query = null, bool? priority = false, bool skipCache = false)
        where T : class
    {
        query ??= new();

        string newUrl = QueryHelpers.AddQueryString(url, query);

        if (!skipCache && CacheController.Read(newUrl, out T? result)) return result;

        Logger.MovieDb(newUrl, LogEventLevel.Verbose);

        string response = await GetQueue().Enqueue(() => _client.GetStringAsync(newUrl), newUrl, priority);

        if (!skipCache)
        {
            await CacheController.Write(newUrl, response);
        }

        T? data = response.FromJson<T>();

        return data;
    }

    protected async Task<List<T>?> Paginated<T>(string url, int limit) where T : class
    {
        List<T> list = new();

        TmdbPaginatedResponse<T>? firstPage = await Get<TmdbPaginatedResponse<T>>(url);
        list.AddRange(firstPage?.Results ?? []);

        if (limit > 1)
            await Parallel.ForAsync(2, Max(firstPage?.TotalPages ?? 0, limit, 500), async (i, _) =>
            {
                TmdbPaginatedResponse<T>? page = await Get<TmdbPaginatedResponse<T>>(url, new()
                {
                    ["page"] = i.ToString()
                });
                lock (list)
                {
                    list.AddRange(page?.Results ?? []);
                }
            });

        return list;
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
