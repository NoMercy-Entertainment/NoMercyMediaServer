using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using Serilog.Events;
using HttpClient = System.Net.Http.HttpClient;

namespace NoMercy.Providers.Lrclib.Client;

public class LrclibBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("https://lrclib.net/api/get");

    private readonly HttpClient _client;

    public LrclibBaseClient()
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.Lrclib);
        _client.BaseAddress ??= _baseUrl;
    }

    private static Helpers.Queue? _queue;

    private static Helpers.Queue GetQueue()
    {
        return _queue ??= new(new() { Concurrent = 1, Interval = 1000, Start = true });
    }

    protected Guid Id { get; private set; }

    protected async Task<T?> Get<T>(string url, Dictionary<string, string>? query = null, bool? priority = false,
        int retry = 0) where T : class
    {
        query ??= new();

        string newUrl = url.ToQueryUri(query);

        if (CacheController.Read(newUrl, out T? result)) return result;

        Logger.MusicBrainz(_baseUrl + newUrl, LogEventLevel.Verbose);

        T? data;

        string? response;
        try
        {
            response = await GetQueue().Enqueue(() => _client.GetStringAsync(newUrl), newUrl, priority);
            await CacheController.Write(newUrl, response);

            data = response.FromJson<T>();
        }
        catch (Exception e)
        {
            if (e.Message.Contains("503"))
            {
                Task.Delay(5000).Wait();
                return await Get<T>(url, query, priority, retry + 1);
            }

            if (retry == 10) throw;

            Task.Delay(5000).Wait();
            return await Get<T>(url, query, priority, retry + 1);
        }

        return data ?? throw new($"Failed to parse {response}");
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}