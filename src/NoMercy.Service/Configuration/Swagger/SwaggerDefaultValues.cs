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

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NoMercy.Service.Configuration.Swagger;

public class SwaggerDefaultValues : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ApiDescription apiDescription = context.ApiDescription;

        foreach (ApiResponseType responseType in context.ApiDescription.SupportedResponseTypes)
        {
            string responseKey = responseType.IsDefaultResponse
                ? "default"
                : responseType.StatusCode.ToString();
            if (
                operation.Responses?.TryGetValue(responseKey, out IOpenApiResponse? response)
                != true
            )
                continue;
            if (response?.Content is null)
                continue;

            foreach (string? contentType in response.Content.Keys)
                if (responseType.ApiResponseFormats.All(x => x.MediaType != contentType))
                    response.Content.Remove(contentType);
        }

        if (operation.Parameters == null)
            return;

        foreach (OpenApiParameter parameter in operation.Parameters)
        {
            ApiParameterDescription? description =
                apiDescription.ParameterDescriptions.FirstOrDefault(p => p.Name == parameter.Name);
            if (description is null)
                continue;

            parameter.Description ??= description.ModelMetadata.Description;

            if (
                parameter.Schema is OpenApiSchema { Default: null } schema
                && description.DefaultValue != null
                && description.DefaultValue is not DBNull
                && description.ModelMetadata is { } modelMetadata
            )
            {
                string json = JsonSerializer.Serialize(
                    description.DefaultValue,
                    modelMetadata.ModelType
                );
                schema.Default = JsonNode.Parse(json);
            }

            parameter.Required |= description.IsRequired;
        }
    }
}
