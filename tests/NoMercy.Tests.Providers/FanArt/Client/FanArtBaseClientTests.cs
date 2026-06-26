// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Reflection;
using NoMercy.Providers.FanArt.Client;
using NoMercy.Setup.Server;

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
        _originalClientKey = TestApiKeyStore.Instance.FanArtClientKey;
    }

    public void Dispose()
    {
        TestApiKeyStore.Instance.FanArtClientKey = _originalClientKey;
    }

    private static HttpClient GetHttpClient(FanArtBaseClient client)
    {
        FieldInfo? field = typeof(FanArtBaseClient).GetField(
            "_client",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.NotNull(field);
        HttpClient httpClient = (HttpClient)field.GetValue(client)!;
        Assert.NotNull(httpClient);
        return httpClient;
    }

    [Fact]
    public void ParameterlessConstructor_WithPopulatedClientKey_AddsClientKeyHeader()
    {
        TestApiKeyStore.Instance.FanArtClientKey = "test-client-key-123";

        using FanArtBaseClient client = (FanArtBaseClient)
            Activator.CreateInstance(
                typeof(FanArtBaseClient),
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Array.Empty<object>(),
                null
            )!;

        HttpClient httpClient = GetHttpClient(client);
        bool hasHeader = httpClient.DefaultRequestHeaders.Contains("client-key");

        Assert.True(
            hasHeader,
            "PROV-CRIT-04 regression: client-key header should be present when FanArtClientKey is populated"
        );

        IEnumerable<string> values = httpClient.DefaultRequestHeaders.GetValues("client-key");
        Assert.Equal("test-client-key-123", values.First());
    }

    [Fact]
    public void ParameterlessConstructor_WithEmptyClientKey_DoesNotAddClientKeyHeader()
    {
        TestApiKeyStore.Instance.FanArtClientKey = string.Empty;

        using FanArtBaseClient client = (FanArtBaseClient)
            Activator.CreateInstance(
                typeof(FanArtBaseClient),
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Array.Empty<object>(),
                null
            )!;

        HttpClient httpClient = GetHttpClient(client);
        bool hasHeader = httpClient.DefaultRequestHeaders.Contains("client-key");

        Assert.False(
            hasHeader,
            "PROV-CRIT-04 regression: client-key header should NOT be present when FanArtClientKey is empty"
        );
    }

    [Fact]
    public void ParameterlessConstructor_WithNullClientKey_DoesNotAddClientKeyHeader()
    {
        TestApiKeyStore.Instance.FanArtClientKey = null!;

        using FanArtBaseClient client = (FanArtBaseClient)
            Activator.CreateInstance(
                typeof(FanArtBaseClient),
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Array.Empty<object>(),
                null
            )!;

        HttpClient httpClient = GetHttpClient(client);
        bool hasHeader = httpClient.DefaultRequestHeaders.Contains("client-key");

        Assert.False(
            hasHeader,
            "PROV-CRIT-04 regression: client-key header should NOT be present when FanArtClientKey is null"
        );
    }

    [Fact]
    public void GuidConstructor_WithPopulatedClientKey_AddsClientKeyHeader()
    {
        TestApiKeyStore.Instance.FanArtClientKey = "test-client-key-456";
        Guid testId = Guid.NewGuid();

        using FanArtBaseClient client = (FanArtBaseClient)
            Activator.CreateInstance(
                typeof(FanArtBaseClient),
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new object[] { testId },
                null
            )!;

        HttpClient httpClient = GetHttpClient(client);
        bool hasHeader = httpClient.DefaultRequestHeaders.Contains("client-key");

        Assert.True(
            hasHeader,
            "PROV-CRIT-04 regression: client-key header should be present when FanArtClientKey is populated (Guid constructor)"
        );

        IEnumerable<string> values = httpClient.DefaultRequestHeaders.GetValues("client-key");
        Assert.Equal("test-client-key-456", values.First());
    }

    [Fact]
    public void GuidConstructor_WithEmptyClientKey_DoesNotAddClientKeyHeader()
    {
        TestApiKeyStore.Instance.FanArtClientKey = string.Empty;
        Guid testId = Guid.NewGuid();

        using FanArtBaseClient client = (FanArtBaseClient)
            Activator.CreateInstance(
                typeof(FanArtBaseClient),
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new object[] { testId },
                null
            )!;

        HttpClient httpClient = GetHttpClient(client);
        bool hasHeader = httpClient.DefaultRequestHeaders.Contains("client-key");

        Assert.False(
            hasHeader,
            "PROV-CRIT-04 regression: client-key header should NOT be present when FanArtClientKey is empty (Guid constructor)"
        );
    }

    [Fact]
    public void GuidConstructor_SetsIdProperty()
    {
        TestApiKeyStore.Instance.FanArtClientKey = string.Empty;
        Guid testId = Guid.NewGuid();

        using FanArtBaseClient client = (FanArtBaseClient)
            Activator.CreateInstance(
                typeof(FanArtBaseClient),
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new object[] { testId },
                null
            )!;

        PropertyInfo? idProp = typeof(FanArtBaseClient).GetProperty(
            "Id",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.NotNull(idProp);
        Guid actualId = (Guid)idProp.GetValue(client)!;
        Assert.Equal(testId, actualId);
    }

    [Fact]
    public void Constructor_AlwaysAddsApiKeyHeader()
    {
        TestApiKeyStore.Instance.FanArtClientKey = string.Empty;

        using FanArtBaseClient client = (FanArtBaseClient)
            Activator.CreateInstance(
                typeof(FanArtBaseClient),
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Array.Empty<object>(),
                null
            )!;

        HttpClient httpClient = GetHttpClient(client);
        bool hasApiKey = httpClient.DefaultRequestHeaders.Contains("api-key");

        Assert.True(hasApiKey, "api-key header should always be present regardless of client key");
    }
}
