using System.Net.Http.Headers;
using NoMercy.NmSystem;
using NoMercy.Providers.Helpers;
using Serilog.Events;

namespace NoMercy.Providers.MusicBrainz.Client;

public class MusicBrainzBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("https://musicbrainz.org/ws/2/");

    private readonly HttpClient _client = new();

    protected MusicBrainzBaseClient()
    {
        _client.BaseAddress = _baseUrl;
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _client.DefaultRequestHeaders.Add("User-Agent", "anonymous");
    }

    protected MusicBrainzBaseClient(Guid id)
    {
        _client = new HttpClient
        {
            BaseAddress = _baseUrl
        };
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _client.DefaultRequestHeaders.Add("User-Agent", "anonymous");
        Id = id;
    }

    private static Helpers.Queue? _queue;

    private static Helpers.Queue GetQueue()
    {
        return _queue ??= new Helpers.Queue(new QueueOptions { Concurrent = 20, Interval = 1000, Start = true });
    }

    protected Guid Id { get; private set; }

    protected async Task<T?> Get<T>(string url, Dictionary<string, string>? query = null, bool? priority = false,
        int iteration = 0)
        where T : class
    {
        query ??= new Dictionary<string, string>();

        string newUrl = url.ToQueryUri(query!);

        if (CacheController.Read(newUrl, out T? result)) return result;

        Logger.MusicBrainz(newUrl, LogEventLevel.Verbose);

        T? data;

        string? response = null as string;
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
                return await Get<T>(url, query, priority, iteration + 1);
            }

            if (iteration == 10) throw;

            Task.Delay(5000).Wait();
            return await Get<T>(url, query, priority, iteration + 1);
        }

        return data ?? throw new Exception($"Failed to parse {response}");
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}