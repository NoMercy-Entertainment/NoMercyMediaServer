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

namespace NoMercy.Tests.Service.Configuration.Swagger;

/// <summary>
/// <see cref="SwaggerDefaultValues"/> is the Swashbuckle operation filter that
/// keeps the generated OpenAPI document honest: it strips response content
/// types the action doesn't actually produce (Swashbuckle otherwise lists every
/// formatter's media type for every status code) and folds
/// <see cref="ApiParameterDescription.IsRequired"/> into the emitted parameter
/// — losing either makes the published spec lie to API consumers/codegen.
/// </summary>
[Trait("Category", "Unit")]
public class SwaggerDefaultValuesTests
{
    private static ApiDescription EmptyApiDescription() =>
        new() { ActionDescriptor = new ActionDescriptor() };

    private static OperationFilterContext Context(ApiDescription apiDescription) =>
        new(apiDescription, null!, null!, null!, typeof(object).GetMethods()[0]);

    [Fact]
    public void Apply_ResponseContentTypeNotInSupportedFormats_IsRemoved()
    {
        ApiDescription apiDescription = EmptyApiDescription();
        apiDescription.SupportedResponseTypes.Add(
            new()
            {
                StatusCode = 200,
                ApiResponseFormats = [new() { MediaType = "application/json" }],
            }
        );

        OpenApiResponse response = new()
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new(),
                ["text/plain"] = new(),
            },
        };
        OpenApiOperation operation = new() { Responses = new() { ["200"] = response } };

        new SwaggerDefaultValues().Apply(operation, Context(apiDescription));

        Assert.Contains("application/json", response.Content.Keys);
        Assert.DoesNotContain("text/plain", response.Content.Keys);
    }

    [Fact]
    public void Apply_DefaultResponseType_MatchesByDefaultResponseKey()
    {
        ApiDescription apiDescription = EmptyApiDescription();
        apiDescription.SupportedResponseTypes.Add(
            new()
            {
                IsDefaultResponse = true,
                ApiResponseFormats = [new() { MediaType = "application/json" }],
            }
        );

        OpenApiResponse response = new()
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new(),
                ["application/xml"] = new(),
            },
        };
        OpenApiOperation operation = new() { Responses = new() { ["default"] = response } };

        new SwaggerDefaultValues().Apply(operation, Context(apiDescription));

        Assert.Contains("application/json", response.Content.Keys);
        Assert.DoesNotContain("application/xml", response.Content.Keys);
    }

    [Fact]
    public void Apply_ResponseWithNoMatchingOperationEntry_IsSkippedWithoutThrowing()
    {
        ApiDescription apiDescription = EmptyApiDescription();
        apiDescription.SupportedResponseTypes.Add(
            new()
            {
                StatusCode = 404,
                ApiResponseFormats = [new() { MediaType = "application/json" }],
            }
        );
        OpenApiOperation operation = new() { Responses = new() };

        Exception? thrown = Record.Exception(() =>
            new SwaggerDefaultValues().Apply(operation, Context(apiDescription))
        );

        Assert.Null(thrown);
    }

    [Fact]
    public void Apply_NoParameters_ReturnsWithoutThrowing()
    {
        ApiDescription apiDescription = EmptyApiDescription();
        OpenApiOperation operation = new() { Parameters = null };

        Exception? thrown = Record.Exception(() =>
            new SwaggerDefaultValues().Apply(operation, Context(apiDescription))
        );

        Assert.Null(thrown);
    }

    [Fact]
    public void Apply_ParameterMarkedRequiredInDescription_ForcesOperationParameterRequired()
    {
        ApiDescription apiDescription = EmptyApiDescription();
        apiDescription.ParameterDescriptions.Add(
            new()
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

        new SwaggerDefaultValues().Apply(operation, Context(apiDescription));

        Assert.True(parameter.Required);
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

        new SwaggerDefaultValues().Apply(operation, Context(apiDescription));

        Assert.False(parameter.Required);
    }
}
