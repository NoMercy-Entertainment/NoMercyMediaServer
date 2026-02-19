using System.Reflection;
using NoMercy.Providers.FanArt.Client;
using NoMercy.Setup;

namespace NoMercy.Tests.Providers.FanArt.Client;

/// <summary>
/// PROV-CRIT-04: Tests verifying that FanArtBaseClient correctly checks
/// the FanArtClientKey before adding the "client-key" header.
/// The bug: `if (string.IsNullOrEmpty(ApiInfo.FanArtClientKey))` added the header
/// when the key IS empty and skipped it when populated — inverted logic.
/// The fix: `if (!string.IsNullOrEmpty(ApiInfo.FanArtClientKey))`
/// </summary>
[Trait("Category", "Unit")]
public class FanArtBaseClientTests : IDisposable
{
    private readonly string _originalClientKey;

    public FanArtBaseClientTests()
    {
        _originalClientKey = ApiInfo.FanArtClientKey;
    }

    public void Dispose()
    {
        ApiInfo.FanArtClientKey = _originalClientKey;
    }

    private static HttpClient GetHttpClient(FanArtBaseClient client)
    {
        FieldInfo? field = typeof(FanArtBaseClient).GetField(
            "_client",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(field);
        HttpClient httpClient = (HttpClient)field.GetValue(client)!;
        Assert.NotNull(httpClient);
        return httpClient;
    }

    [Fact]
    public void ParameterlessConstructor_WithPopulatedClientKey_AddsClientKeyHeader()
    {
        ApiInfo.FanArtClientKey = "test-client-key-123";

        using FanArtBaseClient client = (FanArtBaseClient)Activator.CreateInstance(
            typeof(FanArtBaseClient),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            Array.Empty<object>(),
            null)!;

        HttpClient httpClient = GetHttpClient(client);
        bool hasHeader = httpClient.DefaultRequestHeaders.Contains("client-key");

        Assert.True(hasHeader,
            "PROV-CRIT-04 regression: client-key header should be present when FanArtClientKey is populated");

        IEnumerable<string> values = httpClient.DefaultRequestHeaders.GetValues("client-key");
        Assert.Equal("test-client-key-123", values.First());
    }

    [Fact]
    public void ParameterlessConstructor_WithEmptyClientKey_DoesNotAddClientKeyHeader()
    {
        ApiInfo.FanArtClientKey = string.Empty;

        using FanArtBaseClient client = (FanArtBaseClient)Activator.CreateInstance(
            typeof(FanArtBaseClient),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            Array.Empty<object>(),
            null)!;

        HttpClient httpClient = GetHttpClient(client);
        bool hasHeader = httpClient.DefaultRequestHeaders.Contains("client-key");

        Assert.False(hasHeader,
            "PROV-CRIT-04 regression: client-key header should NOT be present when FanArtClientKey is empty");
    }

    [Fact]
    public void ParameterlessConstructor_WithNullClientKey_DoesNotAddClientKeyHeader()
    {
        ApiInfo.FanArtClientKey = null!;

        using FanArtBaseClient client = (FanArtBaseClient)Activator.CreateInstance(
            typeof(FanArtBaseClient),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            Array.Empty<object>(),
            null)!;

        HttpClient httpClient = GetHttpClient(client);
        bool hasHeader = httpClient.DefaultRequestHeaders.Contains("client-key");

        Assert.False(hasHeader,
            "PROV-CRIT-04 regression: client-key header should NOT be present when FanArtClientKey is null");
    }

    [Fact]
    public void GuidConstructor_WithPopulatedClientKey_AddsClientKeyHeader()
    {
        ApiInfo.FanArtClientKey = "test-client-key-456";
        Guid testId = Guid.NewGuid();

        using FanArtBaseClient client = (FanArtBaseClient)Activator.CreateInstance(
            typeof(FanArtBaseClient),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new object[] { testId },
            null)!;

        HttpClient httpClient = GetHttpClient(client);
        bool hasHeader = httpClient.DefaultRequestHeaders.Contains("client-key");

        Assert.True(hasHeader,
            "PROV-CRIT-04 regression: client-key header should be present when FanArtClientKey is populated (Guid constructor)");

        IEnumerable<string> values = httpClient.DefaultRequestHeaders.GetValues("client-key");
        Assert.Equal("test-client-key-456", values.First());
    }

    [Fact]
    public void GuidConstructor_WithEmptyClientKey_DoesNotAddClientKeyHeader()
    {
        ApiInfo.FanArtClientKey = string.Empty;
        Guid testId = Guid.NewGuid();

        using FanArtBaseClient client = (FanArtBaseClient)Activator.CreateInstance(
            typeof(FanArtBaseClient),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new object[] { testId },
            null)!;

        HttpClient httpClient = GetHttpClient(client);
        bool hasHeader = httpClient.DefaultRequestHeaders.Contains("client-key");

        Assert.False(hasHeader,
            "PROV-CRIT-04 regression: client-key header should NOT be present when FanArtClientKey is empty (Guid constructor)");
    }

    [Fact]
    public void GuidConstructor_SetsIdProperty()
    {
        ApiInfo.FanArtClientKey = string.Empty;
        Guid testId = Guid.NewGuid();

        using FanArtBaseClient client = (FanArtBaseClient)Activator.CreateInstance(
            typeof(FanArtBaseClient),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new object[] { testId },
            null)!;

        PropertyInfo? idProp = typeof(FanArtBaseClient).GetProperty(
            "Id",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(idProp);
        Guid actualId = (Guid)idProp.GetValue(client)!;
        Assert.Equal(testId, actualId);
    }

    [Fact]
    public void Constructor_AlwaysAddsApiKeyHeader()
    {
        ApiInfo.FanArtClientKey = string.Empty;

        using FanArtBaseClient client = (FanArtBaseClient)Activator.CreateInstance(
            typeof(FanArtBaseClient),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            Array.Empty<object>(),
            null)!;

        HttpClient httpClient = GetHttpClient(client);
        bool hasApiKey = httpClient.DefaultRequestHeaders.Contains("api-key");

        Assert.True(hasApiKey, "api-key header should always be present regardless of client key");
    }
}
