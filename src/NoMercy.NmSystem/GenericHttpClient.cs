using Polly;
using Polly.Wrap;

namespace NoMercy.NmSystem;

public class GenericHttpClient
{
    private readonly HttpClient _client;
    private readonly AsyncPolicyWrap<HttpResponseMessage> _retryPolicy;

    public GenericHttpClient(string? baseUrl = null, int timeoutSeconds = 5, int retryCount = 3000)
    {
        _client = new();

        if (!string.IsNullOrEmpty(baseUrl))
        {
            _client.BaseAddress = new(baseUrl);
        }

        _retryPolicy = Policy.WrapAsync(
            [
                Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                    .Or<HttpRequestException>()
                    .WaitAndRetryAsync(retryCount, _ => TimeSpan.FromSeconds(2),
                        (result, timeSpan, rtCount, context) =>
                        {
                            Console.WriteLine($"Retry {rtCount}: {baseUrl} after {timeSpan.TotalSeconds} seconds due to: {result.Exception?.Message ?? result.Result?.StatusCode.ToString()}");
                        }),
                Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(timeoutSeconds), Polly.Timeout.TimeoutStrategy.Optimistic)
            ]);
    }

    public void SetDefaultHeaders(string userAgent, string? bearerToken = null)
    {
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        _client.DefaultRequestHeaders.Accept.Add(new("application/json"));

        if (!string.IsNullOrEmpty(bearerToken))
        {
            _client.DefaultRequestHeaders.Authorization = new("Bearer", bearerToken);
        }
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string endpoint, HttpContent? content = null)
    {
        return await _retryPolicy.ExecuteAsync(() =>
        {
            HttpRequestMessage request = new(method, endpoint) { Content = content };
            return _client.SendAsync(request);
        });
    }

    public async Task<string> SendAndReadAsync(HttpMethod method, string endpoint, HttpContent? content = null, Dictionary<string, string>? queryParams = null)
    {
        HttpResponseMessage response;
        if(queryParams?.Count > 0)
        {
            response = await SendAsync(method, endpoint, queryParams);
        }
        else
        {
            response = await SendAsync(method, endpoint, content);
        }
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync();
    }
    
    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string endpoint, Dictionary<string, string> queryParams)
    {
        if (queryParams.Count > 0)
        {
            string query = string.Join("&", queryParams.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            endpoint = $"{endpoint}?{query}";
        }

        return await _retryPolicy.ExecuteAsync(() =>
        {
            HttpRequestMessage request = new(method, endpoint);
            return _client.SendAsync(request);
        });
    }
}