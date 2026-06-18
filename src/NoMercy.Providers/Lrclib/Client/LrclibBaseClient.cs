using System.Net;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using Serilog.Events;
using HttpClient = System.Net.Http.HttpClient;

namespace NoMercy.Providers.Lrclib.Client;

public class LrclibBaseClient : IDisposable
{
    private const int MaxRetries = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly Uri _baseUrl = new("https://lrclib.net/api/");
    private readonly HttpClient _client;

    public LrclibBaseClient()
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.Lrclib);
        _client.BaseAddress ??= _baseUrl;
    }

    private static Queue? _queue;

    private static Queue GetQueue()
    {
        return _queue ??= new(
            new()
            {
                Concurrent = 1,
                Interval = 1000,
                Start = true,
            }
        );
    }

    protected Guid Id { get; private set; }

    protected async Task<T?> Get<T>(
        string url,
        Dictionary<string, string>? query = null,
        bool? priority = false,
        int retry = 0
    )
        where T : class
    {
        query ??= new();
        string newUrl = url.ToQueryUri(query);

        if (CacheController.Read(newUrl, out T? result))
            return result;

        Logger.MusicBrainz(_baseUrl + newUrl, LogEventLevel.Verbose);

        try
        {
            string response = await GetQueue()
                .Enqueue(() => _client.GetStringAsync(newUrl), newUrl, priority);
            await CacheController.Write(newUrl, response);
            return response.FromJson<T>();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Lrclib returns 404 for tracks with no lyrics — treat as "no
            // result" so callers can fall through to the next provider
            // instead of catching exceptions.
            return null;
        }
        catch (HttpRequestException ex) when (retry < MaxRetries)
        {
            Logger.MusicBrainz(
                $"Lrclib {ex.StatusCode} retry {retry + 1}/{MaxRetries} for {newUrl}",
                LogEventLevel.Debug
            );
            await Task.Delay(RetryDelay);
            return await Get<T>(url, query, priority, retry + 1);
        }
        catch (TaskCanceledException) when (retry < MaxRetries)
        {
            await Task.Delay(RetryDelay);
            return await Get<T>(url, query, priority, retry + 1);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
