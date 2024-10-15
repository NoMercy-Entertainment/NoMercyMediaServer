using System.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Providers.AcoustId.Models;
using NoMercy.Providers.Helpers;
using Serilog.Events;

namespace NoMercy.Providers.AcoustId.Client;

public class AcoustIdBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("https://api.acoustid.org/v2/");

    private readonly HttpClient _client = new();

    protected AcoustIdBaseClient()
    {
        _client.BaseAddress = _baseUrl;
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _client.DefaultRequestHeaders.Add("User-Agent", ApiInfo.UserAgent);
    }

    protected AcoustIdBaseClient(Guid id)
    {
        _client = new HttpClient
        {
            BaseAddress = _baseUrl
        };
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _client.DefaultRequestHeaders.Add("User-Agent", ApiInfo.UserAgent);
        Id = id;
    }

    private static Helpers.Queue? _queue;

    private static Helpers.Queue GetQueue()
    {
        return _queue ??= new Helpers.Queue(new QueueOptions { Concurrent = 3, Interval = 1000, Start = true });
    }

    protected Guid Id { get; private set; }

    protected async Task<T?> Get<T>(string url, Dictionary<string, string?>? query = default, bool? priority = false)
        where T : class
    {
        query ??= new Dictionary<string, string?>();

        string newUrl = QueryHelpers.AddQueryString(url, query);

        if (CacheController.Read(newUrl, out AcoustIdFingerprint? result))
            if (result?.Results.Length > 0 && result.Results
                    .Any(fpResult => fpResult.Recordings is not null && fpResult.Recordings
                        .Any(recording => recording?.Title != null)))
                return result as T;

        Logger.AcoustId(newUrl, LogEventLevel.Verbose);

        string response = await GetQueue().Enqueue(() => _client.GetStringAsync(newUrl), newUrl, priority);

        await CacheController.Write(newUrl, response);

        AcoustIdFingerprint? data = response.FromJson<AcoustIdFingerprint>();

        int iteration = 0;

        if (data?.Results.Length > 0 && data.Results
                .Any(fpResult => fpResult.Recordings is not null && fpResult.Recordings
                    .Any(recording => recording?.Title != null))) return data as T;

        while (data?.Results.Length == 0 && data.Results
                   .Any(fpResult => fpResult.Recordings is not null && fpResult.Recordings
                       .Any(recording => recording?.Title == null)) && iteration < 10)
        {
            response = await GetQueue().Enqueue(() => _client.GetStringAsync(newUrl), newUrl, priority);

            await CacheController.Write(newUrl, response);

            Logger.Request(response, LogEventLevel.Verbose);

            data = response.FromJson<AcoustIdFingerprint>();

            iteration++;
        }

        return data as T;
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}