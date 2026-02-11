using Asp.Versioning.ApiExplorer;
using AspNetCore.Swagger.Themes;
using Microsoft.Extensions.Options;
using NoMercy.NmSystem.Information;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NoMercy.Server.Configuration.Swagger;

public static class SwaggerConfiguration
{
    public static void AddSwagger(IServiceCollection services)
    {
        services.AddSwaggerGen(options => options.OperationFilter<SwaggerDefaultValues>());
        services.AddSwaggerGenNewtonsoftSupport();
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
    }

    public static void UseSwaggerUi(IApplicationBuilder app, IApiVersionDescriptionProvider provider)
    {
        app.UseSwagger();
        app.UseSwaggerUI(ModernStyle.Dark, options =>
        {
            options.RoutePrefix = string.Empty;
            options.DocumentTitle = "NoMercy MediaServer API";
            options.OAuthClientId(Config.TokenClientId);
            options.OAuthScopes("openid");
            options.EnablePersistAuthorization();
            options.EnableTryItOutByDefault();

            IReadOnlyList<ApiVersionDescription> descriptions = provider.ApiVersionDescriptions;
            foreach (ApiVersionDescription description in descriptions)
            {
                string groupName = $"v{description.ApiVersion.MajorVersion}";
                string url = $"/swagger/{groupName}/swagger.json";
                string name = groupName.ToUpperInvariant();
                options.SwaggerEndpoint(url, name);
            }
        });
    }
}
