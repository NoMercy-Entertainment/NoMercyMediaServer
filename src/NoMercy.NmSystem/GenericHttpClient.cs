using System.Net;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;

namespace NoMercy.NmSystem;

public class GenericHttpClient
{
    private readonly HttpClient _client;
    private readonly AsyncPolicyWrap<HttpResponseMessage> _resiliencePolicy;

    public GenericHttpClient(string? baseUrl = null, int timeoutSeconds = 5, int retryCount = 3)
    {
        _client = new();

        if (!string.IsNullOrEmpty(baseUrl)) _client.BaseAddress = new(baseUrl);

        // Timeout policy
        AsyncTimeoutPolicy<HttpResponseMessage>? timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(timeoutSeconds),
            TimeoutStrategy.Optimistic);

        // Retry only for transient failures: 5xx, 408 (RequestTimeout), 429 (TooManyRequests) and network exceptions
        AsyncRetryPolicy<HttpResponseMessage>? retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>() // often indicates a timeout or network drop
            .OrResult(r => r != null && IsTransientStatusCode(r.StatusCode))
            .WaitAndRetryAsync(retryCount,
                retryAttempt =>
                {
                    // Exponential backoff with cap
                    double seconds = Math.Min(Math.Pow(2, retryAttempt), 30);
                    return TimeSpan.FromSeconds(seconds);
                },
                onRetryAsync: (_, _, _, _) => Task.CompletedTask);

        _resiliencePolicy = Policy.WrapAsync(retryPolicy, timeoutPolicy);
    }

    public void SetDefaultHeaders(string userAgent, string? bearerToken = null)
    {
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new("application/json"));

        if (!string.IsNullOrEmpty(bearerToken))
            _client.DefaultRequestHeaders.Authorization = new("Bearer", bearerToken);
    }

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string endpoint, HttpContent? content = null, CancellationToken cancellationToken = default)
    {
        return _resiliencePolicy.ExecuteAsync(ct =>
        {
            HttpRequestMessage request = new(method, endpoint) { Content = content };
            return _client.SendAsync(request, ct);
        }, cancellationToken);
    }

    public async Task<string> SendAndReadAsync(HttpMethod method, string endpoint, HttpContent? content = null,
        Dictionary<string, string>? queryParams = null, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        if (queryParams?.Count > 0)
            response = await SendAsync(method, endpoint, queryParams, cancellationToken);
        else
            response = await SendAsync(method, endpoint, content, cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string endpoint,
        Dictionary<string, string> queryParams, CancellationToken cancellationToken = default)
    {
        if (queryParams.Count > 0)
        {
            string query = string.Join("&",
                queryParams.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            endpoint = $"{endpoint}{(endpoint.Contains('?') ? "&" : "?")}{query}";
        }

        return _resiliencePolicy.ExecuteAsync(ct =>
        {
            HttpRequestMessage request = new(method, endpoint);
            return _client.SendAsync(request, ct);
        }, cancellationToken);
    }

    private static bool IsTransientStatusCode(HttpStatusCode? statusCode)
    {
        if (!statusCode.HasValue) return false;
        int code = (int)statusCode.Value;

        return code switch
        {
            // Retry on server errors (5xx), 408 Request Timeout, and 429 Too Many Requests
            >= 500 and <= 599 or 408 or 429 => true,
            _ => false
        };
    }
}