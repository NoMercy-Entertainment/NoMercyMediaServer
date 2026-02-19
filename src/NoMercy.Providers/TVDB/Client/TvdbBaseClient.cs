using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TVDB.Models;
using NoMercy.Setup;
using Serilog.Events;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("https://api4.thetvdb.com/v4/");

    private readonly HttpClient _client;

    private static TvdbLoginResponse? Token { get; set; }

    protected TvdbBaseClient()
    {
        Login().Wait();

        _client = HttpClientProvider.CreateClient(HttpClientNames.Tvdb);
        _client.BaseAddress ??= _baseUrl;
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {Token?.Data.Token}");
    }

    protected TvdbBaseClient(int id)
    {
        Login().Wait();

        _client = HttpClientProvider.CreateClient(HttpClientNames.Tvdb);
        _client.BaseAddress ??= _baseUrl;
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {Token?.Data.Token}");
        Id = id;
    }

    private static Helpers.Queue? _queue;

    private async Task Login()
    {
        if (IsTokenValid(Token)) return;

        TvdbLoginResponse? token = await GetToken();

        if (token is null) return;

        Token = token;
    }

    private static bool IsTokenValid(TvdbLoginResponse? token)
    {
        if (token is null) return false;

        if (token.Data.ExpiresAt >= DateTime.Now.AddMinutes(5)) return true;

        Token = null;
        return false;
    }


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

    private async Task<TvdbLoginResponse?> GetToken()
    {
        HttpClient client = HttpClientProvider.CreateClient(HttpClientNames.TvdbLogin);
        client.BaseAddress ??= _baseUrl;

        JsonContent content = JsonContent.Create(new { apikey = ApiInfo.TvdbKey });

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, "login")
        {
            Content = content
        };

        using HttpResponseMessage httpResponse = await client.SendAsync(httpRequestMessage);
        string response = await httpResponse.Content.ReadAsStringAsync();

        return response.FromJson<TvdbLoginResponse>();
    }

    protected async Task<T?> Get<T>(string url, Dictionary<string, string>? query = null, bool? priority = false,
        bool skipCache = false)
        where T : class
    {
        query ??= new();

        string newUrl = QueryHelpers.AddQueryString(url, query!);

        if (!skipCache && CacheController.Read(newUrl, out T? result)) return result;

        Logger.Tvdb(_baseUrl + newUrl, LogEventLevel.Verbose);

        string response = await GetQueue().Enqueue(() => _client.GetStringAsync(newUrl), newUrl, priority);

        if (!skipCache) await CacheController.Write(newUrl, response);

        T? data = response.FromJson<T>();

        return data;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}