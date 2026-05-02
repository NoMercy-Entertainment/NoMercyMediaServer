using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using NoMercy.NmSystem.Extensions;
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

    private static Queue? _queue;

    protected static Queue GetQueue()
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

    protected async Task<T2?> Post<T1, T2>(string url, T1 query, bool? priority = false)
        where T1 : class
        where T2 : class
    {
        string xml = query.ToXml();
        Logger.OpenSubs(xml);

        string newUrl = QueryHelpers.AddQueryString(
            url,
            new Dictionary<string, string?> { { "query", xml } }
        );

        string response = await GetQueue().Enqueue(() => SendAsync(url, xml), newUrl, priority);

        await CacheController.Write(newUrl, response);

        Logger.OpenSubs(response);

        return response.FromXml<T2>();
    }

    private async Task<string> SendAsync(string url, string xml)
    {
        using StringContent content = new(xml, Encoding.UTF8, "text/xml");
        using HttpResponseMessage response = await _client.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
