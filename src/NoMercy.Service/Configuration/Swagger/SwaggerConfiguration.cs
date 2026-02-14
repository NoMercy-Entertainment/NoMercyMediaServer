using Asp.Versioning.ApiExplorer;
using AspNetCore.Swagger.Themes;
using Microsoft.Extensions.Options;
using NoMercy.NmSystem.Information;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NoMercy.Service.Configuration.Swagger;

public static class SwaggerConfiguration
{
    private static readonly Lock ThemeLock = new();

    public static void AddSwagger(IServiceCollection services)
    {
        services.AddSwaggerGen(options => options.OperationFilter<SwaggerDefaultValues>());
        services.AddSwaggerGenNewtonsoftSupport();
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
    }

    public static void UseSwaggerUi(IApplicationBuilder app, IApiVersionDescriptionProvider provider)
    {
        app.UseSwagger();
        // Lock required: AspNetCore.SwaggerUI.Themes uses a non-concurrent Dictionary internally,
        // which causes corruption when multiple hosts initialize in parallel (e.g. during tests).
        lock (ThemeLock)
        app.UseSwaggerUI(Theme.Dark, options =>
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
