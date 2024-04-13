﻿using System.Drawing;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using NoMercy.Helpers;
using NoMercy.Providers.Helpers;

namespace NoMercy.Providers.CoverArt.Client
{
    public class BaseClient : IDisposable
    {
        private readonly Uri _baseUrl = new("https://coverartarchive.org/");

        private readonly HttpClient _client = new();

        protected BaseClient()
        {
            _client.BaseAddress = _baseUrl;
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _client.DefaultRequestHeaders.Add("User-Agent","NoMercy MediaServer");
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
            Id = id;
        }

        private static Queue? _queue;

        private static Queue GetQueue()
        {
            return _queue ??= new Queue(new QueueOptions { Concurrent = 3, Interval = 1000, Start = true });
        }

        protected Guid Id { get; private set; }
        
        protected async Task<T?> Get<T>(string url, Dictionary<string, string>? query = null, bool? priority = false) where T : class
        {
            query ??= new Dictionary<string, string>();

            var newUrl = QueryHelpers.AddQueryString(url, query!);

            if (CacheController.Read(newUrl, out T? result))
            {
                return result;
            }

            Logger.CoverArt(newUrl, LogLevel.Verbose);

            var response = await GetQueue().Enqueue(() => _client.GetStringAsync(newUrl), newUrl, priority);
            
            await CacheController.Write(newUrl, response);

            var data = JsonConvert.DeserializeObject<T>(response);

            return data;
        }
        
        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
