using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using HttpClient = System.Net.Http.HttpClient;

namespace NoMercy.Providers.OpenSubtitles.Client;

public class OpenSubtitlesBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("https://api.opensubtitles.org/xml-rpc");

    internal static string? AccessToken { get; set; } = null;

    private readonly HttpClient _client;

    protected OpenSubtitlesBaseClient()
    {
        _client = HttpClientProvider.CreateClient(HttpClientNames.OpenSubtitles);
        _client.BaseAddress ??= _baseUrl;
    }

    private static Helpers.Queue? _queue;

    protected static Helpers.Queue GetQueue()
    {
        return _queue ??= new(new() { Concurrent = 1, Interval = 1000, Start = true });
    }

    protected async Task<T2?> Post<T1, T2>(string url, T1 query, bool? priority = false)
        where T1 : class
        where T2 : class
    {
        StringContent content = new(query.ToXml(), Encoding.UTF8, "text/xml");

        Logger.OpenSubs(content.ReadAsStringAsync().Result);

        string newUrl =
            QueryHelpers.AddQueryString(url, new Dictionary<string, string?> { { "query", query.ToXml() } });
        // if (CacheController.Read(newUrl, out T2? result, true)) return result;

        string response = await GetQueue()
            .Enqueue(() => _client.PostAsync(url, content).Result.Content.ReadAsStringAsync(), newUrl, priority);

        await CacheController.Write(newUrl, response);

        Logger.OpenSubs(response);

        T2? data = response.FromXml<T2>();

        return data;
    }


    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}