using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using NoMercy.NmSystem.Information;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NoMercy.Server.Swagger;

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
            options.SwaggerDoc(groupName, CreateInfoForApiVersion(description, groupName));
        }

        // Configure security definitions - only add once, not per version
        options.AddSecurityDefinition("Keycloak", new()
        {
            Type = SecuritySchemeType.OAuth2,
            Flows = new()
            {
                Implicit = new()
                {
                    AuthorizationUrl = new($"{Config.AuthBaseUrl}protocol/openid-connect/auth"),
                    Scopes = new Dictionary<string, string>
                    {
                        { "openid", "openid" },
                        { "profile", "profile" }
                    }
                }
            }
        });

        OpenApiSecurityScheme keycloakSecurityScheme = new()
        {
            Reference = new()
            {
                Id = "Keycloak",
                Type = ReferenceType.SecurityScheme
            },
            In = ParameterLocation.Header,
            Name = "Authorization",
            Type = SecuritySchemeType.OAuth2,
            Description = "Use the Keycloak authorization server to authenticate.",
            Scheme = "Bearer"
        };

        options.AddSecurityRequirement(new()
        {
            { keycloakSecurityScheme, [] },
            {
                new() { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                []
            }
        });
    }

    private static OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description, string groupName)
    {
        string baseDescription = groupName switch
        {
            "v1" => """
                NoMercy MediaServer API v1

                Core API endpoints for media library management, streaming, and user interactions.
                """,
            "v2" => """
                NoMercy MediaServer API v2

                Enhanced API with EncoderV2 distributed encoding system.

                ## EncoderV2 Features
                - **Profiles**: Manage encoding profiles with video, audio, and subtitle configurations
                - **Jobs**: Submit, monitor, and control encoding jobs
                - **Tasks**: Distributed task execution across encoder nodes
                - **Real-time Updates**: SignalR hub at `/encodingProgressHub` for live progress

                ## Authentication
                All endpoints require OAuth2 authentication via Keycloak.
                Moderator role required for encoding operations.
                """,
            _ => "NoMercy MediaServer API"
        };

        OpenApiInfo info = new()
        {
            Title = "NoMercy API",
            Version = groupName,
            Description = baseDescription,
            Contact = new()
            {
                Name = "NoMercy",
                Email = "info@nomercy.tv",
                Url = new("https://nomercy.tv")
            },
            TermsOfService = new("https://nomercy.tv/terms-of-service")
        };

        if (description.IsDeprecated) info.Description += "\n\n**⚠️ This API version has been deprecated.**";

        return info;
    }
}