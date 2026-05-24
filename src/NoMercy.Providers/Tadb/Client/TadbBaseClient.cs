using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using NoMercy.Setup.Server;
using Serilog.Events;

namespace NoMercy.Providers.Tadb.Client;

public class TadbBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new(
        $"https://www.theaudiodb.com/api/v1/json/{ApiInfo.TadbKey}/"
    );

    private readonly HttpClient _client;

    protected TadbBaseClient()
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.Tadb);
        _client.BaseAddress = _baseUrl;
    }

    protected TadbBaseClient(int id)
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.Tadb);
        _client.BaseAddress = _baseUrl;
        Id = id;
    }

    private static Queue? _queue;

    private static Queue GetQueue()
    {
        return _queue ??= new(
            new()
            {
                Concurrent = 2,
                Interval = 1000,
                Start = true,
            }
        );
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

    protected async Task<T?> Get<T>(
        string url,
        Dictionary<string, string>? query = null,
        bool? priority = false
    )
        where T : class
    {
        query ??= new();

        string newUrl = QueryHelpers.AddQueryString(url, query!);

        if (CacheController.Read(newUrl, out T? result))
            return result;

        Logger.AudioDb(_baseUrl + newUrl, LogEventLevel.Verbose);

        try
        {
            string response = await GetQueue()
                .Enqueue(() => _client.GetStringAsync(newUrl), newUrl, priority);

            await CacheController.Write(newUrl, response);
            return response.FromJson<T>();
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            return null;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
