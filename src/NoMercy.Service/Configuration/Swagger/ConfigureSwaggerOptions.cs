using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using NoMercy.NmSystem.Information;
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
            options.SwaggerDoc(groupName, CreateInfoForApiVersion(description, groupName));
        }

        // Configure security definitions - only add once, not per version
        options.AddSecurityDefinition("Keycloak", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Flows = new OpenApiOAuthFlows
            {
                Implicit = new OpenApiOAuthFlow
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

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference("Keycloak", document), [] },
            { new OpenApiSecuritySchemeReference("Bearer", document), [] }
        });
    }

    private static OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description, string groupName)
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
                Url = new("https://nomercy.tv")
            },
            TermsOfService = new("https://nomercy.tv/terms-of-service")
        };

        if (description.IsDeprecated) info.Description += " This API version has been deprecated.";

        return info;
    }
}
