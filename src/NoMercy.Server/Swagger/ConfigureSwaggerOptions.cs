using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using NoMercy.NmSystem;
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
            options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description));

            options.AddSecurityDefinition("Keycloak", new()
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new()
                {
                    Implicit = new()
                    {
                        AuthorizationUrl =
                            new($"{Config.AuthBaseUrl}protocol/openid-connect/auth"),
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
                Name = "Bearer",
                Scheme = "Bearer"
            };

            options.AddSecurityRequirement(new()
            {
                { keycloakSecurityScheme, Array.Empty<string>() },
                {
                    new() { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                    []
                }
            });
        }
    }

    private static OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
    {
        OpenApiInfo info = new()
        {
            Title = "NoMercy API",
            Version = description.ApiVersion.ToString(),
            Description = "NoMercy API",
            Contact = new()
            {
                Name = "NoMercy",
                Email = "info@nomercy.tv",
                Url = new("https://nomercy.tv")
            },
            TermsOfService = new("https://nomercy.tv/terms-of-service")
        };

        if (description.IsDeprecated) info.Description += " This API version has been deprecated.";

        return info;
    }
}