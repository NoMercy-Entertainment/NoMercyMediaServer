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

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoMercy.Api.DTOs.Common;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Contracts;

[Trait("Category", "Contract")]
public class ResponseEnvelopeContractTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public ResponseEnvelopeContractTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    private JsonSerializerSettings AppSerializerSettings =>
        _factory
            .Services.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>()
            .Value.SerializerSettings;

    private static string[] SortedPropertyNames(JObject obj) =>
        obj.Properties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    [Fact]
    public void StatusResponseDto_SerializesTheLockedEnvelopeFields()
    {
        StatusResponseDto<object> envelope = new()
        {
            Status = "ok",
            Data = new { sample = 1 },
            Message = "hello",
            Args = [],
        };

        string json = JsonConvert.SerializeObject(envelope, AppSerializerSettings);
        JObject parsed = JObject.Parse(json);

        string[] expected = ["args", "data", "message", "status"];
        Assert.Equal(expected, SortedPropertyNames(parsed));
    }

    [Fact]
    public void DataResponseDto_SerializesTheLockedEnvelopeFields()
    {
        DataResponseDto<object> envelope = new() { Data = new { sample = 1 } };

        string json = JsonConvert.SerializeObject(envelope, AppSerializerSettings);
        JObject parsed = JObject.Parse(json);

        string[] expected = ["data"];
        Assert.Equal(expected, SortedPropertyNames(parsed));
    }

    [Fact]
    public void PaginatedResponse_SerializesTheLockedEnvelopeFields()
    {
        PaginatedResponse<object> envelope = new()
        {
            Data = [new { sample = 1 }],
            NextPage = 2,
            HasMore = true,
        };

        string json = JsonConvert.SerializeObject(envelope, AppSerializerSettings);
        JObject parsed = JObject.Parse(json);

        string[] expected = ["data", "has_more", "next_page"];
        Assert.Equal(expected, SortedPropertyNames(parsed));

        Assert.Equal(2, parsed["next_page"]!.Value<int>());
        Assert.True(parsed["has_more"]!.Value<bool>());
    }

    [Fact]
    public async Task SuccessEnvelope_OverRealHost_MatchesTheLockedStatusResponseShape()
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();
        HttpResponseMessage response = await client.GetAsync("/api/v1/dashboard/devices");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        JObject parsed = JObject.Parse(body);

        string[] expected = ["args", "data", "message", "status"];
        Assert.Equal(expected, SortedPropertyNames(parsed));
        Assert.Equal("ok", parsed["status"]!.Value<string>());
        Assert.Equal(JTokenType.Array, parsed["data"]!.Type);
    }

    [Fact]
    public async Task ErrorEnvelope_OverRealHost_MatchesTheLockedProblemDetailsShape()
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();
        HttpResponseMessage response = await client.DeleteAsync(
            $"/api/v1/dashboard/libraries/{Ulid.NewUlid()}"
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        JObject parsed = JObject.Parse(body);

        string[] requiredFields = ["detail", "instance", "status", "title", "type"];
        string[] actualFields = SortedPropertyNames(parsed);

        foreach (string field in requiredFields)
            Assert.Contains(field, actualFields);

        Assert.Equal(404, parsed["status"]!.Value<int>());
        Assert.Equal("/docs/errors/not-found", parsed["type"]!.Value<string>());
    }
}
