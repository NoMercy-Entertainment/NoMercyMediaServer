﻿using System.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using NoMercy.Helpers;
using NoMercy.Providers.Helpers;

namespace NoMercy.Providers.MusixMatch.Client
{
    public class BaseClient : IDisposable
    {
        private readonly Uri _baseUrl = new("https://apic-desktop.musixmatch.com/ws/1.1/");

        private readonly HttpClient _client = new();

        protected BaseClient()
        {
            _client.BaseAddress = _baseUrl;
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _client.DefaultRequestHeaders.Add("User-Agent","NoMercy MediaServer");
            
            _client.DefaultRequestHeaders.Add("authority","apic-desktop.musixmatch.com");
            _client.DefaultRequestHeaders.Add("cookie", "x-mxm-token-guid=");
        }

        protected BaseClient(Guid id)
        {
            _client = new HttpClient
            {
                BaseAddress = _baseUrl
            };
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _client.DefaultRequestHeaders.Add("User-Agent","NoMercy MediaServer");
            
            _client.DefaultRequestHeaders.Add("authority","apic-desktop.musixmatch.com");
            _client.DefaultRequestHeaders.Add("cookie", "x-mxm-token-guid=");
            
            Id = id;
        }

        private static Queue? _queue;

        private static Queue GetQueue()
        {
            return _queue ??= new Queue(new QueueOptions { Concurrent = 2, Interval = 1000, Start = true });
        }

        protected Guid Id { get; private set; }
        
        protected async Task<T?> Get<T>(string url, Dictionary<string, string?> query, bool? priority = false) where T : class
        {
            query.Add("format", "json");
            query.Add("namespace", "lyrics_richsynched");
            query.Add("subtitle_format", "mxm");
            query.Add("app_id", "web-desktop-app-v1.0");
            query.Add("usertoken", ApiInfo.MusixmatchKey);

            var newUrl = QueryHelpers.AddQueryString(url, query);

            if (CacheController.Read(newUrl, out T? result))
            {
                return result;
            }

            Logger.MusixMatch(newUrl, LogLevel.Verbose);

            var response = await GetQueue().Enqueue(() => _client.GetStringAsync(newUrl), newUrl, priority);
            
            await CacheController.Write(newUrl, response);

            var data = JsonHelper.FromJson<T>(response);

            return data;
        }
        
        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
