using Microsoft.AspNetCore.WebUtilities;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Setup;
using Serilog.Events;

namespace NoMercy.Providers.TMDB.Client;

public class TmdbBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("https://api.themoviedb.org/3/");
    private readonly string Language;
    private bool _disposed = false;

    public int Id { get; private set; }

    private readonly HttpClient _client;

    protected TmdbBaseClient()
    {
        _client = new()
        {
            BaseAddress = _baseUrl
        };
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiInfo.TmdbToken}");
        _client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        _client.Timeout = TimeSpan.FromMinutes(5);
        Language = "en,null";
    }

    protected TmdbBaseClient(int id, string language = "en-US")
    {

        _client = new()
        {
            BaseAddress = _baseUrl
        };
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiInfo.TmdbToken}");
        _client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        _client.Timeout = TimeSpan.FromMinutes(5);
        Language = language + ",null";
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

    protected async Task<T?> Get<T>(string url, Dictionary<string, string?>? query = null, bool? priority = false,
        bool skipCache = false)
        where T : class
    {
        query ??= new();
        
        query["language"] = priority is true
            ? Language 
            : "";

        query["include_adult"] = Config.AllowAdultContent;

        string newUrl = QueryHelpers.AddQueryString(url, query);

        if (!skipCache && CacheController.Read(newUrl, out T? result)) return result;

        Logger.MovieDb(_baseUrl + newUrl, LogEventLevel.Verbose);

        try
        {
            string response = await GetQueue().Enqueue(() => {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(TmdbBaseClient), "Cannot access a disposed TMDB client.");
                }
                return _client.GetStringAsync(newUrl);
            }, newUrl, priority);

            if (!skipCache) await CacheController.Write(newUrl, response);

            T? data = response.FromJson<T>();

            return data;
        }
        catch (ObjectDisposedException)
        {
            // If the client is disposed, return null gracefully
            Logger.MovieDb($"TMDB client disposed during operation for {newUrl}", LogEventLevel.Debug);
            return null;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("422") || ex.Message.Contains("400"))
        {
            // Handle common HTTP errors gracefully - return null for not found, unprocessable entity, or bad request
            Logger.MovieDb($"HTTP error for {newUrl}: {ex.Message}", LogEventLevel.Debug);
            return null;
        }
    }

    protected async Task<List<T>?> Paginated<T>(string url, int limit) where T : class
    {
        List<T> list = [];

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
        if (_disposed) return;
        
        _disposed = true;
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}