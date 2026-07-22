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

using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using NoMercy.NmSystem.Configuration;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NoMercy.Service.Configuration.Swagger;

public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        foreach (ApiVersionDescription description in _provider.ApiVersionDescriptions)
        {
            string groupName = $"v{description.ApiVersion.MajorVersion}";
            options.SwaggerDoc(name: groupName, info: CreateInfoForApiVersion(description: description, groupName: groupName));
        }

        // Configure security definitions - only add once, not per version
        options.AddSecurityDefinition(
            name: "Keycloak",
            securityScheme: new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new()
                {
                    Implicit = new()
                    {
                        AuthorizationUrl = new(uriString: $"{ExternalServicesConfig.Current.AuthBaseUrl}protocol/openid-connect/auth"),
                        Scopes = new Dictionary<string, string>
                        {
                            { "openid", "openid" },
                            { "profile", "profile" },
                        },
                    },
                },
            }
        );

        options.AddSecurityRequirement(securityRequirement: document =>
            new() { { new(referenceId: "Keycloak", hostDocument: document), [] }, { new(referenceId: "Bearer", hostDocument: document), [] } }
        );
    }

    private static OpenApiInfo CreateInfoForApiVersion(
        ApiVersionDescription description,
        string groupName
    )
    {
        OpenApiInfo info = new()
        {
            Title = "NoMercy API",
            Version = groupName, // Use forced group name (e.g., v1)
            Description = "NoMercy API",
            Contact = new()
            {
                Name = "NoMercy",
                Email = "info@nomercy.tv",
                Url = new(uriString: "https://nomercy.tv"),
            },
            TermsOfService = new(uriString: "https://nomercy.tv/terms-of-service"),
        };

        if (description.IsDeprecated)
            info.Description += " This API version has been deprecated.";

        return info;
    }
}
