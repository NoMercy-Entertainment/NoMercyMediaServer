﻿using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using Serilog.Events;
using HttpClient = System.Net.Http.HttpClient;

namespace NoMercy.Providers.MusicBrainz.Client;

public class MusicBrainzBaseClient : IDisposable
{
    private readonly Uri _baseUrl = new("https://musicbrainz.org/ws/2/");

    private readonly HttpClient _client = new();

    protected MusicBrainzBaseClient()
    {
        _client.BaseAddress = _baseUrl;
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        _client.DefaultRequestHeaders.Add("User-Agent", "anonymous");
    }

    protected MusicBrainzBaseClient(Guid id)
    {
        _client = new()
        {
            BaseAddress = _baseUrl
        };
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        _client.DefaultRequestHeaders.Add("User-Agent", "anonymous");
        Id = id;
    }

    private static Helpers.Queue? _queue;

    private static Helpers.Queue GetQueue()
    {
        return _queue ??= new(new() { Concurrent = 1, Interval = 1000, Start = true });
    }

    protected Guid Id { get; private set; }

    protected async Task<T?> Get<T>(string url, Dictionary<string, string>? query = null, bool? priority = false, int retry = 0) where T : class
    {
        query ??= new();

        string newUrl = url.ToQueryUri(query);

        if (CacheController.Read(newUrl, out T? result)) return result;

        Logger.MusicBrainz(newUrl, LogEventLevel.Verbose);

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
        _client.Dispose();
    }
}