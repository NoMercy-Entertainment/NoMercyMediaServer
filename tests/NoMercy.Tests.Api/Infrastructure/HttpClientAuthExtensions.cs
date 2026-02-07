namespace NoMercy.Tests.Api.Infrastructure;

public static class HttpClientAuthExtensions
{
    public static HttpClient AsAuthenticated(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove(TestAuthDefaults.TestAuthHeader);
        return client;
    }

    public static HttpClient AsUnauthenticated(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove(TestAuthDefaults.TestAuthHeader);
        client.DefaultRequestHeaders.Add(TestAuthDefaults.TestAuthHeader, TestAuthDefaults.Deny);
        return client;
    }
}
