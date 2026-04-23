using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using Serilog.Events;
using HttpClient = System.Net.Http.HttpClient;

namespace NoMercy.Providers.MusicBrainz.Client;

public class MusicBrainzBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("https://musicbrainz.org/ws/2/");

    private readonly HttpClient _client;

    protected MusicBrainzBaseClient()
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.MusicBrainz);
        _client.BaseAddress ??= _baseUrl;
    }

    protected MusicBrainzBaseClient(Guid id)
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.MusicBrainz);
        _client.BaseAddress ??= _baseUrl;
        Id = id;
    }

    private static Helpers.Queue? _queue;

    private static Helpers.Queue GetQueue()
    {
        return _queue ??= new(
            new()
            {
                Concurrent = 1,
                Interval = 1100,
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

        T? data;

        string? response;
        try
        {
            HttpResponseMessage httpResponse = await GetQueue()
                .Enqueue(() => _client.GetAsync(newUrl), newUrl, priority);

            if (!httpResponse.IsSuccessStatusCode)
            {
                string body = await httpResponse.Content.ReadAsStringAsync();
                string truncated = body.Length > 500 ? body[..500] : body;
                Logger.App(
                    $"MusicBrainz non-success {(int)httpResponse.StatusCode} ({newUrl}): {truncated}",
                    LogEventLevel.Debug
                );

                int statusCode = (int)httpResponse.StatusCode;

                if (statusCode == 403 && retry < 3)
                {
                    int delay = (retry + 1) * 30_000;
                    Logger.App(
                        $"MusicBrainz 403 ({newUrl}), retrying in {delay / 1000}s (attempt {retry + 1}/3)",
                        LogEventLevel.Debug
                    );
                    await Task.Delay(delay);
                    return await Get<T>(url, query, priority, retry + 1);
                }

                if ((statusCode == 429 || statusCode == 503) && retry < 10)
                {
                    int delay = (int)Math.Pow(2, retry + 1) * 1000;
                    Logger.App(
                        $"MusicBrainz {statusCode} ({newUrl}), retrying in {delay / 1000}s (attempt {retry + 1}/10)",
                        LogEventLevel.Debug
                    );
                    await Task.Delay(delay);
                    return await Get<T>(url, query, priority, retry + 1);
                }

                httpResponse.EnsureSuccessStatusCode();
            }

            response = await httpResponse.Content.ReadAsStringAsync();
            await CacheController.Write(newUrl, response);

            data = response.FromJson<T>();
        }
        catch (Exception e)
            when (retry < 10 && (e.Message.Contains("429") || e.Message.Contains("503")))
        {
            int delay = (int)Math.Pow(2, retry + 1) * 1000;
            Logger.App(
                $"Rate limited ({newUrl}), retrying in {delay / 1000}s (attempt {retry + 1}/10)",
                LogEventLevel.Debug
            );
            await Task.Delay(delay);
            return await Get<T>(url, query, priority, retry + 1);
        }

        return data ?? throw new($"Failed to parse {response}");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
