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

using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using NoMercy.Service.Configuration.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace NoMercy.Tests.Service.Configuration.Swagger;

/// <summary>
/// <see cref="SwaggerDefaultValues"/> is the Swashbuckle operation filter that
/// keeps the generated OpenAPI document honest: it strips response content
/// types the action doesn't actually produce (Swashbuckle otherwise lists every
/// formatter's media type for every status code) and folds
/// <see cref="ApiParameterDescription.IsRequired"/> into the emitted parameter
/// — losing either makes the published spec lie to API consumers/codegen.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class SwaggerDefaultValuesTests
{
    private static ApiDescription EmptyApiDescription() =>
        new() { ActionDescriptor = new ActionDescriptor() };

    private static OperationFilterContext Context(ApiDescription apiDescription) =>
        new(apiDescription: apiDescription, schemaRegistry: null!, schemaRepository: null!, document: null!, methodInfo: typeof(object).GetMethods()[0]);

    [Fact]
    public void Apply_ResponseContentTypeNotInSupportedFormats_IsRemoved()
    {
        ApiDescription apiDescription = EmptyApiDescription();
        apiDescription.SupportedResponseTypes.Add(
            item: new()
            {
                StatusCode = 200,
                ApiResponseFormats = [new() { MediaType = "application/json" }],
            }
        );

        OpenApiResponse response = new()
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [key: "application/json"] = new(),
                [key: "text/plain"] = new(),
            },
        };
        OpenApiOperation operation = new() { Responses = new() { [key: "200"] = response } };

        new SwaggerDefaultValues().Apply(operation: operation, context: Context(apiDescription: apiDescription));

        Assert.Contains(expected: "application/json", collection: response.Content.Keys);
        Assert.DoesNotContain(expected: "text/plain", collection: response.Content.Keys);
    }

    [Fact]
    public void Apply_DefaultResponseType_MatchesByDefaultResponseKey()
    {
        ApiDescription apiDescription = EmptyApiDescription();
        apiDescription.SupportedResponseTypes.Add(
            item: new()
            {
                IsDefaultResponse = true,
                ApiResponseFormats = [new() { MediaType = "application/json" }],
            }
        );

        OpenApiResponse response = new()
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [key: "application/json"] = new(),
                [key: "application/xml"] = new(),
            },
        };
        OpenApiOperation operation = new() { Responses = new() { [key: "default"] = response } };

        new SwaggerDefaultValues().Apply(operation: operation, context: Context(apiDescription: apiDescription));

        Assert.Contains(expected: "application/json", collection: response.Content.Keys);
        Assert.DoesNotContain(expected: "application/xml", collection: response.Content.Keys);
    }

    [Fact]
    public void Apply_ResponseWithNoMatchingOperationEntry_IsSkippedWithoutThrowing()
    {
        ApiDescription apiDescription = EmptyApiDescription();
        apiDescription.SupportedResponseTypes.Add(
            item: new()
            {
                StatusCode = 404,
                ApiResponseFormats = [new() { MediaType = "application/json" }],
            }
        );
        OpenApiOperation operation = new() { Responses = new() };

        Exception? thrown = Record.Exception(testCode: () =>
            new SwaggerDefaultValues().Apply(operation: operation, context: Context(apiDescription: apiDescription))
        );

        Assert.Null(@object: thrown);
    }

    [Fact]
    public void Apply_NoParameters_ReturnsWithoutThrowing()
    {
        ApiDescription apiDescription = EmptyApiDescription();
        OpenApiOperation operation = new() { Parameters = null };

        Exception? thrown = Record.Exception(testCode: () =>
            new SwaggerDefaultValues().Apply(operation: operation, context: Context(apiDescription: apiDescription))
        );

        Assert.Null(@object: thrown);
    }

    [Fact]
    public void Apply_ParameterMarkedRequiredInDescription_ForcesOperationParameterRequired()
    {
        ApiDescription apiDescription = EmptyApiDescription();
        apiDescription.ParameterDescriptions.Add(
            item: new()
            {
                Name = "libraryId",
                IsRequired = true,
                // Schema left null on the operation parameter below so the
                // default-value branch (which needs a real ModelMetadata) is
                // never entered — this test targets only the Required merge.
            }
        );
        OpenApiParameter parameter = new()
        {
            Name = "libraryId",
            Required = false,
            Description = "already set",
            Schema = null,
        };
        OpenApiOperation operation = new() { Parameters = [parameter] };

        new SwaggerDefaultValues().Apply(operation: operation, context: Context(apiDescription: apiDescription));

        Assert.True(condition: parameter.Required);
    }

    [Fact]
    public void Apply_ParameterWithNoMatchingDescription_LeavesParameterUnchanged()
    {
        ApiDescription apiDescription = EmptyApiDescription();
        OpenApiParameter parameter = new()
        {
            Name = "unmatched",
            Required = false,
            Description = "already set",
        };
        OpenApiOperation operation = new() { Parameters = [parameter] };

        new SwaggerDefaultValues().Apply(operation: operation, context: Context(apiDescription: apiDescription));

        Assert.False(condition: parameter.Required);
    }
}
