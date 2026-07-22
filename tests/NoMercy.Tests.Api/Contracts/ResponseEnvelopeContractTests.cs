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

[Trait(name: "Category", value: "Contract")]
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
        obj.Properties().Select(selector: p => p.Name).OrderBy(keySelector: n => n, comparer: StringComparer.Ordinal).ToArray();

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

        string json = JsonConvert.SerializeObject(value: envelope, settings: AppSerializerSettings);
        JObject parsed = JObject.Parse(json: json);

        string[] expected = ["args", "data", "message", "status"];
        Assert.Equal(expected: expected, actual: SortedPropertyNames(obj: parsed));
    }

    [Fact]
    public void DataResponseDto_SerializesTheLockedEnvelopeFields()
    {
        DataResponseDto<object> envelope = new() { Data = new { sample = 1 } };

        string json = JsonConvert.SerializeObject(value: envelope, settings: AppSerializerSettings);
        JObject parsed = JObject.Parse(json: json);

        string[] expected = ["data"];
        Assert.Equal(expected: expected, actual: SortedPropertyNames(obj: parsed));
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

        string json = JsonConvert.SerializeObject(value: envelope, settings: AppSerializerSettings);
        JObject parsed = JObject.Parse(json: json);

        string[] expected = ["data", "has_more", "next_page"];
        Assert.Equal(expected: expected, actual: SortedPropertyNames(obj: parsed));

        Assert.Equal(expected: 2, actual: parsed[propertyName: "next_page"]!.Value<int>());
        Assert.True(condition: parsed[propertyName: "has_more"]!.Value<bool>());
    }

    [Fact]
    public async Task SuccessEnvelope_OverRealHost_MatchesTheLockedStatusResponseShape()
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();
        HttpResponseMessage response = await client.GetAsync(requestUri: "/api/v1/dashboard/devices");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        JObject parsed = JObject.Parse(json: body);

        string[] expected = ["args", "data", "message", "status"];
        Assert.Equal(expected: expected, actual: SortedPropertyNames(obj: parsed));
        Assert.Equal(expected: "ok", actual: parsed[propertyName: "status"]!.Value<string>());
        Assert.Equal(expected: JTokenType.Array, actual: parsed[propertyName: "data"]!.Type);
    }

    [Fact]
    public async Task ErrorEnvelope_OverRealHost_MatchesTheLockedProblemDetailsShape()
    {
        HttpClient client = _factory.CreateClient().AsAuthenticated();
        HttpResponseMessage response = await client.DeleteAsync(
            requestUri: $"/api/v1/dashboard/libraries/{Ulid.NewUlid()}"
        );

        Assert.Equal(expected: HttpStatusCode.NotFound, actual: response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        JObject parsed = JObject.Parse(json: body);

        string[] requiredFields = ["detail", "instance", "status", "title", "type"];
        string[] actualFields = SortedPropertyNames(obj: parsed);

        foreach (string field in requiredFields)
            Assert.Contains(expected: field, collection: actualFields);

        Assert.Equal(expected: 404, actual: parsed[propertyName: "status"]!.Value<int>());
        Assert.Equal(expected: "/docs/errors/not-found", actual: parsed[propertyName: "type"]!.Value<string>());
    }
}
